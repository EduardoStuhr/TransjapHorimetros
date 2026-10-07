param(
    [switch]$Foreground,
    [switch]$NoBuild,
    [int]$TimeoutSeconds = 60
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$environmentFile = Join-Path $repositoryRoot ".env"

if (-not (Test-Path -LiteralPath $environmentFile))
{
    Write-Error "Arquivo .env não encontrado na raiz. Copie .env.example para .env antes de iniciar a API."
    exit 1
}

$envVars = @{}
foreach ($line in Get-Content -LiteralPath $environmentFile)
{
    if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith("#"))
    {
        continue
    }

    $separator = $line.IndexOf("=")
    if ($separator -le 0)
    {
        continue
    }

    $name = $line.Substring(0, $separator).Trim()
    $value = $line.Substring($separator + 1).Trim()
    $envVars[$name] = $value
    [Environment]::SetEnvironmentVariable($name, $value, "Process")
}

foreach ($requiredVariable in @("SQLSERVER_HOST", "SQLSERVER_PORT", "ConnectionStrings__DefaultConnection"))
{
    if (-not $envVars.ContainsKey($requiredVariable) -or [string]::IsNullOrWhiteSpace($envVars[$requiredVariable]))
    {
        Write-Error "A variável $requiredVariable deve ser definida no arquivo .env."
        exit 1
    }
}

if ($envVars["ConnectionStrings__DefaultConnection"].Contains("your_secure_password_here"))
{
    Write-Error "Substitua a senha de exemplo do SQL Server no arquivo .env antes de iniciar a API."
    exit 1
}

$sqlServerHost = $envVars["SQLSERVER_HOST"]
$sqlServerPort = [int]$envVars["SQLSERVER_PORT"]

function Test-TcpPort([string]$HostAddress, [int]$Port, [int]$TimeoutMs = 1500)
{
    try
    {
        $tcpClient = New-Object System.Net.Sockets.TcpClient
        $asyncResult = $tcpClient.BeginConnect($HostAddress, $Port, $null, $null)
        $waitSuccess = $asyncResult.AsyncWaitHandle.WaitOne($TimeoutMs, $false)
        if (-not $waitSuccess)
        {
            $tcpClient.Close()
            return $false
        }

        $tcpClient.EndConnect($asyncResult)
        $tcpClient.Close()
        return $true
    }
    catch
    {
        return $false
    }
}

Write-Host "Verificando SQL Server em $sqlServerHost`:$sqlServerPort..." -ForegroundColor Cyan
if (-not (Test-TcpPort -HostAddress $sqlServerHost -Port $sqlServerPort))
{
    Write-Error "SQL Server não está acessível em $sqlServerHost`:$sqlServerPort. Inicie o serviço ou execute 'docker compose up -d sqlserver'."
    exit 1
}
Write-Host "SQL Server está acessível." -ForegroundColor Green

$localDotnet = Join-Path $repositoryRoot ".dotnet\dotnet.exe"
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { "dotnet" }
$project = Join-Path $repositoryRoot "apps\api\src\TransjapHorimetros.Api\TransjapHorimetros.Api.csproj"
$arguments = @("run", "--project", $project, "--no-launch-profile")
if ($NoBuild)
{
    $arguments += "--no-build"
}

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://127.0.0.1:5080"

if ($Foreground)
{
    Write-Host "Iniciando Transjap Horímetros API em primeiro plano..." -ForegroundColor Cyan
    & $dotnet @arguments
    exit $LASTEXITCODE
}

$toolsDirectory = Join-Path $repositoryRoot ".tools"
if (-not (Test-Path -LiteralPath $toolsDirectory))
{
    New-Item -ItemType Directory -Force -Path $toolsDirectory | Out-Null
}

$stdout = Join-Path $toolsDirectory "api.stdout.log"
$stderr = Join-Path $toolsDirectory "api.stderr.log"
Set-Content -LiteralPath $stdout -Value ""
Set-Content -LiteralPath $stderr -Value ""

Write-Host "Iniciando Transjap Horímetros API em segundo plano..." -ForegroundColor Cyan
$process = Start-Process `
    -FilePath $dotnet `
    -ArgumentList $arguments `
    -WorkingDirectory $repositoryRoot `
    -RedirectStandardOutput $stdout `
    -RedirectStandardError $stderr `
    -WindowStyle Hidden `
    -PassThru

$pidFile = Join-Path $toolsDirectory "api.pid"
Set-Content -LiteralPath $pidFile -Value $process.Id

$healthUrl = "http://127.0.0.1:5080/health/ready"
Write-Host "Aguardando /health/ready responder 200 (PID: $($process.Id))..." -ForegroundColor Cyan
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$isReady = $false

while ((Get-Date) -lt $deadline)
{
    $procCheck = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
    if ($null -eq $procCheck -or $procCheck.HasExited)
    {
        Write-Host "O processo da API encerrou inesperadamente." -ForegroundColor Red
        break
    }

    try
    {
        $response = Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
        if ($response.StatusCode -eq 200)
        {
            $isReady = $true
            break
        }
    }
    catch
    {
    }

    Start-Sleep -Milliseconds 800
}

if ($isReady)
{
    Write-Host "Transjap Horímetros API está pronta e saudável." -ForegroundColor Green
    Write-Host "URL Base: http://127.0.0.1:5080" -ForegroundColor Green
    Write-Host "Health:   $healthUrl" -ForegroundColor Green
    Write-Host "Swagger:  http://127.0.0.1:5080/swagger" -ForegroundColor Green
    Write-Output $process.Id
    exit 0
}

Write-Host "FALHA NA INICIALIZAÇÃO DA API." -ForegroundColor Red
if (Test-Path -LiteralPath $stderr)
{
    Get-Content -LiteralPath $stderr -Tail 20 | Write-Host -ForegroundColor Red
}
if (Test-Path -LiteralPath $stdout)
{
    Get-Content -LiteralPath $stdout -Tail 20 | Write-Host -ForegroundColor Yellow
}
exit 1

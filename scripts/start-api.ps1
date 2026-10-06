param(
    [switch]$Foreground,
    [switch]$NoBuild,
    [int]$TimeoutSeconds = 30
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

$postgresPort = if ($envVars.ContainsKey("POSTGRES_PORT")) { [int]$envVars["POSTGRES_PORT"] } else { 5432 }

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

Write-Host "Verificando se o PostgreSQL está respondendo em 127.0.0.1:$postgresPort..." -ForegroundColor Cyan
if (-not (Test-TcpPort -HostAddress "127.0.0.1" -Port $postgresPort))
{
    Write-Error "PostgreSQL não está acessível em 127.0.0.1:$postgresPort. Inicie o PostgreSQL local ou execute 'docker compose up -d' antes de subir a API."
    exit 1
}
Write-Host "PostgreSQL está ativo." -ForegroundColor Green

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

# Limpa logs anteriores
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

Write-Host "Aguardando inicialização da API (PID: $($process.Id)) em http://127.0.0.1:5080/health/ready..." -ForegroundColor Cyan

$healthUrl = "http://127.0.0.1:5080/health/ready"
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$isReady = $false

while ((Get-Date) -lt $deadline)
{
    $procCheck = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
    if ($procCheck -eq $null -or $procCheck.HasExited)
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
        # Ainda inicializando
    }

    Start-Sleep -Milliseconds 800
}

if ($isReady)
{
    Write-Host "Transjap Horímetros API está pronta e saudável!" -ForegroundColor Green
    Write-Host "URL Base: http://127.0.0.1:5080" -ForegroundColor Green
    Write-Host "Health:   http://127.0.0.1:5080/health/ready" -ForegroundColor Green
    Write-Host "Swagger:  http://127.0.0.1:5080/swagger" -ForegroundColor Green
    Write-Output $process.Id
    exit 0
}
else
{
    Write-Host "FALHA NA INICIALIZAÇÃO DA API." -ForegroundColor Red
    if (Test-Path -LiteralPath $stderr)
    {
        $stderrContent = Get-Content -LiteralPath $stderr -Tail 20
        if (-not [string]::IsNullOrWhiteSpace($stderrContent))
        {
            Write-Host "--- Últimas linhas de api.stderr.log ---" -ForegroundColor Red
            Write-Host ($stderrContent -join "`n") -ForegroundColor Red
        }
    }
    if (Test-Path -LiteralPath $stdout)
    {
        $stdoutContent = Get-Content -LiteralPath $stdout -Tail 20
        if (-not [string]::IsNullOrWhiteSpace($stdoutContent))
        {
            Write-Host "--- Últimas linhas de api.stdout.log ---" -ForegroundColor Yellow
            Write-Host ($stdoutContent -join "`n") -ForegroundColor Yellow
        }
    }
    exit 1
}

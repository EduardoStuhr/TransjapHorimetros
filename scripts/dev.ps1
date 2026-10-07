param(
    [switch]$NoFrontend
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$environmentFile = Join-Path $repositoryRoot ".env"

if (-not (Test-Path -LiteralPath $environmentFile))
{
    Write-Host "Arquivo .env não encontrado. Copiando de .env.example..." -ForegroundColor Yellow
    Copy-Item -LiteralPath (Join-Path $repositoryRoot ".env.example") -Destination $environmentFile
    Write-Host "Arquivo .env criado. Defina uma senha forte do SQL Server antes de continuar." -ForegroundColor Yellow
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

$requiredVariables = @(
    "SQLSERVER_HOST",
    "SQLSERVER_PORT",
    "SQLSERVER_DATABASE",
    "SQLSERVER_USER",
    "SQLSERVER_PASSWORD",
    "ConnectionStrings__DefaultConnection"
)
foreach ($requiredVariable in $requiredVariables)
{
    if (-not $envVars.ContainsKey($requiredVariable) -or [string]::IsNullOrWhiteSpace($envVars[$requiredVariable]))
    {
        Write-Error "A variável $requiredVariable deve ser definida no arquivo .env."
        exit 1
    }
}

if ($envVars["SQLSERVER_PASSWORD"] -eq "your_secure_password_here" -or
    $envVars["ConnectionStrings__DefaultConnection"].Contains("your_secure_password_here"))
{
    Write-Error "Substitua a senha de exemplo do SQL Server no arquivo .env antes de iniciar o ambiente."
    exit 1
}

$sqlServerHost = $envVars["SQLSERVER_HOST"]
$sqlServerPort = [int]$envVars["SQLSERVER_PORT"]
$useDocker = -not $envVars.ContainsKey("SQLSERVER_USE_DOCKER") -or
    [bool]::Parse($envVars["SQLSERVER_USE_DOCKER"])

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

Write-Host "==> Verificando SQL Server ($sqlServerHost`:$sqlServerPort)..." -ForegroundColor Cyan
if (-not (Test-TcpPort -HostAddress $sqlServerHost -Port $sqlServerPort))
{
    if (-not $useDocker)
    {
        Write-Error "SQL Server não está acessível e SQLSERVER_USE_DOCKER está desabilitado."
        exit 1
    }

    if (-not (Get-Command "docker" -ErrorAction SilentlyContinue))
    {
        Write-Error "SQL Server não está acessível e o Docker não está instalado. Inicie um SQL Server local ou instale o Docker."
        exit 1
    }

    Write-Host "SQL Server não detectado. Iniciando via Docker Compose..." -ForegroundColor Yellow
    & docker compose up -d sqlserver
    if ($LASTEXITCODE -ne 0)
    {
        exit $LASTEXITCODE
    }

    $deadline = (Get-Date).AddSeconds(90)
    while ((Get-Date) -lt $deadline -and -not (Test-TcpPort -HostAddress $sqlServerHost -Port $sqlServerPort))
    {
        Start-Sleep -Milliseconds 750
    }

    if (-not (Test-TcpPort -HostAddress $sqlServerHost -Port $sqlServerPort))
    {
        Write-Error "Tempo limite esgotado aguardando o SQL Server em $sqlServerHost`:$sqlServerPort."
        exit 1
    }
}
Write-Host "SQL Server operacional em $sqlServerHost`:$sqlServerPort." -ForegroundColor Green

Write-Host "==> Iniciando API ASP.NET Core..." -ForegroundColor Cyan
$startApiScript = Join-Path $repositoryRoot "scripts\start-api.ps1"
& powershell -ExecutionPolicy Bypass -File $startApiScript
if ($LASTEXITCODE -ne 0)
{
    Write-Error "Falha ao iniciar a API."
    exit $LASTEXITCODE
}

Write-Host "==> Verificando Frontend Next.js..." -ForegroundColor Cyan
$frontendPort = 3000
$frontendRunning = Test-TcpPort -HostAddress "127.0.0.1" -Port $frontendPort

if ($frontendRunning)
{
    Write-Host "Frontend já está ativo na porta $frontendPort." -ForegroundColor Green
}
elseif (-not $NoFrontend)
{
    $npm = (Get-Command "npm" -ErrorAction Stop).Source
    $frontProcess = Start-Process `
        -FilePath $npm `
        -ArgumentList @("run", "dev") `
        -WorkingDirectory $repositoryRoot `
        -WindowStyle Hidden `
        -PassThru
    Write-Host "Frontend iniciado (PID: $($frontProcess.Id))." -ForegroundColor Green
}

Write-Host "`n========================================================" -ForegroundColor Magenta
Write-Host "  TRANSJAP HORÍMETROS - AMBIENTE DE DESENVOLVIMENTO ATIVO" -ForegroundColor Magenta
Write-Host "========================================================" -ForegroundColor Magenta
Write-Host "  Frontend Web:  http://localhost:3000" -ForegroundColor Cyan
Write-Host "  API Backend:   http://127.0.0.1:5080" -ForegroundColor Cyan
Write-Host "  Health Check:  http://127.0.0.1:5080/health/ready" -ForegroundColor Cyan
Write-Host "  Swagger UI:    http://127.0.0.1:5080/swagger" -ForegroundColor Cyan
Write-Host "========================================================`n" -ForegroundColor Magenta

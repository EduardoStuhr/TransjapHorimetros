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
    Write-Host "Arquivo .env criado a partir de .env.example. Verifique as credenciais se necessário." -ForegroundColor Green
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

# 1. Verifica PostgreSQL
Write-Host "==> Verificando PostgreSQL (porta $postgresPort)..." -ForegroundColor Cyan
if (-not (Test-TcpPort -HostAddress "127.0.0.1" -Port $postgresPort))
{
    Write-Host "PostgreSQL não detectado na porta $postgresPort. Tentando inicializar..." -ForegroundColor Yellow
    
    $localPgCtl = Join-Path $repositoryRoot ".postgres\pgsql\bin\pg_ctl.exe"
    $localPgData = Join-Path $repositoryRoot ".postgres-data"
    
    if ((Test-Path -LiteralPath $localPgCtl) -and (Test-Path -LiteralPath $localPgData))
    {
        Write-Host "Iniciando PostgreSQL local via pg_ctl..." -ForegroundColor Cyan
        & $localPgCtl -D $localPgData -l (Join-Path $repositoryRoot ".tools\postgres.log") start
    }
    elseif (Get-Command "docker" -ErrorAction SilentlyContinue)
    {
        Write-Host "Iniciando PostgreSQL via Docker Compose..." -ForegroundColor Cyan
        docker compose up -d postgres
    }
    else
    {
        Write-Error "PostgreSQL não está rodando e não foi possível iniciá-lo automaticamente. Inicie o PostgreSQL manualmente."
        exit 1
    }

    # Aguarda o Postgres responder
    $deadline = (Get-Date).AddSeconds(20)
    $pgOk = $false
    while ((Get-Date) -lt $deadline)
    {
        if (Test-TcpPort -HostAddress "127.0.0.1" -Port $postgresPort)
        {
            $pgOk = $true
            break
        }
        Start-Sleep -Milliseconds 500
    }

    if (-not $pgOk)
    {
        Write-Error "Tempo limite esgotado aguardando o PostgreSQL responder na porta $postgresPort."
        exit 1
    }
}
Write-Host "PostgreSQL operacional na porta $postgresPort." -ForegroundColor Green

# 2. Inicia a API ASP.NET Core
Write-Host "==> Verificando API ASP.NET Core..." -ForegroundColor Cyan
$startApiScript = Join-Path $repositoryRoot "scripts\start-api.ps1"
& powershell -ExecutionPolicy Bypass -File $startApiScript

if ($LASTEXITCODE -ne 0)
{
    Write-Error "Falha ao iniciar a API."
    exit $LASTEXITCODE
}

# 3. Verifica / Inicia o Frontend Next.js
Write-Host "==> Verificando Frontend Next.js..." -ForegroundColor Cyan
$frontendPort = 3000
$frontendRunning = Test-TcpPort -HostAddress "127.0.0.1" -Port $frontendPort

if ($frontendRunning)
{
    Write-Host "Frontend já está ativo na porta $frontendPort." -ForegroundColor Green
}
elseif (-not $NoFrontend)
{
    Write-Host "Iniciando Frontend Next.js em segundo plano..." -ForegroundColor Cyan
    $frontProcess = Start-Process `
        -FilePath "npm" `
        -ArgumentList @("run", "dev") `
        -WorkingDirectory $repositoryRoot `
        -WindowStyle Minimized `
        -PassThru

    Write-Host "Frontend iniciado (PID: $($frontProcess.Id))." -ForegroundColor Green
}

Write-Host "`n========================================================" -ForegroundColor Magenta
Write-Host "  TRANSJAP HORÍMETROS — AMBIENTE DE DESENVOLVIMENTO ATIVO" -ForegroundColor Magenta
Write-Host "========================================================" -ForegroundColor Magenta
Write-Host "  Frontend Web:  http://localhost:3000" -ForegroundColor Cyan
Write-Host "  API Backend:   http://127.0.0.1:5080" -ForegroundColor Cyan
Write-Host "  Health Check:  http://127.0.0.1:5080/health/ready" -ForegroundColor Cyan
Write-Host "  Swagger UI:    http://127.0.0.1:5080/swagger" -ForegroundColor Cyan
Write-Host "========================================================`n" -ForegroundColor Magenta

param(
    [string]$Database = "",
    [string]$OutputFolder = ""
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$environmentFile = Join-Path $repositoryRoot ".env"

if (-not (Test-Path -LiteralPath $environmentFile))
{
    Write-Error "Arquivo .env não encontrado na raiz. Copie .env.example para .env antes de executar o backup."
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
}

$dbUser = if ($envVars.ContainsKey("POSTGRES_USER")) { $envVars["POSTGRES_USER"] } else { "postgres" }
$dbPass = if ($envVars.ContainsKey("POSTGRES_PASSWORD")) { $envVars["POSTGRES_PASSWORD"] } else { "postgres" }
$dbPort = if ($envVars.ContainsKey("POSTGRES_PORT")) { $envVars["POSTGRES_PORT"] } else { "5432" }
$targetDb = if (-not [string]::IsNullOrWhiteSpace($Database)) { $Database } elseif ($envVars.ContainsKey("POSTGRES_DB")) { $envVars["POSTGRES_DB"] } else { "transjap_horimetros" }

if ([string]::IsNullOrWhiteSpace($OutputFolder))
{
    $OutputFolder = Join-Path $repositoryRoot ".postgres-backups"
}

if (-not (Test-Path -LiteralPath $OutputFolder))
{
    New-Item -ItemType Directory -Force -Path $OutputFolder | Out-Null
}

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupFile = Join-Path $OutputFolder "${targetDb}_backup_${timestamp}.sql"

$localPgDump = Join-Path $repositoryRoot ".postgres\pgsql\bin\pg_dump.exe"
$pgDumpCmd = if (Test-Path -LiteralPath $localPgDump)
{
    $localPgDump
}
elseif (Get-Command "pg_dump" -ErrorAction SilentlyContinue)
{
    "pg_dump"
}
else
{
    $null
}

Write-Host "Iniciando backup preventivo do banco '$targetDb'..." -ForegroundColor Cyan

if ($pgDumpCmd)
{
    $env:PGPASSWORD = $dbPass
    & $pgDumpCmd -h 127.0.0.1 -p $dbPort -U $dbUser -d $targetDb -F p --clean --if-exists -f $backupFile
    if ($LASTEXITCODE -ne 0)
    {
        Write-Error "Falha ao executar pg_dump (código de saída $LASTEXITCODE)."
        exit $LASTEXITCODE
    }
}
else
{
    # Tenta via docker compose se pg_dump não estiver no host
    Write-Host "pg_dump local não encontrado. Tentando via docker compose..." -ForegroundColor Yellow
    docker compose exec -T postgres pg_dump -U $dbUser -d $targetDb -F p --clean --if-exists > $backupFile
    if ($LASTEXITCODE -ne 0)
    {
        Write-Error "Não foi possível executar o backup nem via executável local nem via Docker."
        exit $LASTEXITCODE
    }
}

if (Test-Path -LiteralPath $backupFile)
{
    $size = (Get-Item -LiteralPath $backupFile).Length
    Write-Host "Backup concluído com sucesso!" -ForegroundColor Green
    Write-Host "Arquivo: $backupFile" -ForegroundColor Green
    Write-Host "Tamanho: $([math]::Round($size / 1KB, 2)) KB" -ForegroundColor Green
}
else
{
    Write-Error "Arquivo de backup não foi gerado."
    exit 1
}

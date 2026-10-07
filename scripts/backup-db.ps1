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

foreach ($requiredVariable in @("SQLSERVER_HOST", "SQLSERVER_PORT", "SQLSERVER_DATABASE", "SQLSERVER_USER", "SQLSERVER_PASSWORD"))
{
    if (-not $envVars.ContainsKey($requiredVariable) -or [string]::IsNullOrWhiteSpace($envVars[$requiredVariable]))
    {
        Write-Error "A variável $requiredVariable deve ser definida no arquivo .env."
        exit 1
    }
}

if ($envVars["SQLSERVER_PASSWORD"] -eq "your_secure_password_here")
{
    Write-Error "Substitua a senha de exemplo do SQL Server no arquivo .env antes de executar o backup."
    exit 1
}

$dbHost = $envVars["SQLSERVER_HOST"]
$dbPort = $envVars["SQLSERVER_PORT"]
$dbUser = $envVars["SQLSERVER_USER"]
$dbPassword = $envVars["SQLSERVER_PASSWORD"]
$targetDb = if ([string]::IsNullOrWhiteSpace($Database)) { $envVars["SQLSERVER_DATABASE"] } else { $Database }

if ([string]::IsNullOrWhiteSpace($OutputFolder))
{
    $OutputFolder = Join-Path $repositoryRoot ".sqlserver-backups"
}
if (-not (Test-Path -LiteralPath $OutputFolder))
{
    New-Item -ItemType Directory -Force -Path $OutputFolder | Out-Null
}

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupFile = Join-Path ([IO.Path]::GetFullPath($OutputFolder)) "${targetDb}_backup_${timestamp}.bak"
$escapedDatabase = $targetDb.Replace("]", "]]")
$docker = Get-Command "docker" -ErrorAction SilentlyContinue
$containerId = $null

if ($docker)
{
    $containerId = (& $docker.Source compose ps -q sqlserver 2>$null | Select-Object -First 1)
}

Write-Host "Iniciando backup preventivo do banco '$targetDb'..." -ForegroundColor Cyan

if (-not [string]::IsNullOrWhiteSpace($containerId))
{
    $serverBackupPath = "/var/opt/mssql/backup/$(Split-Path -Leaf $backupFile)"
    $escapedServerPath = $serverBackupPath.Replace("'", "''")
    $query = "BACKUP DATABASE [$escapedDatabase] TO DISK = N'$escapedServerPath' WITH COPY_ONLY, INIT, CHECKSUM; RESTORE VERIFYONLY FROM DISK = N'$escapedServerPath' WITH CHECKSUM;"

    & $docker.Source compose exec -T sqlserver mkdir -p /var/opt/mssql/backup
    if ($LASTEXITCODE -ne 0)
    {
        Write-Error "Não foi possível preparar o diretório de backup no container."
        exit $LASTEXITCODE
    }

    & $docker.Source compose exec -T -e "SQLCMDPASSWORD=$dbPassword" sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U $dbUser -C -b -Q $query
    if ($LASTEXITCODE -ne 0)
    {
        Write-Error "Falha ao criar ou validar o backup dentro do container SQL Server."
        exit $LASTEXITCODE
    }

    & $docker.Source cp "${containerId}:${serverBackupPath}" $backupFile
    $copyExitCode = $LASTEXITCODE
    & $docker.Source compose exec -T sqlserver rm -f $serverBackupPath
    if ($copyExitCode -ne 0)
    {
        Write-Error "O backup foi criado no container, mas não pôde ser copiado para o host."
        exit $copyExitCode
    }
}
else
{
    $sqlCmd = Get-Command "sqlcmd" -ErrorAction SilentlyContinue
    if (-not $sqlCmd)
    {
        Write-Error "Backup não executado: use o serviço Docker 'sqlserver' ou instale o utilitário sqlcmd."
        exit 1
    }

    $escapedBackupPath = $backupFile.Replace("'", "''")
    $query = "BACKUP DATABASE [$escapedDatabase] TO DISK = N'$escapedBackupPath' WITH COPY_ONLY, INIT, CHECKSUM; RESTORE VERIFYONLY FROM DISK = N'$escapedBackupPath' WITH CHECKSUM;"
    $previousSqlCmdPassword = $env:SQLCMDPASSWORD
    try
    {
        $env:SQLCMDPASSWORD = $dbPassword
        & $sqlCmd.Source -S "$dbHost,$dbPort" -U $dbUser -C -b -Q $query
        if ($LASTEXITCODE -ne 0)
        {
            Write-Error "Falha ao criar ou validar o backup com sqlcmd. Confirme que o serviço SQL Server pode gravar em '$OutputFolder'."
            exit $LASTEXITCODE
        }
    }
    finally
    {
        $env:SQLCMDPASSWORD = $previousSqlCmdPassword
    }
}

if (-not (Test-Path -LiteralPath $backupFile))
{
    Write-Error "Arquivo de backup não foi gerado."
    exit 1
}

$size = (Get-Item -LiteralPath $backupFile).Length
Write-Host "Backup SQL Server criado e verificado com sucesso." -ForegroundColor Green
Write-Host "Arquivo: $backupFile" -ForegroundColor Green
Write-Host "Tamanho: $([math]::Round($size / 1MB, 2)) MB" -ForegroundColor Green

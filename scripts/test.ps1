param(
    [string]$Filter = "",
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$environmentFile = Join-Path $repositoryRoot ".env"

if (-not (Test-Path -LiteralPath $environmentFile))
{
    Write-Error "Arquivo .env não encontrado na raiz. Copie .env.example para .env antes de rodar os testes."
    exit 1
}

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
    [Environment]::SetEnvironmentVariable($name, $value, "Process")
}

if ([string]::IsNullOrWhiteSpace($env:TEST_POSTGRES_CONNECTION_STRING))
{
    Write-Error "A variável TEST_POSTGRES_CONNECTION_STRING não foi definida no arquivo .env."
    exit 1
}

$localDotnet = Join-Path $repositoryRoot ".dotnet\dotnet.exe"
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { "dotnet" }
$solution = Join-Path $repositoryRoot "apps\api\TransjapHorimetros.sln"

$arguments = @("test", $solution)
if ($NoBuild)
{
    $arguments += "--no-build"
}
if (-not [string]::IsNullOrWhiteSpace($Filter))
{
    $arguments += @("--filter", $Filter)
}

Write-Host "Executando testes da solução Transjap Horímetros com variáveis de ambiente carregadas..." -ForegroundColor Cyan
& $dotnet @arguments
exit $LASTEXITCODE

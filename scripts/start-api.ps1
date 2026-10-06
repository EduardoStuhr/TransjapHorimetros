param(
    [switch]$Foreground,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$environmentFile = Join-Path $repositoryRoot ".env"

if (Test-Path -LiteralPath $environmentFile)
{
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

        $name = $line.Substring(0, $separator)
        $value = $line.Substring($separator + 1)
        [Environment]::SetEnvironmentVariable($name, $value, "Process")
    }
}

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
    & $dotnet @arguments
    exit $LASTEXITCODE
}

$toolsDirectory = Join-Path $repositoryRoot ".tools"
New-Item -ItemType Directory -Force -Path $toolsDirectory | Out-Null
$stdout = Join-Path $toolsDirectory "api.stdout.log"
$stderr = Join-Path $toolsDirectory "api.stderr.log"
$process = Start-Process `
    -FilePath $dotnet `
    -ArgumentList $arguments `
    -WorkingDirectory $repositoryRoot `
    -RedirectStandardOutput $stdout `
    -RedirectStandardError $stderr `
    -WindowStyle Hidden `
    -PassThru
Set-Content -LiteralPath (Join-Path $toolsDirectory "api.pid") -Value $process.Id
Write-Output $process.Id

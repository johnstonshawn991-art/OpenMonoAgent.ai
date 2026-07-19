param(
    [string]$Workdir = (Get-Location).Path,
    [int]$AcpPort = 7475,
    [switch]$Run
)

$ErrorActionPreference = "Stop"

$csproj = Join-Path $PSScriptRoot "src/OpenMono.Cli/OpenMono.Cli.csproj"

if (-not (Test-Path $csproj)) {
    Write-Error "Could not find $csproj. Run this script from the cloned 'public' directory."
    exit 1
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "The .NET SDK is required. Install .NET 10 from https://dotnet.microsoft.com/download, then re-run."
    exit 1
}

$sdkVersion = (& dotnet --version)
Write-Host "Found .NET SDK $sdkVersion"
if ([int]($sdkVersion.Split('.')[0]) -lt 10) {
    Write-Error "OpenMono needs .NET 10 or newer. Found $sdkVersion."
    exit 1
}

Write-Host "Building the OpenMono agent (Release)..."
& dotnet build $csproj -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed."
    exit $LASTEXITCODE
}

if ($Run) {
    Write-Host "Starting the ACP agent on port $AcpPort for $Workdir ..."
    & dotnet run --project $csproj -c Release -- --workdir $Workdir --acp-only --acp-port $AcpPort
}
else {
    Write-Host ""
    Write-Host "Build complete. Start the agent for your project with:"
    Write-Host "  dotnet run --project `"$csproj`" -c Release -- --workdir `"$Workdir`" --acp-only --acp-port $AcpPort"
    Write-Host ""
    Write-Host "Then open the same folder in VS Code / Cursor and open the OpenMono Agent panel."
}

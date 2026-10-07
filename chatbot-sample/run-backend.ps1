# Starts the .NET 10 backend on http://localhost:5179
$ErrorActionPreference = "Stop"

# The .NET 10 SDK is installed per-user (winget is blocked by group policy on this machine),
# so point at it explicitly instead of relying on PATH.
$dotnet = Join-Path $env:USERPROFILE ".dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) { $dotnet = "dotnet" }

$env:DOTNET_ROOT = Join-Path $env:USERPROFILE ".dotnet"
# --no-launch-profile below skips launchSettings.json, so set the environment here.
$env:ASPNETCORE_ENVIRONMENT = "Development"
$project = Join-Path $PSScriptRoot "backend\ChatbotApi\ChatbotApi.csproj"
$port = 5179

# Kestrel throws an unhandled AddressInUseException if the port is taken, which buries the
# actual cause in a 40-line stack trace. Say who has it instead.
$listener = Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue |
    Select-Object -First 1
if ($listener) {
    $owner = Get-Process -Id $listener.OwningProcess -ErrorAction SilentlyContinue
    Write-Host "Port $port is already in use by $($owner.ProcessName) (PID $($listener.OwningProcess))." -ForegroundColor Yellow
    Write-Host "That is probably an instance of this backend you already started." -ForegroundColor Yellow
    Write-Host "Stop it with:  Stop-Process -Id $($listener.OwningProcess)" -ForegroundColor Yellow
    exit 1
}

# The port comes from appsettings.json (Kestrel:Endpoints:Http:Url). Passing --urls as well
# would be silently overridden by that config, which only produces a confusing warning.
Write-Host "Backend -> http://localhost:$port  (Ctrl+C to stop)" -ForegroundColor Cyan
& $dotnet run --project $project --no-launch-profile

# Starts the Streamlit frontend on http://localhost:8501 and opens it in your browser.
$ErrorActionPreference = "Stop"

$frontend = Join-Path $PSScriptRoot "frontend"
$venvPython = Join-Path $frontend ".venv\Scripts\python.exe"
$url = "http://localhost:8501"

if (-not (Test-Path $venvPython)) {
    Write-Host "Creating virtual environment and installing dependencies..." -ForegroundColor Yellow
    $python = Join-Path $env:LOCALAPPDATA "Programs\Python\Python312\python.exe"
    if (-not (Test-Path $python)) { $python = "python" }
    & $python -m venv (Join-Path $frontend ".venv")
    & $venvPython -m pip install --quiet -r (Join-Path $frontend "requirements.txt")
}

# Streamlit's own auto-open (server.headless = false) first shows an interactive
# "enter your email" prompt, which blocks startup until you press Enter. Headless mode
# skips that prompt, so we run headless and open the browser ourselves once the port
# is accepting connections.
$opener = Start-Job -ArgumentList $url -ScriptBlock {
    param($url)
    for ($i = 0; $i -lt 120; $i++) {
        try {
            Invoke-WebRequest $url -TimeoutSec 2 -UseBasicParsing | Out-Null
            Start-Process $url
            return "opened $url"
        } catch {
            Start-Sleep -Milliseconds 500
        }
    }
    return "gave up waiting for $url"
}

Write-Host "Frontend -> $url  (opening in your browser; Ctrl+C to stop)" -ForegroundColor Cyan
try {
    & $venvPython -m streamlit run (Join-Path $frontend "app.py") `
        --server.port 8501 --server.headless true --browser.gatherUsageStats false
} finally {
    Remove-Job $opener -Force -ErrorAction SilentlyContinue
}

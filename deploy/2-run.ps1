#Requires -Version 5.1
<#
    WealthLedger - Run  (step 2 of 2)
    ---------------------------------
    Starts the app that was created by "1-setup.ps1" and opens it in your browser.

    A single local server (on http://localhost:5000) serves BOTH the website (SPA)
    and the API, so there is nothing else to start.

    Usage:
      Right-click this file  ->  "Run with PowerShell"
      or from a terminal:      ./deploy/2-run.ps1

    To stop the app: close this window or press Ctrl+C.

    Options:
      -Port <n>   Use a different port (default: 5000).
#>
param(
    [int]$Port = 5000
)

$ErrorActionPreference = 'Stop'

$repoRoot   = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $repoRoot 'publish'
$dll        = Join-Path $publishDir 'WealthLedger.WebApp.dll'
$url        = "http://localhost:$Port"

if (-not (Test-Path $dll)) {
    Write-Host "`n  [X] The app has not been built yet." -ForegroundColor Red
    Write-Host "      Run  ./deploy/1-setup.ps1  first.`n"   -ForegroundColor Red
    exit 1
}

Write-Host "`n=== Starting WealthLedger ===" -ForegroundColor Cyan
Write-Host "  URL:  $url"                     -ForegroundColor Gray
Write-Host "  Site: https://lcarlini.github.io/WealthLedger/" -ForegroundColor Gray
Write-Host "  Implemented by Computer Engineer Leandro Carlini Mingorance" -ForegroundColor Gray
Write-Host "  Reach out: https://lcarlini.github.io/lcarlini/" -ForegroundColor Gray
Write-Host "  Stop: close this window or press Ctrl+C`n" -ForegroundColor Gray

# Open the browser automatically once the server is up (runs in the background).
Start-Job -ArgumentList $url {
    param($url)
    for ($i = 0; $i -lt 40; $i++) {
        try {
            Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 2 | Out-Null
            Start-Process $url
            return
        } catch {
            Start-Sleep -Seconds 1
        }
    }
    # Fallback: open anyway so the user is not left staring at a blank console.
    Start-Process $url
} | Out-Null

# Run the server in THIS window so its logs are visible and Ctrl+C stops it.
Push-Location $publishDir
try {
    dotnet .\WealthLedger.WebApp.dll --urls $url
} finally {
    Pop-Location
    Get-Job | Remove-Job -Force -ErrorAction SilentlyContinue
}

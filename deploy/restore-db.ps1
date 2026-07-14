param(
    [string]$Source = "",
    [string]$TargetDir = "C:\LocalApps\WealthLedger"
)

$ErrorActionPreference = "Stop"

$repoRoot = "C:\git\WealthLedger"

if (-not $Source) {
    $candidates = @(
        (Join-Path $repoRoot "publish\wealthledger.db"),
        (Join-Path $repoRoot "wealthledger.db"),
        (Join-Path $repoRoot "Source\Run\WebApp\wealthledger.db")
    ) | Where-Object { Test-Path $_ } | Sort-Object { (Get-Item $_).Length } -Descending

    $Source = $candidates | Select-Object -First 1
}

if (-not $Source -or -not (Test-Path $Source)) {
    throw "No source database found. Pass -Source with the path to your wealthledger.db backup."
}

$targetDb = Join-Path $TargetDir "wealthledger.db"
New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null

Write-Host "Restoring database" -ForegroundColor Cyan
Write-Host "  From: $Source ($((Get-Item $Source).Length) bytes)"
Write-Host "  To:   $targetDb"

Copy-Item $Source $targetDb -Force
Remove-Item (Join-Path $TargetDir "wealthledger.db-shm") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $TargetDir "wealthledger.db-wal") -Force -ErrorAction SilentlyContinue

Write-Host "Database restored. Restart the app if it is running." -ForegroundColor Green

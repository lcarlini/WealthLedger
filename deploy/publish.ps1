$ErrorActionPreference = "Stop"

$repoRoot = "C:\git\WealthLedger"
$root     = Join-Path $repoRoot "Source\Run"
$apiProj  = Join-Path $root "WebApp\WealthLedger.WebApp.csproj"
$spaDir   = Join-Path $root "SPA"
$outDir   = "C:\LocalApps\WealthLedger"
$legacyDir = Join-Path $repoRoot "publish"

$dbFiles = @("wealthledger.db", "wealthledger.db-shm", "wealthledger.db-wal")

function Backup-DatabaseFiles {
    param([string]$Directory)

    $backup = @{}
    foreach ($dbFile in $dbFiles) {
        $dbPath = Join-Path $Directory $dbFile
        if (Test-Path $dbPath) {
            try {
                $backup[$dbFile] = [IO.File]::ReadAllBytes($dbPath)
            } catch {
                $tempPath = Join-Path $env:TEMP "wealthledger-deploy-$dbFile"
                Copy-Item $dbPath $tempPath -Force
                $backup[$dbFile] = [IO.File]::ReadAllBytes($tempPath)
                Remove-Item $tempPath -Force -ErrorAction SilentlyContinue
            }
        }
    }
    return $backup
}

function Restore-DatabaseFiles {
    param(
        [string]$Directory,
        [hashtable]$Backup
    )

    foreach ($dbFile in $dbFiles) {
        $dbPath = Join-Path $Directory $dbFile
        if ($Backup.ContainsKey($dbFile)) {
            [IO.File]::WriteAllBytes($dbPath, $Backup[$dbFile])
        } else {
            Remove-Item $dbPath -Force -ErrorAction SilentlyContinue
        }
    }
}

function Get-DatabaseFileSize {
    param([string]$Path)
    if (Test-Path $Path) { return (Get-Item $Path).Length }
    return 0
}

function Remove-DatabaseSidecars {
    param([string]$Directory)

    Remove-Item (Join-Path $Directory "wealthledger.db-shm") -Force -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $Directory "wealthledger.db-wal") -Force -ErrorAction SilentlyContinue
}

function Import-DatabaseIfNeeded {
    $targetDb = Join-Path $outDir "wealthledger.db"
    $targetSize = Get-DatabaseFileSize $targetDb

    $candidates = @(
        (Join-Path $legacyDir "wealthledger.db"),
        (Join-Path $repoRoot "wealthledger.db"),
        (Join-Path $repoRoot "Source\Run\WebApp\wealthledger.db")
    ) | Where-Object { Test-Path $_ } | Sort-Object { (Get-Item $_).Length } -Descending

    $bestCandidate = $candidates | Select-Object -First 1
    if (-not $bestCandidate) { return }

    $bestSize = (Get-Item $bestCandidate).Length
    $targetLooksEmpty = $targetSize -lt 120KB
    $candidateHasData = $bestSize -gt ($targetSize + 50KB)

    if ((-not (Test-Path $targetDb)) -or ($targetLooksEmpty -and $candidateHasData)) {
        Write-Host "Migrating database from $bestCandidate ..." -ForegroundColor Yellow
        Copy-Item $bestCandidate $targetDb -Force
        Remove-DatabaseSidecars -Directory $outDir
    }
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

Import-DatabaseIfNeeded

$dbBackup = Backup-DatabaseFiles -Directory $outDir

# 1) Publish API
dotnet publish $apiProj -c Release -o $outDir

# 2) Build SPA (Angular)
Push-Location $spaDir
npm ci
npm run build -- --configuration production
Pop-Location

# 3) Copy dist output into wwwroot
$distRoot = Join-Path $spaDir "dist"
$distCandidate = Get-ChildItem $distRoot -Directory | Select-Object -First 1
if (-not $distCandidate) { throw "Nao achei nada em $distRoot. Confira o output do Angular build." }

$browserPath = Join-Path $distCandidate.FullName "browser"
$spaOut = if (Test-Path $browserPath) { $browserPath } else { $distCandidate.FullName }

$wwwroot = Join-Path $outDir "wwwroot"
if (Test-Path $wwwroot) {
    Remove-Item $wwwroot -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $wwwroot | Out-Null
Copy-Item (Join-Path $spaOut "*") $wwwroot -Recurse -Force

# 3b) Sync local WebLLM model files (large; excluded from ng production assets copy)
$aiSource = Join-Path $repoRoot "Source\Run\SPA\src\assets\ai"
$aiTarget = Join-Path $wwwroot "assets\ai"
if (Test-Path $aiSource) {
    New-Item -ItemType Directory -Force -Path $aiTarget | Out-Null
    robocopy $aiSource $aiTarget /MIR /R:2 /W:2 /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Failed to copy WebLLM assets to $aiTarget" }
}

# 4) Never overwrite the live database during deployment
Restore-DatabaseFiles -Directory $outDir -Backup $dbBackup

Write-Host "Deploy concluido em: $outDir" -ForegroundColor Green

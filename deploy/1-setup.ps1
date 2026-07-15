#Requires -Version 5.1
<#
    WealthLedger - Setup  (step 1 of 2)
    ------------------------------------
    One command that gets everything ready:

      1. Checks that the tools you need are installed (.NET + Node.js).
      2. Downloads the local AI model (runs 100% in your browser).
      3. Builds the app and publishes it into the "publish" folder.

    Everything is written INSIDE this project folder (the "publish" folder and
    the AI model). Nothing is installed on C:\ or anywhere else on your machine.

    Usage:
      Right-click this file  ->  "Run with PowerShell"
      or from a terminal:      ./deploy/1-setup.ps1

    Options:
      -SkipModel   Skip the (large, ~2 GB) AI model download.
      -Force       Re-download the AI model even if it is already present.
#>
param(
    [switch]$SkipModel,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

# --- Paths: everything is relative to the project root ------------------------
$repoRoot   = Split-Path -Parent $PSScriptRoot
$spaDir     = Join-Path $repoRoot 'Source\Run\SPA'
$apiProj    = Join-Path $repoRoot 'Source\Run\WebApp\WealthLedger.WebApp.csproj'
$publishDir = Join-Path $repoRoot 'publish'
$aiAssets   = Join-Path $spaDir  'src\assets\ai'

$dbFiles = @('wealthledger.db', 'wealthledger.db-shm', 'wealthledger.db-wal')

# --- Pretty output helpers ----------------------------------------------------
function Write-Title { param([string]$Text) Write-Host "`n=== $Text ===" -ForegroundColor Cyan }
function Write-Ok    { param([string]$Text) Write-Host "  [ok] $Text"   -ForegroundColor Green }
function Write-Info  { param([string]$Text) Write-Host "  $Text"        -ForegroundColor Gray }
function Write-Fail  { param([string]$Text) Write-Host "`n  [X] $Text`n" -ForegroundColor Red }

# =============================================================================
# 1) Check prerequisites
# =============================================================================
Write-Title 'Checking prerequisites'

function Test-Tool {
    param([string]$Command, [string]$FriendlyName, [string]$InstallUrl)
    $tool = Get-Command $Command -ErrorAction SilentlyContinue
    if (-not $tool) {
        Write-Fail "$FriendlyName is not installed. Get it at: $InstallUrl"
        return $false
    }
    $version = (& $Command --version 2>$null | Select-Object -First 1)
    Write-Ok "$FriendlyName found ($version)"
    return $true
}

$ok = $true
$ok = (Test-Tool 'dotnet' '.NET SDK 10' 'https://dotnet.microsoft.com/download') -and $ok
$ok = (Test-Tool 'node'   'Node.js 20+' 'https://nodejs.org/')                    -and $ok
$ok = (Test-Tool 'npm'    'npm'         'https://nodejs.org/')                    -and $ok

if (-not $ok) {
    Write-Fail 'Please install the missing tool(s) above, then run this script again.'
    exit 1
}

# =============================================================================
# 2) Download the local AI model (into the project folder)
# =============================================================================
function Get-WebLlmModel {
    param([switch]$ForceDownload)

    $modelRepo        = 'https://huggingface.co/mlc-ai/Qwen3-4B-q4f32_1-MLC/resolve/main'
    $modelId          = 'Qwen3-4B-q4f32_1-MLC'
    $modelLibFileName = 'Qwen3-4B-q4f32_1_cs1k-webgpu.wasm'
    $modelLibUrl      = 'https://raw.githubusercontent.com/mlc-ai/binary-mlc-llm-libs/main/web-llm-models/v0_2_84/base/Qwen3-4B-q4f32_1_cs1k-webgpu.wasm'

    $modelDir = Join-Path $aiAssets 'models\qwen3-4b\resolve\main'
    $libDir   = Join-Path $aiAssets 'models\lib'
    New-Item -ItemType Directory -Force -Path $modelDir, $libDir | Out-Null

    function Get-File {
        param([string]$Url, [string]$Destination)
        if ((Test-Path -LiteralPath $Destination) -and -not $ForceDownload) {
            Write-Info "skip (already downloaded): $(Split-Path $Destination -Leaf)"
            return
        }
        Write-Info "downloading: $(Split-Path $Destination -Leaf)"
        Invoke-WebRequest -Uri $Url -OutFile $Destination
    }

    # Manifest / tokenizer files
    foreach ($file in @('mlc-chat-config.json', 'ndarray-cache.json', 'tokenizer.json', 'tokenizer_config.json')) {
        Get-File -Url "$modelRepo/$file" -Destination (Join-Path $modelDir $file)
    }

    # WebLLM 0.2.x reads tensor-cache.json (identical content to ndarray-cache.json)
    $ndarrayCachePath = Join-Path $modelDir 'ndarray-cache.json'
    $tensorCachePath  = Join-Path $modelDir 'tensor-cache.json'
    if (-not (Test-Path -LiteralPath $tensorCachePath) -or $ForceDownload) {
        Copy-Item -LiteralPath $ndarrayCachePath -Destination $tensorCachePath -Force
    }

    # Parameter shards listed inside ndarray-cache.json
    $cache   = Get-Content -LiteralPath $ndarrayCachePath -Raw | ConvertFrom-Json
    $records = if ($cache.records) { $cache.records }
               elseif ($cache.metadata.records) { $cache.metadata.records }
               elseif ($cache.params) { $cache.params }
               else { @() }

    $shards = $records |
        ForEach-Object { if ($_.dataPath) { $_.dataPath } elseif ($_.data_path) { $_.data_path } elseif ($_.path) { $_.path } } |
        Where-Object { $_ } | Sort-Object -Unique
    if (-not $shards) { throw 'Could not resolve model shards from ndarray-cache.json' }

    Write-Info "downloading $($shards.Count) model weight files (this is the large part)..."
    foreach ($dataPath in $shards) {
        $fileName = Split-Path $dataPath -Leaf
        Get-File -Url "$modelRepo/$fileName" -Destination (Join-Path $modelDir $fileName)
    }

    # WebGPU runtime library
    Get-File -Url $modelLibUrl -Destination (Join-Path $libDir $modelLibFileName)
    Get-ChildItem $libDir -Filter '*.wasm' -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ne $modelLibFileName } | Remove-Item -Force

    # Config the SPA reads at runtime
    $config = [ordered]@{
        model_list = @(
            [ordered]@{
                model     = '/assets/ai/models/qwen3-4b/'
                model_id  = $modelId
                model_lib = "/assets/ai/models/lib/$modelLibFileName"
            }
        )
    }
    $config | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $aiAssets 'webllm-model-config.json') -Encoding UTF8
}

Write-Title 'Local AI model'
if ($SkipModel) {
    Write-Info 'Skipped (-SkipModel). The Analytics AI insights feature will be unavailable.'
} else {
    Write-Info 'Downloading into the project folder (Source\Run\SPA\src\assets\ai).'
    Write-Info 'First run downloads ~2 GB and can take a while - grab a coffee.'
    Get-WebLlmModel -ForceDownload:$Force
    Write-Ok 'AI model ready.'
}

# =============================================================================
# 3) Build & publish into the "publish" folder
# =============================================================================
Write-Title 'Building the app'

# Keep any existing database safe across re-publishes.
$dbBackup = @{}
foreach ($dbFile in $dbFiles) {
    $dbPath = Join-Path $publishDir $dbFile
    if (Test-Path $dbPath) { $dbBackup[$dbFile] = [IO.File]::ReadAllBytes($dbPath) }
}

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

Write-Info 'Publishing the API (.NET, Release)...'
dotnet publish $apiProj -c Release -o $publishDir | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Fail 'dotnet publish failed.'; exit 1 }
Write-Ok 'API published.'

Write-Info 'Installing SPA dependencies (npm ci)...'
Push-Location $spaDir
npm ci
if ($LASTEXITCODE -ne 0) { Pop-Location; Write-Fail 'npm ci failed.'; exit 1 }

Write-Info 'Building the SPA (production)...'
npm run build -- --configuration production
if ($LASTEXITCODE -ne 0) { Pop-Location; Write-Fail 'SPA build failed.'; exit 1 }
Pop-Location
Write-Ok 'SPA built.'

# Copy SPA build output into wwwroot
$distRoot      = Join-Path $spaDir 'dist'
$distCandidate = Get-ChildItem $distRoot -Directory -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $distCandidate) { Write-Fail "No SPA build output found in $distRoot."; exit 1 }

$browserPath = Join-Path $distCandidate.FullName 'browser'
$spaOut      = if (Test-Path $browserPath) { $browserPath } else { $distCandidate.FullName }

$wwwroot = Join-Path $publishDir 'wwwroot'
if (Test-Path $wwwroot) { Remove-Item $wwwroot -Recurse -Force }
New-Item -ItemType Directory -Force -Path $wwwroot | Out-Null
Copy-Item (Join-Path $spaOut '*') $wwwroot -Recurse -Force

# Copy the (large) AI model into wwwroot so the published app can serve it
if (Test-Path $aiAssets) {
    $aiTarget = Join-Path $wwwroot 'assets\ai'
    New-Item -ItemType Directory -Force -Path $aiTarget | Out-Null
    robocopy $aiAssets $aiTarget /MIR /R:2 /W:2 /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
    if ($LASTEXITCODE -ge 8) { Write-Fail "Failed to copy AI model into $aiTarget."; exit 1 }
}

# Restore the saved database - a re-publish never overwrites your data.
foreach ($dbFile in $dbFiles) {
    $dbPath = Join-Path $publishDir $dbFile
    if ($dbBackup.ContainsKey($dbFile)) {
        [IO.File]::WriteAllBytes($dbPath, $dbBackup[$dbFile])
    } elseif (Test-Path $dbPath) {
        Remove-Item $dbPath -Force -ErrorAction SilentlyContinue
    }
}

Write-Title 'Done!'
Write-Host "  The app was published to: $publishDir" -ForegroundColor Green
Write-Host "  Next step: run  ./deploy/2-run.ps1`n" -ForegroundColor Green

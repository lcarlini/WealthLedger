param(
    [string]$RepoRoot = 'C:\git\WealthLedger',
    [string]$ModelRepo = 'https://huggingface.co/mlc-ai/Qwen3-4B-q4f32_1-MLC/resolve/main',
    [string]$ModelId = 'Qwen3-4B-q4f32_1-MLC',
    # Must match @mlc-ai/web-llm npm modelVersion (see node_modules/@mlc-ai/web-llm lib/config.d.ts).
    [string]$WebLlmLibVersion = 'v0_2_84/base',
    [string]$ModelLibFileName = 'Qwen3-4B-q4f32_1_cs1k-webgpu.wasm',
    [string]$ModelLibUrl = 'https://raw.githubusercontent.com/mlc-ai/binary-mlc-llm-libs/main/web-llm-models/v0_2_84/base/Qwen3-4B-q4f32_1_cs1k-webgpu.wasm',
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

function Write-Step {
    param([string]$Message)
    Write-Host "[WealthLedger][WebLLM] $Message" -ForegroundColor Cyan
}

function Ensure-Directory {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

function Download-File {
    param(
        [string]$Url,
        [string]$Destination,
        [switch]$ForceDownload
    )

    if ((Test-Path -LiteralPath $Destination) -and -not $ForceDownload) {
        Write-Host "  - Skipping existing: $Destination" -ForegroundColor DarkGray
        return
    }

    Write-Host "  - Downloading: $Url" -ForegroundColor Yellow
    Invoke-WebRequest -Uri $Url -OutFile $Destination
}

if (-not (Test-Path -LiteralPath $RepoRoot)) {
    throw "RepoRoot not found: $RepoRoot"
}

$spaAssetsRoot = Join-Path $RepoRoot 'Source\Run\SPA\src\assets\ai'
$modelRoot = Join-Path $spaAssetsRoot 'models'
$modelDir = Join-Path $modelRoot 'qwen3-4b\resolve\main'
$libDir = Join-Path $modelRoot 'lib'

Ensure-Directory $spaAssetsRoot
Ensure-Directory $modelRoot
Ensure-Directory $modelDir
Ensure-Directory $libDir

Write-Step "Downloading model manifest files to $modelDir"

$manifestFiles = @(
    'mlc-chat-config.json',
    'ndarray-cache.json',
    'tokenizer.json',
    'tokenizer_config.json'
)

foreach ($file in $manifestFiles) {
    $url = "$ModelRepo/$file"
    $destination = Join-Path $modelDir $file
    Download-File -Url $url -Destination $destination -ForceDownload:$Force
}

# WebLLM 0.2.x reads tensor-cache.json (same content as legacy ndarray-cache.json on HF).
$tensorCachePath = Join-Path $modelDir 'tensor-cache.json'
$ndarrayCachePath = Join-Path $modelDir 'ndarray-cache.json'
if (-not (Test-Path -LiteralPath $tensorCachePath) -or $Force) {
    if (-not (Test-Path -LiteralPath $ndarrayCachePath)) {
        throw "Missing ndarray-cache.json at $ndarrayCachePath"
    }
    Copy-Item -LiteralPath $ndarrayCachePath -Destination $tensorCachePath -Force
    Write-Host "  - Wrote tensor-cache.json (copy of ndarray-cache.json)" -ForegroundColor Yellow
}

Write-Step 'Downloading parameter shards listed in ndarray-cache.json'
$ndarrayCache = Get-Content -LiteralPath $ndarrayCachePath -Raw | ConvertFrom-Json

$records = @()
if ($ndarrayCache.records) {
    $records = $ndarrayCache.records
} elseif ($ndarrayCache.metadata -and $ndarrayCache.metadata.records) {
    $records = $ndarrayCache.metadata.records
} elseif ($ndarrayCache.params) {
    $records = $ndarrayCache.params
}

if (-not $records -or $records.Count -eq 0) {
    throw 'Could not find parameter shard records inside ndarray-cache.json'
}

$uniqueDataPaths = $records |
    ForEach-Object {
        if ($_.dataPath) { $_.dataPath }
        elseif ($_.data_path) { $_.data_path }
        elseif ($_.path) { $_.path }
    } |
    Where-Object { $_ } |
    Sort-Object -Unique

if (-not $uniqueDataPaths -or $uniqueDataPaths.Count -eq 0) {
    throw 'Could not resolve shard filenames from ndarray-cache.json'
}

foreach ($dataPath in $uniqueDataPaths) {
    $fileName = Split-Path $dataPath -Leaf
    $url = "$ModelRepo/$fileName"
    $destination = Join-Path $modelDir $fileName
    Download-File -Url $url -Destination $destination -ForceDownload:$Force
}

Write-Step "Downloading WebGPU model lib to $libDir (WebLLM $WebLlmLibVersion)"
$modelLibDestination = Join-Path $libDir $ModelLibFileName
Download-File -Url $ModelLibUrl -Destination $modelLibDestination -ForceDownload:$Force

# Remove legacy wasm from older download scripts.
Get-ChildItem $libDir -Filter '*.wasm' -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -ne $ModelLibFileName } |
    Remove-Item -Force

Write-Step 'Writing Angular/WebLLM config example'
$configPath = Join-Path $spaAssetsRoot 'webllm-model-config.json'
$config = [ordered]@{
    model_list = @(
        [ordered]@{
            model = '/assets/ai/models/qwen3-4b/'
            model_id = $ModelId
            model_lib = "/assets/ai/models/lib/$modelLibFileName"
        }
    )
}
$config | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $configPath -Encoding UTF8

Write-Step 'Done.'
Write-Host ''
Write-Host 'Files downloaded to:' -ForegroundColor Green
Write-Host "  $modelDir"
Write-Host "  $modelLibDestination"
Write-Host "  $configPath"
Write-Host ''
Write-Host 'Suggested next step in Angular:' -ForegroundColor Green
Write-Host '  Load /assets/ai/webllm-model-config.json and pass it as appConfig to CreateWebWorkerMLCEngine.'

param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$Repo = 'sokol6020/custom_stt',

    [string]$Token = $env:GITHUB_TOKEN
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Token)) {
    throw 'GITHUB_TOKEN is not set. Create a token with repo scope and run: set GITHUB_TOKEN=ghp_...'
}

$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$ReleaseDir = Join-Path $Root "releases\v$Version"
$Tag = "v$Version"

if (-not (Test-Path -LiteralPath $ReleaseDir)) {
    throw "Release folder not found: $ReleaseDir. Run create-release.bat $Version first."
}

$notesPath = Join-Path $ReleaseDir 'RELEASE_NOTES.txt'
$notes = if (Test-Path $notesPath) { Get-Content -LiteralPath $notesPath -Raw } else { "Release $Version" }

$headers = @{
    Authorization = "Bearer $Token"
    Accept        = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = '2022-11-28'
}

$releasePayload = @{
    tag_name = $Tag
    name     = "customSTT $Tag"
    body     = $notes
    draft    = $false
} | ConvertTo-Json

try {
    $existing = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/tags/$Tag" -Headers $headers -Method Get
} catch {
    $existing = $null
}

if ($null -ne $existing -and $existing.id) {
    Write-Host "Deleting existing GitHub release for $Tag..."
    Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/$($existing.id)" -Headers $headers -Method Delete
}

Write-Host "Creating GitHub release $Tag..."
$release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases" -Headers $headers -Method Post -Body $releasePayload -ContentType 'application/json; charset=utf-8'

function Upload-Asset {
    param([string]$FilePath)
    $name = [Uri]::EscapeDataString((Split-Path -Leaf $FilePath))
    $uploadUrl = "$($release.upload_url -replace '\{\?name,label\}$','')?name=$name"
    $bytes = [System.IO.File]::ReadAllBytes($FilePath)
    Invoke-RestMethod -Uri $uploadUrl -Headers @{
        Authorization = "Bearer $Token"
        Accept        = 'application/vnd.github+json'
    } -Method Post -Body $bytes -ContentType 'application/octet-stream' | Out-Null
    Write-Host "Uploaded: $name"
}

$exe = Join-Path $ReleaseDir 'customSTT.exe'
$zip = Get-ChildItem -Path $ReleaseDir -Filter "customSTT-$Version-win-x64.zip" | Select-Object -First 1

Upload-Asset $exe
if ($zip) { Upload-Asset $zip.FullName }

Write-Host "GitHub release published: $($release.html_url)"

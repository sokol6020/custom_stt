param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$Repo,

    [string]$Token
)

$ErrorActionPreference = 'Stop'

& (Join-Path $PSScriptRoot 'load-release-config.ps1') | Out-Null

if ([string]::IsNullOrWhiteSpace($Token)) {
    $Token = $env:GITHUB_TOKEN
}

if ([string]::IsNullOrWhiteSpace($Repo)) {
    $Repo = if ($env:GITHUB_REPO) { $env:GITHUB_REPO } else { 'sokol6020/custom_stt' }
}

if ([string]::IsNullOrWhiteSpace($Token)) {
    Write-Host 'GITHUB_TOKEN not found. Copy config\release.env.example to config\release.env'
    exit 2
}

$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$ReleaseDir = Join-Path $Root "releases\v$Version"
$Tag = "v$Version"

if (-not (Test-Path -LiteralPath $ReleaseDir)) {
    throw "Release folder not found: $ReleaseDir. Run create-release.bat $Version first."
}

$notesPath = Join-Path $ReleaseDir 'RELEASE_NOTES.txt'
if (Test-Path -LiteralPath $notesPath) {
    $notes = [System.IO.File]::ReadAllText($notesPath, [System.Text.UTF8Encoding]::new($false))
} else {
    $notes = "Release $Version"
}

$releasePayloadJson = (@{
    tag_name = $Tag
    name     = "customSTT $Tag"
    body     = $notes
    draft    = $false
} | ConvertTo-Json -Compress)

$headers = @{
    Authorization = "Bearer $Token"
    Accept        = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = '2022-11-28'
}

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
$release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases" -Headers $headers -Method Post -Body $releasePayloadJson -ContentType 'application/json; charset=utf-8'

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

$zip = Get-ChildItem -Path $ReleaseDir -Filter "customSTT-$Version-win-x64.zip" | Select-Object -First 1

if (-not $zip) {
    throw "Zip not found in $ReleaseDir"
}

Upload-Asset $zip.FullName

Write-Host "GitHub release published: $($release.html_url)"

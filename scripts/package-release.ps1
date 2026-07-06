param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$PublishDir,

    [Parameter(Mandatory = $true)]
    [string]$ReleaseDir,

    [Parameter(Mandatory = $true)]
    [string]$NotesFile
)

$ErrorActionPreference = 'Stop'

$exeSource = Join-Path $PublishDir 'customSTT.exe'
if (-not (Test-Path -LiteralPath $exeSource)) {
    throw "Not found: $exeSource"
}

New-Item -ItemType Directory -Force -Path $ReleaseDir | Out-Null

$exeTarget = Join-Path $ReleaseDir 'customSTT.exe'
Copy-Item -LiteralPath $exeSource -Destination $exeTarget -Force

$zipName = "customSTT-$Version-win-x64.zip"
$zipPath = Join-Path $ReleaseDir $zipName
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

$staging = Join-Path $env:TEMP "customSTT-release-$Version"
if (Test-Path -LiteralPath $staging) {
    Remove-Item -LiteralPath $staging -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $staging | Out-Null
Copy-Item -Path (Join-Path $PublishDir '*') -Destination $staging -Recurse -Force
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zipPath -Force
Remove-Item -LiteralPath $staging -Recurse -Force

$notesTarget = Join-Path $ReleaseDir 'RELEASE_NOTES.txt'
if ($NotesFile -ne $notesTarget) {
    Copy-Item -LiteralPath $NotesFile -Destination $notesTarget -Force
}

$builtAt = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
$meta = @"
customSTT $Version
Built: $builtAt
Platform: win-x64 (self-contained)

Files:
  - customSTT.exe
  - $zipName

"@
$meta | Set-Content -LiteralPath (Join-Path $ReleaseDir 'BUILD_INFO.txt') -Encoding UTF8

Write-Host "Release packaged: $ReleaseDir"
Write-Host "  $exeTarget"
Write-Host "  $zipPath"

param(
    [switch]$NoPause
)

$ErrorActionPreference = 'Stop'

$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$AppDir = Join-Path $Root 'customSTT'
$Project = Join-Path $AppDir 'customSTT.csproj'
$BinDir = Join-Path $AppDir 'bin'
$ObjDir = Join-Path $AppDir 'obj'
$IconTool = Join-Path $Root 'tools\GenerateAppIcon\GenerateAppIcon.csproj'
$IconOut = Join-Path $AppDir 'Assets\app.ico'
$Output = Join-Path $Root 'publish'
$Exe = Join-Path $Output 'customSTT.exe'
$Dll = Join-Path $Output 'customSTT.dll'

function Invoke-Dotnet {
    param([string[]]$DotnetArgs)
    & dotnet @DotnetArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet failed: dotnet $($DotnetArgs -join ' ')"
    }
}

Write-Host '=== customSTT: prod build ==='
Write-Host ''

Write-Host 'Stopping running instances...'
for ($i = 0; $i -lt 15; $i++) {
    Get-Process -Name customSTT -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
    if (-not (Get-Process -Name customSTT -ErrorAction SilentlyContinue)) {
        break
    }
    if ($i -eq 14) {
        throw 'Could not stop customSTT.exe'
    }
}

Write-Host 'Cleaning bin, obj and publish...'
foreach ($dir in @($Output, $BinDir, $ObjDir)) {
    if (Test-Path -LiteralPath $dir) {
        Remove-Item -LiteralPath $dir -Recurse -Force
    }
}

if (Test-Path -LiteralPath $Output) {
    throw 'Publish folder is locked. Close customSTT.exe and retry.'
}

Invoke-Dotnet @('clean', $Project, '-c', 'Release', '-r', 'win-x64', '--nologo')

Write-Host 'Generating icon...'
try {
    Invoke-Dotnet @('run', '--project', $IconTool, '-c', 'Release', '--no-build', '--', $IconOut)
} catch {
    Invoke-Dotnet @('run', '--project', $IconTool, '-c', 'Release', '--', $IconOut)
}

Write-Host 'Restoring packages...'
Invoke-Dotnet @('restore', $Project, '--runtime', 'win-x64', '--force')

Write-Host 'Publishing Release win-x64...'
Invoke-Dotnet @(
    'publish', $Project,
    '--configuration', 'Release',
    '--runtime', 'win-x64',
    '--self-contained', 'true',
    '--no-restore',
    '-p:ContinuousIntegrationBuild=true',
    '-p:PublishReadyToRun=false',
    '-p:DebugType=none',
    '-p:DebugSymbols=false',
    '--output', $Output
)

if (-not (Test-Path -LiteralPath $Exe)) {
    throw "Not found: $Exe"
}

$appVersion = (Select-Xml -Path $Project -XPath '//Version').Node.InnerText
$builtVersion = [System.Reflection.AssemblyName]::GetAssemblyName($Dll).Version.ToString(3)

Write-Host ''
Write-Host '=== DONE ==='
Write-Host "Project version: $appVersion"
Write-Host "Built version:   $builtVersion"
Write-Host "Run: $Exe"
Write-Host "Exe size: $((Get-Item $Exe).Length) bytes, modified: $((Get-Item $Exe).LastWriteTime)"
Write-Host "Dll size: $((Get-Item $Dll).Length) bytes, modified: $((Get-Item $Dll).LastWriteTime)"
Write-Host ''

if ($appVersion -ne $builtVersion) {
    Write-Warning 'Project version and built DLL version do not match.'
}

if (-not $NoPause -and $Host.Name -eq 'ConsoleHost') {
    Read-Host 'Press Enter to continue'
}

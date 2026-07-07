param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDir,

    [string]$SubFolder = 'lib'
)

# Reorganizes a self-contained .NET publish folder so that customSTT.exe is not
# lost among hundreds of files. The .NET host resolves deps.json runtime asset
# paths relative to the app base dir ONLY for framework ("runtimepack")
# assemblies; for NuGet "package"/"project" assemblies the relative path is
# ignored and the assembly is resolved by file name in the app base dir
# (see dotnet/runtime#3525). Therefore:
#
#   * Framework runtimepack managed assemblies + WPF satellite locale folders
#     are moved into <SubFolder>, and their deps.json asset paths are rewritten
#     to point into <SubFolder> (the host honors this for runtimepack).
#   * NuGet package/app managed assemblies stay at the root (the host can only
#     find them there).
#   * All native DLLs (coreclr, clrjit, hostfxr, hostpolicy, wpfgfx_cor3, the
#     apphost, ...), customSTT.dll, the *.json config files and runtimes\ stay
#     at the root so the app starts and native resolution keeps working.
#
# AppContext.BaseDirectory stays at the root (customSTT.dll is not moved), so
# Data\ and whisper-models\ created by the app at runtime are unaffected.

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $PublishDir)) {
    throw "Publish folder not found: $PublishDir"
}

$PublishDir = (Resolve-Path -LiteralPath $PublishDir).Path
$libDir = Join-Path $PublishDir $SubFolder
$depsPath = Join-Path $PublishDir 'customSTT.deps.json'
if (-not (Test-Path -LiteralPath $depsPath)) {
    throw "deps.json not found: $depsPath"
}

function Get-Leaf {
    param([string]$Key)
    return ($Key -split '[\\/]')[-1]
}

Write-Host "Tidying publish folder: $PublishDir"
Write-Host "  Subfolder: $SubFolder"

# --- 1) Read deps.json and collect framework (runtimepack) assets -----------
$depsRaw = Get-Content -LiteralPath $depsPath -Raw -Encoding UTF8
$deps = $depsRaw | ConvertFrom-Json

# In deps.json the library "type" (runtimepack / package / project) lives in the
# top-level "libraries" section, keyed by "<name>/<version>". The per-target
# library objects do not carry it, so build a lookup map.
$libTypes = @{}
if ($deps.libraries) {
    foreach ($libProp in $deps.libraries.PSObject.Properties) {
        $libTypes[$libProp.Name] = [string]$libProp.Value.type
    }
}

$frameworkManaged = New-Object System.Collections.Generic.HashSet[string]   # filenames

foreach ($targetProp in $deps.targets.PSObject.Properties) {
    foreach ($libProp in $targetProp.Value.PSObject.Properties) {
        $lib = $libProp.Value
        $libType = if ($libTypes.ContainsKey($libProp.Name)) { $libTypes[$libProp.Name] } else { '' }
        if ($libType -ne 'runtimepack') { continue }

        $runtime = $lib.PSObject.Properties['runtime']
        if ($runtime -and $runtime.Value) {
            foreach ($a in $runtime.Value.PSObject.Properties) {
                [void]$frameworkManaged.Add((Get-Leaf $a.Name))
            }
        }
    }
}

# --- 2) Move framework managed DLLs into the subfolder ----------------------
New-Item -ItemType Directory -Force -Path $libDir | Out-Null

$movedManagedNames = New-Object System.Collections.Generic.HashSet[string]
foreach ($file in (Get-ChildItem -LiteralPath $PublishDir -File -Filter '*.dll')) {
    if (-not $frameworkManaged.Contains($file.Name)) { continue }
    Move-Item -LiteralPath $file.FullName -Destination (Join-Path $libDir $file.Name) -Force
    [void]$movedManagedNames.Add($file.Name)
}
Write-Host ("  Moved framework managed DLLs: " + $movedManagedNames.Count)

# --- 3) Move satellite locale folders into the subfolder --------------------
# Satellite resource assemblies (WPF framework localizations) are not listed in
# deps.json; they are resolved lazily by the ResourceManager via probing. We
# move any top-level folder that contains *.resources.dll and register the
# subfolder in runtimeconfig.json additionalProbingPaths so they still resolve
# (as lib\<culture>\Name.resources.dll). This only affects lazy satellite
# probing and does not disturb TPA-resolved framework/package assemblies.
$keepDirs = @('Assets', 'runtimes', $SubFolder)
$movedCultureDirs = New-Object System.Collections.Generic.HashSet[string]
foreach ($dir in (Get-ChildItem -LiteralPath $PublishDir -Directory)) {
    if ($keepDirs -contains $dir.Name) { continue }

    $hasResources = Get-ChildItem -LiteralPath $dir.FullName -File -Filter '*.resources.dll' -ErrorAction SilentlyContinue
    if (-not $hasResources) { continue }

    Move-Item -LiteralPath $dir.FullName -Destination (Join-Path $libDir $dir.Name) -Force
    [void]$movedCultureDirs.Add($dir.Name)
}
Write-Host ("  Moved locale folders: " + $movedCultureDirs.Count)

# Register the subfolder for satellite resource probing.
if ($movedCultureDirs.Count -gt 0) {
    $rcPath = Join-Path $PublishDir 'customSTT.runtimeconfig.json'
    if (Test-Path -LiteralPath $rcPath) {
        $rc = Get-Content -LiteralPath $rcPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if (-not $rc.runtimeOptions.PSObject.Properties['additionalProbingPaths']) {
            $rc.runtimeOptions | Add-Member -NotePropertyName 'additionalProbingPaths' -NotePropertyValue @($SubFolder)
        } else {
            $existing = @($rc.runtimeOptions.additionalProbingPaths)
            if ($existing -notcontains $SubFolder) {
                $rc.runtimeOptions.additionalProbingPaths = @($existing + $SubFolder)
            }
        }
        $rcJson = $rc | ConvertTo-Json -Depth 100
        [System.IO.File]::WriteAllText($rcPath, $rcJson, [System.Text.UTF8Encoding]::new($false))
        Write-Host "  Added additionalProbingPaths -> $SubFolder in runtimeconfig.json"
    }
}

# --- 4) Rewrite deps.json asset paths for runtimepack libraries -------------
$prefix = "$SubFolder/"

function Convert-RuntimeLike {
    param([object]$AssetGroup)
    if ($null -eq $AssetGroup) { return $null }
    $ordered = [ordered]@{}
    foreach ($prop in $AssetGroup.PSObject.Properties) {
        $leaf = Get-Leaf $prop.Name
        $newKey = $prop.Name
        if ($movedManagedNames.Contains($leaf)) { $newKey = "$prefix$leaf" }
        $ordered[$newKey] = $prop.Value
    }
    return $ordered
}

function Convert-Resources {
    param([object]$AssetGroup)
    if ($null -eq $AssetGroup) { return $null }
    $ordered = [ordered]@{}
    foreach ($prop in $AssetGroup.PSObject.Properties) {
        $leaf = Get-Leaf $prop.Name
        $culture = $null
        if ($prop.Value -and $prop.Value.PSObject.Properties['locale']) {
            $culture = [string]$prop.Value.locale
        }
        if ([string]::IsNullOrEmpty($culture)) {
            $parts = $prop.Name -split '[\\/]'
            if ($parts.Length -ge 2) { $culture = $parts[$parts.Length - 2] }
        }
        $newKey = $prop.Name
        if ($culture -and $movedCultureDirs.Contains($culture)) {
            $newKey = "$prefix$culture/$leaf"
        }
        $ordered[$newKey] = $prop.Value
    }
    return $ordered
}

foreach ($targetProp in $deps.targets.PSObject.Properties) {
    foreach ($libProp in $targetProp.Value.PSObject.Properties) {
        $lib = $libProp.Value
        $libType = if ($libTypes.ContainsKey($libProp.Name)) { $libTypes[$libProp.Name] } else { '' }
        if ($libType -ne 'runtimepack') { continue }

        foreach ($sectionName in @('runtime', 'runtimeTargets')) {
            $section = $lib.PSObject.Properties[$sectionName]
            if ($section) {
                $converted = Convert-RuntimeLike -AssetGroup $section.Value
                if ($converted) {
                    $lib.PSObject.Properties.Remove($sectionName)
                    $lib | Add-Member -NotePropertyName $sectionName -NotePropertyValue ([pscustomobject]$converted)
                }
            }
        }

        $resources = $lib.PSObject.Properties['resources']
        if ($resources) {
            $converted = Convert-Resources -AssetGroup $resources.Value
            if ($converted) {
                $lib.PSObject.Properties.Remove('resources')
                $lib | Add-Member -NotePropertyName 'resources' -NotePropertyValue ([pscustomobject]$converted)
            }
        }
    }
}

$json = $deps | ConvertTo-Json -Depth 100
[System.IO.File]::WriteAllText($depsPath, $json, [System.Text.UTF8Encoding]::new($false))

Write-Host "  Rewrote deps.json framework asset paths -> $SubFolder/"
Write-Host "Tidy layout complete. Root keeps the apphost, native/host files, NuGet package DLLs, runtimes\, Assets\ and $SubFolder\."

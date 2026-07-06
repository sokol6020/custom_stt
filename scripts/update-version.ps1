param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$ProjectPath
)

$assemblyVersion = if ($Version -match '^\d+\.\d+\.\d+$') { "$Version.0" } else { $Version }

$content = Get-Content -LiteralPath $ProjectPath -Raw
$content = $content -replace '<Version>[^<]+</Version>', "<Version>$Version</Version>"
$content = $content -replace '<AssemblyVersion>[^<]+</AssemblyVersion>', "<AssemblyVersion>$assemblyVersion</AssemblyVersion>"
$content = $content -replace '<FileVersion>[^<]+</FileVersion>', "<FileVersion>$assemblyVersion</FileVersion>"

$utf8Bom = New-Object System.Text.UTF8Encoding $true
[System.IO.File]::WriteAllText($ProjectPath, $content, $utf8Bom)

Write-Host "Version updated to $Version ($assemblyVersion)"

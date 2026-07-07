param(
    [string]$ConfigPath
)

$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$path = if ($ConfigPath) { $ConfigPath } else { Join-Path $Root 'config\release.env' }

if (-not (Test-Path -LiteralPath $path)) {
    return $false
}

Get-Content -LiteralPath $path -Encoding UTF8 | ForEach-Object {
    $line = $_.Trim()
    if ($line -eq '' -or $line.StartsWith('#')) {
        return
    }

    $eq = $line.IndexOf('=')
    if ($eq -lt 1) {
        return
    }

    $name = $line.Substring(0, $eq).Trim()
    $value = $line.Substring($eq + 1).Trim().Trim('"').Trim("'")

    if ($value -eq '') {
        return
    }

    Set-Item -Path "Env:$name" -Value $value
}

return $true

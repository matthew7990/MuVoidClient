# Genera version.json en el directorio del cliente compilado.
# Llamado automaticamente por compile-client.bat.
# Uso: .\generate-client-manifest.ps1 "<ruta_a_Main.exe>"
param(
    [Parameter(Mandatory=$true)]
    [string]$ExePath
)

$ErrorActionPreference = "Stop"

$exeDir  = Split-Path $ExePath -Parent
$repoRoot = Split-Path $PSScriptRoot -Parent

# ── Versión (auto-incrementa build en cada compilación) ──────────────────────
$versionFile = Join-Path $repoRoot "CLIENT_VERSION"
$prevVersion = ""
if (Test-Path $versionFile) {
    $prevVersion = (Get-Content $versionFile -Raw).Trim()
}
# Formato: 1.0.0 o 1.0.0.1 → incrementar último segmento numérico
$parts = $prevVersion -split '\.'
$lastIdx = $parts.Count - 1
$build = 1
if ($lastIdx -ge 0 -and $parts[$lastIdx] -match '^\d+$') {
    $build = [int]$parts[$lastIdx] + 1
    $parts[$lastIdx] = $build.ToString()
    $version = $parts -join '.'
} elseif ($prevVersion) {
    $version = $prevVersion + ".1"
} else {
    $version = "1.0.0.1"
}
[System.IO.File]::WriteAllText($versionFile, $version, (New-Object System.Text.UTF8Encoding $false))
if ($prevVersion -and $prevVersion -ne $version) {
    Write-Host "[*] Version: $prevVersion -> $version" -ForegroundColor Cyan
}

# ── Changelog ────────────────────────────────────────────────────────────────
$changelogFile = Join-Path $repoRoot "CLIENT_CHANGELOG.md"
$changelog = @()
if (Test-Path $changelogFile) {
    $content = Get-Content $changelogFile -Raw -Encoding UTF8
    $inSection = $false
    foreach ($line in ($content -split "`n")) {
        if ($line -match '^##\s+Pr')              { $inSection = $true; continue }
        if ($inSection -and $line -match '^##\s+') { break }
        if ($inSection -and $line -match '^\-\s+(.+)') { $changelog += $matches[1].Trim() }
    }
}

# ── SHA256 de todos los archivos del directorio compilado ─────────────────────
Write-Host "[*] Calculando SHA256 de archivos compilados..." -ForegroundColor Gray
$files = @()
$sha   = [System.Security.Cryptography.SHA256]::Create()
$baseLen = $exeDir.TrimEnd('\').Length + 1

Get-ChildItem $exeDir -File -Recurse | Where-Object { $_.Name -ne "version.json" -and $_.Name -ne "config.ini" } | ForEach-Object {
    $relPath = $_.FullName.Substring($baseLen) -replace "\\", "/"
    $bytes   = [System.IO.File]::ReadAllBytes($_.FullName)
    $hash    = [BitConverter]::ToString($sha.ComputeHash($bytes)) -replace '-', ''
    $files  += [ordered]@{
        path   = $relPath
        sha256 = $hash.ToLower()
        size   = $_.Length
    }
}
$sha.Dispose()

# ── Commit actual (opcional, para trazabilidad) ───────────────────────────────
$commit = ""
try { $commit = (git -C $repoRoot rev-parse --short HEAD 2>$null).Trim() } catch {}

# ── Escribir version.json ─────────────────────────────────────────────────────
$manifest = [ordered]@{
    version   = $version
    date      = (Get-Date -Format "yyyy-MM-dd")
    commit    = $commit
    changelog = $changelog
    files     = $files
}

$outPath = Join-Path $exeDir "version.json"
$json = $manifest | ConvertTo-Json -Depth 4
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($outPath, $json, $utf8NoBom)

# Guardar ruta para build-all.bat (directorio exacto donde compilo)
Set-Content (Join-Path $repoRoot ".client-build-dir") $exeDir -NoNewline

Write-Host "[OK] version.json generado: v$version  |  $($files.Count) archivos  |  $exeDir" -ForegroundColor Green

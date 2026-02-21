# Publica el launcher a launcher-release (mismo sistema que cliente: version.json + delta)
# Requiere: launcher compilado (npm run build en launcher/)
# Uso: .\publish-launcher.ps1

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not (Test-Path "$repoRoot\.git")) { $repoRoot = (Get-Location).Path }
Set-Location $repoRoot

$versionFile = "launcher/VERSION"
$changelogFile = "launcher/LAUNCHER_CHANGELOG.md"

# Buscar el exe del launcher
$possibleOutDirs = @("launcher/src-tauri/target/release", "launcher/target/release")
$outDir = $null
foreach ($p in $possibleOutDirs) {
    if (Test-Path "$repoRoot/$p") {
        $exe = Get-ChildItem "$repoRoot/$p" -Filter "*.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($exe) { $outDir = $p; break }
    }
}
if (-not $outDir) {
    $exe = Get-ChildItem -Path "launcher" -Filter "*.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($exe) { $outDir = $exe.DirectoryName -replace [regex]::Escape($repoRoot), "" -replace "^\\", "" -replace "\\", "/" }
}

Write-Host "`n=== MuVoid - Publicar Launcher ===" -ForegroundColor Cyan

if (-not (Test-Path $versionFile)) { Write-Error "Falta launcher/VERSION" }
$version = (Get-Content $versionFile -Raw).Trim()
if (-not $version -match '^\d+\.\d+\.\d+$') { Write-Error "VERSION debe ser semantica (ej: 1.0.0)" }

if (-not (Test-Path $changelogFile)) { Write-Error "Falta launcher/LAUNCHER_CHANGELOG.md" }
$content = Get-Content $changelogFile -Raw -Encoding UTF8
$changelog = @()
$inSection = $false
foreach ($line in ($content -split "`n")) {
    if ($line -match '^##\s+Pr') { $inSection = $true; continue }
    if ($inSection -and $line -match '^##\s+') { break }
    if ($inSection -and $line -match '^\-\s+(.+)') { $changelog += $matches[1].Trim() }
}
if ($changelog.Count -eq 0) { Write-Error "Anade items en '## Proxima version' de LAUNCHER_CHANGELOG.md" }

$outDirFull = Join-Path $repoRoot ($outDir -replace "/", [System.IO.Path]::DirectorySeparatorChar)
$exeFile = Get-ChildItem -Path $outDirFull -Filter "*.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $exeFile) { Write-Error "No se encontro el .exe del launcher en $outDir" }

$distDir = Join-Path $env:TEMP "muvoid-launcher-dist-$([Guid]::NewGuid().ToString('N').Substring(0,8))"
New-Item -ItemType Directory -Path $distDir -Force | Out-Null

# Copiar exe y dlls (solo lo necesario para el launcher)
Copy-Item "$outDirFull\*.exe" $distDir -Force -ErrorAction SilentlyContinue
Copy-Item "$outDirFull\*.dll" $distDir -Force -ErrorAction SilentlyContinue

# Generar version.json con files array
$files = @()
$sha = [System.Security.Cryptography.SHA256]::Create()
Get-ChildItem $distDir -File | ForEach-Object {
    $relPath = $_.Name
    $bytes = [System.IO.File]::ReadAllBytes($_.FullName)
    $hash = $sha.ComputeHash($bytes)
    $hex = [BitConverter]::ToString($hash) -replace '-',''
    $files += @{ path = $relPath; sha256 = $hex.ToLower(); size = $_.Length }
}
$sha.Dispose()

$commit = (git rev-parse --short HEAD).Trim()
$date = Get-Date -Format "yyyy-MM-dd"
$manifest = [ordered]@{ version = $version; date = $date; commit = $commit; changelog = $changelog; files = $files }
$manifest | ConvertTo-Json -Depth 4 | Set-Content "$distDir/version.json" -Encoding UTF8

Write-Host "[OK] version.json: $version ($($files.Count) archivos)" -ForegroundColor Green

# Quitar lockfiles
@(".git\index.lock", ".git\refs\heads\master.lock") | ForEach-Object {
    if (Test-Path $_) { Remove-Item $_ -Force }
}

# Subir a launcher-release (worktree temporal - NO toca tu directorio)
$worktreePath = Join-Path $env:TEMP "muvoid-launcher-release-worktree"
if (Test-Path $worktreePath) {
    $prevErr = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    git worktree remove -f $worktreePath 2>&1 | Out-Null
    $ErrorActionPreference = $prevErr
    if (Test-Path $worktreePath) { Remove-Item $worktreePath -Recurse -Force }
}

$branchExists = git ls-remote --heads origin launcher-release 2>$null
$prevErr = $ErrorActionPreference
$ErrorActionPreference = 'Continue'

if ($branchExists) {
    git fetch origin launcher-release --depth 1 2>&1 | Out-Null
    git worktree add $worktreePath launcher-release 2>&1 | Out-Null
} else {
    git worktree add -b launcher-release $worktreePath HEAD 2>&1 | Out-Null
}

$ErrorActionPreference = $prevErr

Push-Location $worktreePath
try {
    Get-ChildItem -Force | Where-Object { $_.Name -ne ".git" } | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    Copy-Item "$distDir\*" . -Force -Recurse
    Remove-Item $distDir -Recurse -Force -ErrorAction SilentlyContinue

    $readme = @"
# MuVoid Launcher Distribution

> Publicado localmente. El launcher se actualiza solo (delta por SHA256).

**Version:** $version
**Commit:** $commit
**Date:** $date
"@
    Set-Content "README.md" $readme -Encoding UTF8

    git add --all
    git diff --cached --quiet 2>$null
    $hasChanges = $LASTEXITCODE -ne 0
    if ($hasChanges) {
        git commit -m "chore(launcher): release $version from $commit"
        git push origin launcher-release
        Write-Host "`n[OK] Launcher $version publicado en launcher-release" -ForegroundColor Green
    } else {
        Write-Host "`n[!] No hay cambios" -ForegroundColor Yellow
    }
} finally {
    Pop-Location
    git worktree remove -f $worktreePath 2>&1 | Out-Null
}

Write-Host ""

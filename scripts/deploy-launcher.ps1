# Compila solo el launcher y lo publica en MuVoidClient-Release.
# Uso: .\deploy-launcher.ps1
# No compila el cliente (usa el que ya está).

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$releaseDir = Join-Path (Split-Path $repoRoot -Parent) "MuVoidClient-Release"

if (-not (Test-Path $releaseDir)) {
    Write-Host "ERROR: No existe $releaseDir" -ForegroundColor Red
    Write-Host "MuVoidClient-Release debe estar como carpeta hermana de MuVoidClient" -ForegroundColor Yellow
    exit 1
}

Push-Location $repoRoot

try {
    Write-Host "`n=== MuVoid - Deploy Launcher (solo launcher) ===" -ForegroundColor Cyan

    # 1. Compilar launcher (forzar target en proyecto para que el exe quede en launcher/src-tauri/target/release)
    Write-Host "[1/4] Compilando launcher..." -ForegroundColor Gray
    $launcherTargetDir = Join-Path $repoRoot "launcher\src-tauri\target"
    $env:CARGO_TARGET_DIR = $launcherTargetDir
    Push-Location (Join-Path $repoRoot "launcher")
    try {
        $null = npm run tauri build
        if ($LASTEXITCODE -ne 0) { throw "Fallo compilacion del launcher (exit $LASTEXITCODE)" }
    } finally {
        Remove-Item Env:CARGO_TARGET_DIR -ErrorAction SilentlyContinue
        Pop-Location
    }

    # 2. Buscar el exe (muvoid-launcher.exe o MuVoid Launcher.exe)
    $launcherDir = Join-Path $launcherTargetDir "release"
    $exeFile = Get-ChildItem -Path $launcherDir -Filter "*.exe" -ErrorAction SilentlyContinue | Where-Object { $_.Name -notmatch "\.d\.exe$" } | Select-Object -First 1
    if (-not $exeFile) {
        Write-Host "ERROR: No se encontro el .exe en $launcherDir" -ForegroundColor Red
        Write-Host "Ejecuta 'npm run tauri build' en launcher/ primero" -ForegroundColor Yellow
        exit 1
    }

    $version = (Get-Content (Join-Path $repoRoot "launcher\VERSION") -Raw).Trim()
    $changelogFile = Join-Path $repoRoot "launcher\LAUNCHER_CHANGELOG.md"
    $changelog = @()
    if (Test-Path $changelogFile) {
        $content = Get-Content $changelogFile -Raw -Encoding UTF8
        $inSection = $false
        foreach ($line in ($content -split "`n")) {
            if ($line -match '^##\s+Pr') { $inSection = $true; continue }
            if ($inSection -and $line -match '^##\s+') { break }
            if ($inSection -and $line -match '^\-\s+(.+)') { $changelog += $matches[1].Trim() }
        }
    }
    if ($changelog.Count -eq 0) { $changelog = @("Actualizacion del launcher") }

    # 3. Copiar exe a MuVoidClient-Release y generar launcher_version.json
    Write-Host "[2/4] Copiando a MuVoidClient-Release..." -ForegroundColor Gray
    $exeName = "MuVoid Launcher.exe"
    $destExe = Join-Path $releaseDir $exeName
    Copy-Item $exeFile.FullName $destExe -Force

    $sha = [System.Security.Cryptography.SHA256]::Create()
    $bytes = [System.IO.File]::ReadAllBytes($destExe)
    $hash = $sha.ComputeHash($bytes)
    $hex = ([BitConverter]::ToString($hash) -replace '-','').ToLower()
    $sha.Dispose()

    $manifest = @{
        version = $version
        date = Get-Date -Format "yyyy-MM-dd"
        changelog = $changelog
        files = @(@{
            path = $exeName
            sha256 = $hex
            size = (Get-Item $destExe).Length
        })
    }
    $manifest | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $releaseDir "launcher_version.json") -Encoding UTF8

    Write-Host "[OK] Launcher $version copiado a MuVoidClient-Release" -ForegroundColor Green

    # 4. Git en MuVoidClient (source)
    Write-Host "[3/4] Commit en MuVoidClient..." -ForegroundColor Gray
    git add launcher/index.html launcher/src/main.js launcher/src-tauri/src/http_updater.rs
    git add scripts/deploy-launcher.ps1 scripts/deploy-launcher.bat
    git diff --cached --quiet 2>$null
    if ($LASTEXITCODE -ne 0) {
        git commit -m "fix(launcher): carpeta instalacion Program Files, boton seleccionar, sin mensaje compile-client"
        Write-Host "[OK] Commit en MuVoidClient" -ForegroundColor Green
    } else {
        Write-Host "[!] Sin cambios en MuVoidClient" -ForegroundColor Yellow
    }

    # 5. Git en MuVoidClient-Release
    Write-Host "[4/4] Commit en MuVoidClient-Release..." -ForegroundColor Gray
    Push-Location $releaseDir
    try {
        git add launcher_version.json $exeName 2>$null
        git status --short
        git diff --cached --quiet 2>$null
        if ($LASTEXITCODE -ne 0) {
            git commit -m "chore(launcher): v$version - carpeta Program Files, boton seleccionar"
            Write-Host "[OK] Commit en MuVoidClient-Release" -ForegroundColor Green
            Write-Host "`nPara publicar: git push origin main (en ambos repos)" -ForegroundColor Cyan
        } else {
            Write-Host "[!] Sin cambios en MuVoidClient-Release" -ForegroundColor Yellow
        }
    } finally {
        Pop-Location
    }

    Write-Host "`nListo." -ForegroundColor Green
}
catch {
    Write-Host "ERROR: $_" -ForegroundColor Red
    exit 1
}
finally {
    Pop-Location
}

# Compila launcher y publica en MuVoidClient-Release (launcher + cliente).
# Uso: .\deploy-launcher.ps1
# Requiere: compile-client.bat ejecutado antes para tener el cliente en MuVoidClient-Release.

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$releaseDir = Join-Path (Split-Path $repoRoot -Parent) "MuVoidClient-Release"
$clientInRelease = Join-Path $releaseDir "MuVoidClient"

if (-not (Test-Path $releaseDir)) {
    Write-Host "ERROR: No existe $releaseDir" -ForegroundColor Red
    Write-Host "MuVoidClient-Release debe estar como carpeta hermana de MuVoidClient" -ForegroundColor Yellow
    exit 1
}

Push-Location $repoRoot

try {
    Write-Host "`n=== MuVoid - Deploy a Release (launcher + cliente) ===" -ForegroundColor Cyan

    # 0. Sincronizar cliente si .client-build-dir existe (compile-client ya corrió)
    $buildDirFile = Join-Path $repoRoot ".client-build-dir"
    if (Test-Path $buildDirFile) {
        $clientBuildDir = (Get-Content $buildDirFile -Raw).Trim()
        if (Test-Path $clientBuildDir) {
            Write-Host "[0/5] Sincronizando cliente a MuVoidClient-Release..." -ForegroundColor Gray
            if (-not (Test-Path $clientInRelease)) { New-Item -ItemType Directory -Path $clientInRelease -Force | Out-Null }
            robocopy $clientBuildDir $clientInRelease /E /XO /NJH /NJS /NDL /NC /NS /NP | Out-Null
            Write-Host "[OK] Cliente sincronizado (incluye Data/Object42/Object28.bmd, etc.)" -ForegroundColor Green
        }
    }

    # 0b. Si Data99 existe localmente, copiar Interface 99b a Release (no hace falta subir Data99 a git)
    $data99Interface = Join-Path $repoRoot "Data99\Interface"
    $customInterfaceDest = Join-Path $clientInRelease "Data\99bInt"
    if ((Test-Path $data99Interface) -and (Test-Path $clientInRelease)) {
        Write-Host "[0b] Copiando Interface 99b (Data99) a Release..." -ForegroundColor Gray
        if (-not (Test-Path $customInterfaceDest)) { New-Item -ItemType Directory -Path $customInterfaceDest -Force | Out-Null }
        robocopy $data99Interface $customInterfaceDest /E /XO /NJH /NJS /NDL /NC /NS /NP | Out-Null
        $count = (Get-ChildItem $customInterfaceDest -Recurse -File -ErrorAction SilentlyContinue).Count
        Write-Host "[OK] Interface 99b copiada ($count archivos)" -ForegroundColor Green
    }

    # 1. Compilar launcher (forzar target en proyecto para que el exe quede en launcher/src-tauri/target/release)
    Write-Host "[1/5] Compilando launcher..." -ForegroundColor Gray
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
    Write-Host "[2/5] Copiando launcher a MuVoidClient-Release..." -ForegroundColor Gray
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
    $json = $manifest | ConvertTo-Json -Depth 4 -Compress
    # Escribir sin BOM (UTF-8 puro) para que serde_json lo parsee correctamente
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText((Join-Path $releaseDir "launcher_version.json"), $json, $utf8NoBom)

    Write-Host "[OK] Launcher $version copiado a MuVoidClient-Release" -ForegroundColor Green

    # 4. Git en MuVoidClient (source)
    Write-Host "[3/5] Commit en MuVoidClient..." -ForegroundColor Gray
    $clientVer = (Get-Content (Join-Path $repoRoot "CLIENT_VERSION") -Raw -ErrorAction SilentlyContinue).Trim()
    git add launcher/index.html launcher/src/main.js launcher/src-tauri/src/http_updater.rs
    git add scripts/deploy-launcher.ps1 scripts/deploy-launcher.bat
    git add CLIENT_VERSION scripts/generate-client-manifest.ps1 2>$null
    git diff --cached --quiet 2>$null
    if ($LASTEXITCODE -ne 0) {
        $msg = if ($clientVer) { "chore: cliente v$clientVer" } else { "chore: actualizacion" }
        git commit -m $msg
        Write-Host "[OK] Commit en MuVoidClient" -ForegroundColor Green
    } else {
        Write-Host "[!] Sin cambios en MuVoidClient" -ForegroundColor Yellow
    }

    # 5. Git en MuVoidClient-Release (launcher + cliente completo con -f para ignorar .gitignore)
    Write-Host "[5/5] Commit en MuVoidClient-Release..." -ForegroundColor Gray
    Push-Location $releaseDir
    try {
        git add launcher_version.json $exeName 2>$null
        if (Test-Path "MuVoidClient") {
            git add -f MuVoidClient 2>$null
            Write-Host "[*] Incluyendo carpeta MuVoidClient (Main.exe, Data, version.json, etc.)" -ForegroundColor Gray
        }
        git status --short
        git diff --cached --quiet 2>$null
        if ($LASTEXITCODE -ne 0) {
            $clientVer = (Get-Content (Join-Path $repoRoot "CLIENT_VERSION") -Raw -ErrorAction SilentlyContinue).Trim()
            $verInfo = if ($clientVer) { "cliente v$clientVer" } else { "launcher v$version" }
            git commit -m "chore: $verInfo"
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

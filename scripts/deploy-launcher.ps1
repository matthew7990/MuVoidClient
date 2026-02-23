# deploy-launcher.ps1
# Compila cliente, compila launcher y publica en MuVoidClient-Release (launcher + cliente).
# DESPLIEGUE CENTRALIZADO: Este es el único script que necesitas para publicar todo.
# Uso: .\deploy-launcher.ps1

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
    Write-Host "`n=== MuVoid - Despliegue Centralizado (Launcher + Cliente) ===" -ForegroundColor Cyan

    # 1. Compilar Cliente MuMain
    Write-Host "[1/6] Compilando Cliente MuMain..." -ForegroundColor Gray
    & cmd.exe /c "$repoRoot\compile-client.bat" --no-pause
    
    # El compilador deja el exe en Source/out/build/vs-x86/Release/Main.exe (normalmente)
    $possibleOutDirs = @(
        "Source/out/build/vs-x86/Release",
        "Source/out/build/vs-x86/src/Release",
        "MuMain/out/build/vs-x86/src/Release"
    )
    $clientBuildDir = $null
    foreach ($p in $possibleOutDirs) {
        if (Test-Path (Join-Path $repoRoot "$p\Main.exe")) {
            $clientBuildDir = Join-Path $repoRoot $p
            break
        }
    }
    
    if (-not $clientBuildDir) {
        throw "No se encontró Main.exe tras la compilación."
    }

    # 2. Sincronizar cliente a MuVoidClient-Release
    Write-Host "[2/6] Sincronizando cliente a MuVoidClient-Release..." -ForegroundColor Gray
    if (-not (Test-Path $clientInRelease)) { New-Item -ItemType Directory -Path $clientInRelease -Force | Out-Null }
    robocopy $clientBuildDir $clientInRelease /MIR /NJH /NJS /NDL /NC /NS /NP | Out-Null
    
    # 2b. Generar Manifest del Cliente (en Release)
    Write-Host "[*] Generando version manifest del cliente..." -ForegroundColor Gray
    & "$repoRoot\scripts\generate-client-manifest.ps1" -ExePath (Join-Path $clientInRelease "Main.exe")

    # 3. Compilar Launcher
    Write-Host "[3/6] Compilando Launcher..." -ForegroundColor Gray
    $launcherTargetDir = Join-Path $repoRoot "launcher\src-tauri\target"
    $env:CARGO_TARGET_DIR = $launcherTargetDir
    Push-Location (Join-Path $repoRoot "launcher")
    try {
        $null = npm run tauri build
        if ($LASTEXITCODE -ne 0) { throw "Fallo compilación del launcher" }
    } finally {
        Remove-Item Env:CARGO_TARGET_DIR -ErrorAction SilentlyContinue
        Pop-Location
    }

    # 4. Preparar Launcher en Release
    $launcherDir = Join-Path $launcherTargetDir "release"
    $exeFile = Get-ChildItem -Path $launcherDir -Filter "*.exe" -ErrorAction SilentlyContinue | Where-Object { $_.Name -notmatch "\.d\.exe$" } | Select-Object -First 1
    if (-not $exeFile) { throw "No se encontró el .exe del launcher" }

    $version = (Get-Content (Join-Path $repoRoot "launcher\VERSION") -Raw).Trim()
    $exeName = "MuVoid Launcher.exe"
    $destExe = Join-Path $releaseDir $exeName
    Copy-Item $exeFile.FullName $destExe -Force

    # Generar manifest del launcher
    Write-Host "[4/6] Generando launcher_version.json..." -ForegroundColor Gray
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $bytes = [System.IO.File]::ReadAllBytes($destExe)
    $hex = ([BitConverter]::ToString($sha.ComputeHash($bytes)) -replace '-','').ToLower()
    $sha.Dispose()

    $manifest = @{
        version = $version
        date = Get-Date -Format "yyyy-MM-dd"
        files = @(@{ path = $exeName; sha256 = $hex; size = (Get-Item $destExe).Length })
    }
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText((Join-Path $releaseDir "launcher_version.json"), ($manifest | ConvertTo-Json -Depth 4 -Compress), $utf8NoBom)

    # 5. Commits en ambos repos
    Write-Host "[5/6] Creando commits..." -ForegroundColor Gray
    $clientVer = (Get-Content (Join-Path $repoRoot "CLIENT_VERSION") -Raw).Trim()

    # Repo Source
    git add .
    git diff --cached --quiet
    if ($LASTEXITCODE -ne 0) {
        git commit -m "release: v$clientVer"
        Write-Host "[OK] Commit en MuVoidClient" -ForegroundColor Green
    }

    # Repo Release
    Push-Location $releaseDir
    try {
        git add -A
        git diff --cached --quiet
        if ($LASTEXITCODE -ne 0) {
            git commit -m "update: v$clientVer"
            Write-Host "[OK] Commit en MuVoidClient-Release" -ForegroundColor Green
        }
    } finally {
        Pop-Location
    }

    # 6. Push autommático
    Write-Host "[6/6] Subiendo cambios a GitHub (Pushear)..." -ForegroundColor Cyan
    git push origin main
    
    Push-Location $releaseDir
    try {
        git push origin main
    } finally {
        Pop-Location
    }

    Write-Host "`n[LISTO] Despliegue total completado v$clientVer" -ForegroundColor Green
}
catch {
    Write-Host "ERROR: $_" -ForegroundColor Red
    exit 1
}
finally {
    Pop-Location
}

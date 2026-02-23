# deploy-client.ps1
# Publicación centralizada: Sincroniza build, genera manifest, actualiza el repo de Release y sube todo a GitHub.
# Uso: .\deploy-client.ps1

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$stagingDir = Join-Path $repoRoot "MuVoidClient"

Write-Host "=== MuVoid - Despliegue Centralizado ===" -ForegroundColor Cyan

# 0. Compilar Cliente
Write-Host "[*] Iniciando compilación del cliente..." -ForegroundColor Gray
& cmd.exe /c "$repoRoot\compile-client.bat" --no-pause

# 1. Localizar Build Reciente
$possibleOutDirs = @(
    "Source/out/build/vs-x86/Release",
    "Source/out/build/vs-x86/src/Release",
    "MuMain/out/build/vs-x86/src/Release"
)
$buildOutDir = $null
foreach ($p in $possibleOutDirs) {
    if (Test-Path (Join-Path $repoRoot "$p\Main.exe")) {
        $buildOutDir = Join-Path $repoRoot $p
        break
    }
}

if (-not $buildOutDir) {
    Write-Host "ERROR: No se encontró Main.exe en ninguna carpeta de salida conocida. Ejecuta el compilador primero." -ForegroundColor Red
    exit 1
}

Write-Host "[*] Build encontrado en: $buildOutDir" -ForegroundColor Gray

# 2. Sincronizar Build a la carpeta de Staging
Write-Host "[*] Sincronizando build a la carpeta de Staging..." -ForegroundColor Gray
if (-not (Test-Path $stagingDir)) { New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null }
robocopy "$buildOutDir." "$stagingDir." /MIR /NJH /NJS /NDL /NC /NS /NP /XD .git

# 3. Generar Manifest (version.json)
Write-Host "[*] Generando version manifest..." -ForegroundColor Gray
& "$repoRoot\scripts\generate-client-manifest.ps1" -ExePath (Join-Path $stagingDir "Main.exe")

# 4. Leer Versión Actual
$version = "1.0.0"
if (Test-Path (Join-Path $repoRoot "CLIENT_VERSION")) {
    $version = (Get-Content (Join-Path $repoRoot "CLIENT_VERSION") -Raw).Trim()
}

# 5. Mirror al repositorio de Release (hermano)
$releaseRepo = Join-Path $repoRoot "..\MuVoidClient-Release"
if (Test-Path $releaseRepo) {
    Write-Host "[*] Sincronizando con repositorio de Release (hermano)..." -ForegroundColor Gray
    $releaseClientDir = Join-Path $releaseRepo "MuVoidClient"
    if (-not (Test-Path $releaseClientDir)) { New-Item -ItemType Directory -Path $releaseClientDir -Force | Out-Null }
    
    robocopy "$stagingDir." "$releaseClientDir." /MIR /NJH /NJS /NDL /NC /NS /NP
    
    # Auto-push del repositorio de Release
    Push-Location $releaseRepo
    try {
        git add .
        $status = git status --short
        if ($status) {
            git commit -m "Client Update v$version"
            Write-Host "[*] Subiendo cambios al repositorio de Release..." -ForegroundColor Gray
            git push origin main
            Write-Host "[OK] Repositorio de Release actualizado y subido." -ForegroundColor Green
        } else {
            Write-Host "[!] No hay cambios en el repositorio de Release." -ForegroundColor Yellow
        }
    } finally {
        Pop-Location
    }
}

# 6. Actualizar rama 'client' en el repositorio actual
Push-Location $repoRoot
try {
    $currentBranch = (git rev-parse --abbrev-ref HEAD 2>$null)
    if (-not $currentBranch) { $currentBranch = "source" }
    
    Write-Host "[*] Actualizando rama 'client'..." -ForegroundColor Gray
    
    # Guardar staging en temp (git checkout borraría lo no trackeado)
    $tempDir = Join-Path $env:TEMP "MuVoidClient-deploy-$(Get-Random)"
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    Copy-Item $stagingDir $tempDir -Recurse -Force
    
    git checkout client
    git rm -rf . 2>$null
    
    # Restaurar staging
    $restoredStaging = Join-Path $repoRoot "MuVoidClient"
    if (-not (Test-Path $restoredStaging)) { New-Item -ItemType Directory -Path $restoredStaging -Force | Out-Null }
    Copy-Item (Join-Path $tempDir "MuVoidClient\*") $restoredStaging -Recurse -Force
    Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    
    git add -f MuVoidClient
    $status = git status --short
    if ($status) {
        git commit -m "MuVoidClient v$version - $(Get-Date -Format 'yyyy-MM-dd')"
        Write-Host "[*] Subiendo rama 'client'..." -ForegroundColor Gray
        git push origin client
        Write-Host "[OK] Rama 'client' actualizada y subida." -ForegroundColor Green
    } else {
        Write-Host "[!] No hay cambios para la rama 'client'." -ForegroundColor Yellow
    }
    
    git checkout $currentBranch 2>$null
}
catch {
    Write-Host "ERROR: $_" -ForegroundColor Red
    if ($currentBranch) { git checkout $currentBranch 2>$null }
}
finally {
    Pop-Location
}

Write-Host "`n[LISTO] El despliegue se ha completado correctamente." -ForegroundColor Green

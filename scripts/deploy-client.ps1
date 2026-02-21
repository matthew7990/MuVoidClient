# Publica el build en la rama 'client'.
# Uso: .\deploy-client.ps1
# Requiere: build-all.bat ejecutado previamente (MuVoidClient/ debe existir).

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$outDir   = Join-Path $repoRoot "MuVoidClient"

if (-not (Test-Path $outDir)) {
    Write-Host "ERROR: Ejecuta build-all.bat primero para crear MuVoidClient/" -ForegroundColor Red
    exit 1
}

$version = "1.0.0"
if (Test-Path (Join-Path $repoRoot "CLIENT_VERSION")) {
    $version = (Get-Content (Join-Path $repoRoot "CLIENT_VERSION") -Raw).Trim()
}

Push-Location $repoRoot

try {
    # Rama actual
    $currentBranch = (git rev-parse --abbrev-ref HEAD 2>$null)
    if (-not $currentBranch) { $currentBranch = "source" }

    Write-Host "[*] Rama actual: $currentBranch" -ForegroundColor Gray
    Write-Host "[*] Publicando en rama 'client' (solo distribucion, sin source)..." -ForegroundColor Gray

    # Guardar MuVoidClient completa en temp (git clean lo borraria)
    $tempDir = Join-Path $env:TEMP "MuVoidClient-deploy-$(Get-Random)"
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    Copy-Item $outDir (Join-Path $tempDir "MuVoidClient") -Recurse -Force

    # Crear rama huerfana 'client' o reutilizarla
    $clientExists = git rev-parse --verify client 2>$null
    if ($clientExists) {
        git checkout client
        git rm -rf . 2>$null
        Get-ChildItem $repoRoot -Force | Where-Object { $_.Name -ne ".git" } | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    } else {
        git checkout --orphan client
        git reset
        git clean -fdx
    }

    # Copiar carpeta MuVoidClient al repo (incluye Main.exe, DLLs, BMD, version.json, todo)
    Copy-Item (Join-Path $tempDir "MuVoidClient") $repoRoot -Recurse -Force
    Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue

    # -f fuerza agregar a pesar de .gitignore (MuVoidClient/ se ignora en source, pero en client SÍ debe ir)
    git add -f MuVoidClient
    $count = (git status --short | Measure-Object -Line).Lines
    if ($count -eq 0) {
        Write-Host "[!] No hay cambios respecto al ultimo deploy." -ForegroundColor Yellow
    } else {
        git commit -m "MuVoidClient v$version - $(Get-Date -Format 'yyyy-MM-dd')"
        Write-Host "[OK] Commit creado en rama 'client'" -ForegroundColor Green
    }

    # Volver a rama de source (client no debe quedarse activa para desarrollo)
    $backBranch = $currentBranch
    if ($backBranch -eq "client") {
        if (git rev-parse --verify main 2>$null) { $backBranch = "main" }
        elseif (git rev-parse --verify source 2>$null) { $backBranch = "source" }
        else { $backBranch = "master" }
    }
    git checkout $backBranch 2>$null
    Write-Host ""
    Write-Host "Listo. Para subir a GitHub: git push origin client" -ForegroundColor Cyan
}
catch {
    Write-Host "ERROR: $_" -ForegroundColor Red
    git checkout $currentBranch 2>$null
    exit 1
}
finally {
    Pop-Location
}

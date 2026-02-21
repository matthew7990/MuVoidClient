# Publica el cliente MuVoid a client-release (sin workflow, todo local)
# Busca el compilado en: MuMain/out/src/release, MuMain/build/RelWithDebInfo, etc.
# Uso: .\publish-client.ps1

$ErrorActionPreference = "Stop"

function Invoke-GitWithProgress {
    param([string[]]$GitArgs, [string]$Activity)
    Write-Host "[*] $Activity..." -ForegroundColor Gray
    $prevErr = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $output = & git @GitArgs 2>&1
    $ErrorActionPreference = $prevErr
    $output | ForEach-Object {
        if ($_ -match "(\d+)%") {
            Write-Progress -Activity $Activity -Status $_ -PercentComplete [int]$matches[1]
        }
    }
    Write-Progress -Activity $Activity -Completed
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw "Git fallo con codigo $LASTEXITCODE" }
}
$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not (Test-Path "$repoRoot\.git")) { $repoRoot = (Get-Location).Path }
Set-Location $repoRoot

# Rutas donde puede estar el cliente compilado (prioridad)
$possibleOutDirs = @(
    "MuMain/out/build/vs-x86/src/Release",
    "MuMain/out/src/release",
    "MuMain/out/src/Release",
    "MuMain/build/RelWithDebInfo",
    "MuMain/build/Release",
    "MuMain/out/Release"
)
$outDir = $null
foreach ($p in $possibleOutDirs) {
    if (Test-Path "$repoRoot/$p/Main.exe") {
        $outDir = $p
        break
    }
}
if (-not $outDir) {
    # Buscar Main.exe recursivamente bajo MuMain
    $mainExe = Get-ChildItem -Path "MuMain" -Filter "Main.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($mainExe) {
        $outDir = $mainExe.DirectoryName -replace [regex]::Escape($repoRoot), "" -replace "^\\", "" -replace "\\", "/"
    }
}
$versionFile = "MuMain/VERSION"
$changelogFile = "MuMain/CLIENT_CHANGELOG.md"

Write-Host "`n=== MuVoid - Publicar Cliente (local) ===" -ForegroundColor Cyan
Write-Host ""

# 1. Validar VERSION
if (-not (Test-Path $versionFile)) {
    Write-Error "Falta MuMain/VERSION. Crea el archivo con la version (ej: 1.0.0)"
}
$version = (Get-Content $versionFile -Raw).Trim()
if (-not $version -match '^\d+\.\d+\.\d+$') {
    Write-Error "VERSION debe ser semantica (ej: 1.0.0). Actual: $version"
}

# 2. Validar changelog
if (-not (Test-Path $changelogFile)) {
    Write-Error "Falta MuMain/CLIENT_CHANGELOG.md"
}
$content = Get-Content $changelogFile -Raw -Encoding UTF8
$changelog = @()
$inSection = $false
foreach ($line in ($content -split "`n")) {
    if ($line -match '^##\s+Pr') { $inSection = $true; continue }
    if ($inSection -and $line -match '^##\s+') { break }
    if ($inSection -and $line -match '^\-\s+(.+)') { $changelog += $matches[1].Trim() }
}
if ($changelog.Count -eq 0) {
    Write-Error "Anade al menos un item en '## Proxima version' de CLIENT_CHANGELOG.md"
}

# 3. Verificar compilado
if (-not $outDir -or -not (Test-Path "$outDir/Main.exe")) {
    Write-Error "No hay compilado. Buscado en: $($possibleOutDirs -join ', '). Compila el cliente primero."
}
Write-Host "[*] Usando compilado en: $outDir" -ForegroundColor Gray

# 4. Copiar con robocopy (mucho mas rapido que Copy-Item)
$outDirFull = Join-Path $repoRoot ($outDir -replace "/", [System.IO.Path]::DirectorySeparatorChar)
$distDir = Join-Path $env:TEMP "muvoid-client-dist-$([Guid]::NewGuid().ToString('N').Substring(0,8))"
New-Item -ItemType Directory -Path $distDir -Force | Out-Null

Write-Host "[*] Copiando archivos (robocopy)..." -ForegroundColor Gray
$robocopy = robocopy $outDirFull $distDir /E /NFL /NDL /NJH /NJS /nc /ns /np /r:1 /w:1
if ($robocopy -ge 8) { throw "Robocopy fallo (codigo $robocopy)" }

# 5. SHA256 en paralelo (runspaces por lotes)
Write-Host "[*] Calculando SHA256 (paralelo)..." -ForegroundColor Gray
$allDistFiles = @(Get-ChildItem $distDir -File -Recurse)
$distLen = $distDir.Length + 1
$batchSize = [math]::Min(500, [math]::Max(1, [Environment]::ProcessorCount * 64))
$batches = for ($i = 0; $i -lt $allDistFiles.Count; $i += $batchSize) {
    ,@($allDistFiles[$i..([math]::Min($i + $batchSize - 1, $allDistFiles.Count - 1))])
}
$runspacePool = [runspacefactory]::CreateRunspacePool(1, [Environment]::ProcessorCount)
$runspacePool.Open()
$files = @()
foreach ($batch in $batches) {
    $jobs = $batch | ForEach-Object {
        $f = $_
        $ps = [powershell]::Create().AddScript({
            param($path, $len)
            $relPath = $path.Substring($len) -replace "\\", "/"
            $sha = [System.Security.Cryptography.SHA256]::Create()
            $bytes = [System.IO.File]::ReadAllBytes($path)
            $hex = [BitConverter]::ToString($sha.ComputeHash($bytes)) -replace '-',''
            $sha.Dispose()
            [pscustomobject]@{ path = $relPath; sha256 = $hex.ToLower(); size = (Get-Item $path).Length }
        }).AddArgument($f.FullName).AddArgument($distLen)
        $ps.RunspacePool = $runspacePool
        [pscustomobject]@{ Pipe = $ps; Handle = $ps.BeginInvoke() }
    }
    $files += $jobs | ForEach-Object { $_.Pipe.EndInvoke($_.Handle); $_.Pipe.Dispose() }
}
$runspacePool.Close()

$commit = (git rev-parse --short HEAD).Trim()
$date = Get-Date -Format "yyyy-MM-dd"
$manifest = [ordered]@{
    version  = $version
    date     = $date
    commit   = $commit
    changelog = $changelog
    files    = $files
}
$manifest | ConvertTo-Json -Depth 4 | Set-Content "$distDir/version.json" -Encoding UTF8

Write-Host "[OK] version.json generado: $version ($($files.Count) archivos)" -ForegroundColor Green

# 6. Quitar lockfiles
@(".git\index.lock", ".git\refs\heads\master.lock") | ForEach-Object {
    if (Test-Path $_) { Remove-Item $_ -Force; Write-Host "[*] Eliminado $_" }
}

# 7. Subir a client-release (worktree temporal - NO toca tu directorio)
$worktreePath = Join-Path $env:TEMP "muvoid-client-release-worktree"
if (Test-Path $worktreePath) {
    $prevErr = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    git worktree remove -f $worktreePath 2>&1 | Out-Null
    $ErrorActionPreference = $prevErr
    if (Test-Path $worktreePath) { Remove-Item $worktreePath -Recurse -Force }
}

$branchExists = git ls-remote --heads origin client-release 2>$null
$prevErr = $ErrorActionPreference
$ErrorActionPreference = 'Continue'

if ($branchExists) {
    git fetch origin client-release --depth 1 2>&1 | Out-Null
    git worktree add $worktreePath client-release 2>&1 | Out-Null
} else {
    git worktree add -b client-release $worktreePath HEAD 2>&1 | Out-Null
}

$ErrorActionPreference = $prevErr

Push-Location $worktreePath
try {
    Get-ChildItem -Force | Where-Object { $_.Name -ne ".git" } | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    robocopy $distDir . /E /NFL /NDL /NJH /NJS /nc /ns /np /r:1 /w:1 | Out-Null
    Remove-Item $distDir -Recurse -Force -ErrorAction SilentlyContinue

    $readme = @"
# MuVoid Client Distribution

> Publicado localmente - no editar manualmente.

**Version:** $version
**Commit:** $commit
**Date:** $date

El launcher descarga esta rama automaticamente.
"@
    Set-Content "README.md" $readme -Encoding UTF8

    git add --all
    git status --short
    git diff --cached --quiet 2>$null
    $hasChanges = $LASTEXITCODE -ne 0

    if ($hasChanges) {
        git commit -m "chore(client): release $version from $commit"
        Write-Host "[*] Subiendo a client-release..." -ForegroundColor Gray
        Invoke-GitWithProgress -GitArgs "push","origin","client-release","--progress" -Activity "Subiendo"
        Write-Host "`n[OK] Cliente $version publicado en client-release" -ForegroundColor Green
    } else {
        Write-Host "`n[!] No hay cambios respecto a client-release" -ForegroundColor Yellow
    }
} finally {
    Pop-Location
    git worktree remove -f $worktreePath 2>&1 | Out-Null
}

Write-Host ""

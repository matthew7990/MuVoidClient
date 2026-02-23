# Copia los archivos 99b a Data\99bInt. El cliente intenta 99bInt primero;
# si no existe, usa Data\Interface (Season). Así 99b reemplaza solo lo que tiene.
# Uso: .\apply-interface-99.ps1
#
# Opción A: Data99 con Interface
# Opción B: 99bInt\ o Interface\ en la raíz del repo

$ErrorActionPreference = "SilentlyContinue"
$repoRoot = Split-Path $PSScriptRoot -Parent
$dataDir = Join-Path $repoRoot "Source\src\bin\Data"

Write-Host "`n=== MuVoid - Interface 99b (fallback a Season) ===" -ForegroundColor Cyan
Write-Host "Destino: Data\99bInt (99) + Data\Interface (Season)`n" -ForegroundColor Gray

$replacements = @()

# Opción A: Data99 existe
$data99 = Join-Path $repoRoot "Data99"
if (Test-Path $data99) {
    Write-Host "[*] Usando Data99" -ForegroundColor Cyan
    $data99Interface = Join-Path $data99 "Interface"
    if (Test-Path $data99Interface) {
        $replacements += @{ Dest = "99bInt"; Sources = @($data99Interface) }
    }
} else {
    # Opción B: 99bInt o Interface en la raíz (preferir 99bInt)
    if (Test-Path (Join-Path $repoRoot "99bInt")) {
        $replacements += @{ Dest = "99bInt"; Sources = @(Join-Path $repoRoot "99bInt") }
    } elseif (Test-Path (Join-Path $repoRoot "Interface")) {
        $replacements += @{ Dest = "99bInt"; Sources = @(Join-Path $repoRoot "Interface") }
    }
}

if ($replacements.Count -eq 0) {
    exit 0
}

$copied = 0
foreach ($r in $replacements) {
    $destDir = Join-Path $dataDir $r.Dest
    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
    }

    foreach ($srcPath in $r.Sources) {
        if (-not (Test-Path $srcPath)) { continue }
        $items = Get-ChildItem $srcPath -Recurse -File -ErrorAction SilentlyContinue
        if ($items.Count -eq 0) { continue }

        Write-Host "[*] Copiando $srcPath -> Data\$($r.Dest)\" -ForegroundColor Gray
        foreach ($item in $items) {
            $baseLen = $srcPath.TrimEnd('\').Length + 1
            $rel = $item.FullName.Substring($baseLen)
            $destFile = Join-Path $destDir $rel
            $destParent = Split-Path $destFile -Parent
            if (-not (Test-Path $destParent)) {
                New-Item -ItemType Directory -Path $destParent -Force | Out-Null
            }
            Copy-Item $item.FullName $destFile -Force
            $copied++
        }
        Write-Host "    $($items.Count) archivos" -ForegroundColor Green
    }
}

if ($copied -eq 0) {
    exit 0
}

Write-Host "`n[OK] $copied archivos copiados a Data\99bInt. El cliente usa 99b primero, Season como fallback." -ForegroundColor Green

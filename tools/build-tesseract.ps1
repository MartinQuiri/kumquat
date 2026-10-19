# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Kumquat contributors
<#
.SYNOPSIS
  Compila Tesseract (estático, con vcpkg) y deja el motor OCR en engines/tesseract/<rid>.
.DESCRIPTION
  No hay binarios oficiales de Tesseract para Windows: Kumquat lo compila desde el código fuente
  con versiones fijadas (tools/ocr/vcpkg.json) y verifica el SHA-256 de cada modelo de idioma.
  Requiere Visual Studio 2022 con "Desarrollo para el escritorio con C++" (para win-arm64, las
  herramientas ARM64). La primera vez tarda (compila leptonica y sus dependencias).
#>
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content (Join-Path $PSScriptRoot 'engines.json') -Raw | ConvertFrom-Json
$engine = $manifest.engines | Where-Object id -EQ 'tesseract'
$triplet = @{ 'win-x64' = 'x64-windows-kumquat'; 'win-arm64' = 'arm64-windows-kumquat' }[$Runtime]
$target = Join-Path $repo "engines\tesseract\$Runtime"
$work = Join-Path $repo "artifacts\tesseract-build"

# vcpkg: VCPKG_ROOT, el de los runners de GitHub (VCPKG_INSTALLATION_ROOT) o el de Visual Studio.
$vcpkgRoot = if ($env:VCPKG_ROOT) { $env:VCPKG_ROOT } else { $env:VCPKG_INSTALLATION_ROOT }
$vcpkg = if ($vcpkgRoot) { Join-Path $vcpkgRoot 'vcpkg.exe' } else {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    $vs = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    Join-Path $vs 'VC\vcpkg\vcpkg.exe'
}
if (-not (Test-Path $vcpkg)) { throw "No se encontró vcpkg ($vcpkg). Instala Visual Studio 2022 con C++ o define VCPKG_ROOT." }

Write-Host "[..] Compilando Tesseract $($engine.version) para $triplet (puede tardar la primera vez)"
& $vcpkg install --x-manifest-root="$PSScriptRoot\ocr" --x-install-root="$work\installed" --triplet=$triplet --overlay-triplets="$PSScriptRoot\ocr\triplets" --clean-after-build --no-print-usage
if ($LASTEXITCODE -ne 0) { throw "vcpkg falló ($LASTEXITCODE)." }

$installed = Join-Path $work "installed\$triplet"
$exe = Join-Path $installed 'tools\tesseract\tesseract.exe'
if (-not (Test-Path $exe)) { throw "vcpkg no produjo $exe." }

if (Test-Path $target) { Remove-Item $target -Recurse -Force }
New-Item -ItemType Directory -Force "$target\bin", "$target\tessdata", "$target\licenses" | Out-Null
Copy-Item $exe "$target\bin\tesseract.exe"

# Licencias de Tesseract y de cada biblioteca enlazada estáticamente.
Get-ChildItem (Join-Path $installed 'share') -Directory | ForEach-Object {
    $copyright = Join-Path $_.FullName 'copyright'
    if (Test-Path $copyright) { Copy-Item $copyright (Join-Path "$target\licenses" "$($_.Name).txt") }
}

# Modelos de idioma (tessdata_best: los más precisos), verificados por hash.
foreach ($model in $engine.models) {
    $file = Join-Path "$target\tessdata" "$($model.language).traineddata"
    Invoke-WebRequest -UseBasicParsing -Uri $model.url -OutFile $file
    $hash = (Get-FileHash $file -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $model.sha256) { Remove-Item $file; throw "SHA-256 inesperado en $($model.language): $hash" }
}

Set-Content -Encoding utf8 "$target\SOURCE.txt" @"
Tesseract $($engine.version) ($($engine.license)), compilado por Kumquat con vcpkg ($triplet).
Código fuente: $($engine.source)
Receta: tools/ocr/vcpkg.json (baseline de vcpkg fijado) y tools/build-tesseract.ps1.
Modelos: tessdata_best ($($engine.modelsSource)), Apache-2.0.
Bibliotecas incluidas y sus licencias: carpeta licenses/.
"@

Set-Content -NoNewline "$target\.version" "$($engine.version) $($engine.recipe)"
& "$target\bin\tesseract.exe" --version | Select-Object -First 1
Write-Host "[ok] Tesseract listo en $target"

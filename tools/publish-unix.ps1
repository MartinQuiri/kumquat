# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Kumquat contributors
<#
.SYNOPSIS
  Publica kumquat-cli para Linux o macOS (self-contained y recortada), con los motores que trae
  Kumquat en ese sistema, la prueba si el RID coincide con la máquina y genera el .tar.gz y su SHA-256.
.DESCRIPTION
  Linux: incluye ffmpeg, qpdf y mutool fijados por hash (tools/fetch-engines.ps1); Tesseract, Calibre
  y LibreOffice se usan si están instalados en el sistema. macOS: los motores salen de Homebrew.
  Para macOS hay que publicar desde una Mac: el ejecutable tiene que quedar firmado (ad hoc) y eso
  solo lo hace el SDK de .NET con codesign.
.EXAMPLE
  ./tools/publish-unix.ps1 -Runtime linux-x64 -Version 0.2.0
#>
[CmdletBinding()]
param(
    [ValidateSet('linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64')]
    [string] $Runtime = 'linux-x64',
    [string] $Version = '0.2.0'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$platform = if ($Runtime.EndsWith('arm64')) { 'ARM64' } else { 'x64' }
$name = "kumquat-$Version-$Runtime"
$out = Join-Path $repo "artifacts/publish/$Runtime/$name"
$tarball = Join-Path $repo "artifacts/Kumquat-$Version-$Runtime.tar.gz"

& (Join-Path $PSScriptRoot 'fetch-engines.ps1') -Runtime $Runtime

$parent = Split-Path -Parent $out
if (Test-Path $parent) { Remove-Item -Recurse -Force $parent }
dotnet publish (Join-Path $repo 'src/Kumquat.Cli') -c Release -r $Runtime --self-contained `
    -p:Platform=$platform -p:Version=$Version -o $out -nologo
if ($LASTEXITCODE -ne 0) { throw 'Falló la publicación de Kumquat.Cli' }

# Motores propios junto a la CLI: engines/<id>/ (lo encuentra EngineLocator). Solo lo necesario en
# runtime: sin ffplay, documentación, encabezados ni pkgconfig. ffmpeg y qpdf buscan sus bibliotecas
# en $ORIGIN/../lib.
$engines = @{
    'ffmpeg' = @('bin/ffmpeg', 'bin/ffprobe', 'lib', 'LICENSE.txt', 'SOURCE.txt')
    'qpdf'   = @('bin/qpdf', 'lib', 'SOURCE.txt')
    'mupdf'  = @('mutool', 'COPYING.txt', 'SOURCE.txt')
}
foreach ($id in $engines.Keys) {
    $source = Join-Path $repo "engines/$id/$Runtime"
    if (-not (Test-Path $source)) { continue } # macOS: Homebrew.
    foreach ($item in $engines[$id]) {
        $destination = Join-Path $out "engines/$id/$item"
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
        # cp -a conserva los enlaces simbólicos de las bibliotecas y el bit de ejecución.
        & cp -a (Join-Path $source $item) $destination
        if ($LASTEXITCODE -ne 0) { throw "No se pudo copiar $id/$item" }
    }
}
Get-ChildItem -Path (Join-Path $out 'engines') -Recurse -Directory -Filter 'pkgconfig' -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force

foreach ($doc in 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'README.md', 'README.en.md') {
    Copy-Item (Join-Path $repo $doc) $out
}

# Prueba (solo si el RID coincide con la máquina): la CLI ve los motores y convierte de verdad.
$os = if ($IsLinux) { 'linux' } else { 'osx' }
$arch = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'arm64' } else { 'x64' }
if ($Runtime -eq "$os-$arch") {
    $cli = Join-Path $out 'kumquat-cli'
    $engineReport = & $cli engines
    if ($LASTEXITCODE -ne 0 -or $engineReport -notmatch 'ffmpeg=ok' -or $engineReport -notmatch 'vips=ok' -or $engineReport -notmatch 'mupdf=ok') {
        throw "Motores no disponibles en la publicación: $engineReport"
    }
    Write-Host "[ok] $engineReport"

    $smoke = Join-Path ([System.IO.Path]::GetTempPath()) ("kumquat-smoke-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $smoke | Out-Null
    try {
        # Un WAV mínimo (PCM, 8 kHz, 0,1 s de silencio) → MP3 con FFmpeg.
        $samples = 800
        $wav = [System.IO.MemoryStream]::new()
        $writer = [System.IO.BinaryWriter]::new($wav)
        $writer.Write([System.Text.Encoding]::ASCII.GetBytes('RIFF')); $writer.Write([int](36 + $samples * 2))
        $writer.Write([System.Text.Encoding]::ASCII.GetBytes('WAVEfmt ')); $writer.Write([int]16); $writer.Write([int16]1); $writer.Write([int16]1)
        $writer.Write([int]8000); $writer.Write([int]16000); $writer.Write([int16]2); $writer.Write([int16]16)
        $writer.Write([System.Text.Encoding]::ASCII.GetBytes('data')); $writer.Write([int]($samples * 2)); $writer.Write([byte[]]::new($samples * 2))
        [System.IO.File]::WriteAllBytes((Join-Path $smoke 'prueba.wav'), $wav.ToArray())
        $converted = & $cli convert (Join-Path $smoke 'prueba.wav') --to mp3
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path (Join-Path $smoke 'prueba.mp3'))) { throw "kumquat-cli convert falló: $converted" }
        Write-Host "[ok] convierte: $converted"
    } finally {
        Remove-Item -Recurse -Force $smoke
    }
}

if (Test-Path $tarball) { Remove-Item -Force $tarball }
& tar -czf $tarball -C $parent $name
if ($LASTEXITCODE -ne 0) { throw "No se pudo crear $tarball" }
$hash = (Get-FileHash -Algorithm SHA256 $tarball).Hash.ToLowerInvariant()
"$hash  $(Split-Path -Leaf $tarball)" | Set-Content -Encoding ascii -Path "$tarball.sha256"
$size = [math]::Round((Get-Item $tarball).Length / 1MB, 1)
Write-Host "[ok] $tarball ($size MB) sha256=$hash"

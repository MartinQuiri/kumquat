# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Kumquat contributors
<#
.SYNOPSIS
  Compila mutool (MuPDF) para Linux y lo deja en engines/mupdf/<rid>.
.DESCRIPTION
  MuPDF no publica binarios para Linux y el de las distribuciones suele ser viejo (Ubuntu 22.04
  trae 1.19, 24.04 trae 1.23; Kumquat necesita 1.24 o más). Se compila la misma versión que usa
  Windows desde el código fuente fijado en tools/engines.json (SHA-256 verificado), con las
  bibliotecas de thirdparty/ enlazadas estáticamente.

  El compilador es zig cc (versión y hash fijados en el mismo manifiesto) apuntando a una glibc
  vieja: el mutool resultante corre en cualquier distribución, se compile donde se compile, y no
  hace falta tener gcc instalado. Solo se necesita make. Tarda unos minutos.
#>
[CmdletBinding()]
param(
    [ValidateSet('linux-x64', 'linux-arm64')]
    [string]$Runtime = 'linux-x64'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repo = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content (Join-Path $PSScriptRoot 'engines.json') -Raw | ConvertFrom-Json
$engine = $manifest.engines | Where-Object id -EQ 'mupdf'
$artifact = $engine.artifacts.$Runtime
$toolchain = $engine.toolchain
$target = Join-Path $repo "engines/mupdf/$Runtime"
$stamp = Join-Path $target '.version'
$expected = "$($engine.version) $($artifact.sha256) zig-$($toolchain.zig) glibc-$($toolchain.glibc)"
if ((Test-Path $stamp) -and ((Get-Content -Raw $stamp).Trim() -eq $expected)) {
    Write-Host "[ok] mupdf $($engine.version) ($Runtime) ya está compilado."
    return
}

$cache = Join-Path $repo 'engines/.cache'
New-Item -ItemType Directory -Force -Path $cache | Out-Null

# Descarga con hash verificado (código fuente de MuPDF y zig).
function Get-Verified([string]$Url, [string]$Sha256, [string]$File) {
    $path = Join-Path $cache $File
    if (-not (Test-Path $path)) {
        Write-Host "[..] Descargando $Url"
        Invoke-WebRequest -Uri $Url -OutFile "$path.partial" -UseBasicParsing
        Move-Item -Force "$path.partial" $path
    }

    $actual = (Get-FileHash -Algorithm SHA256 -Path $path).Hash.ToLowerInvariant()
    if ($actual -ne $Sha256.ToLowerInvariant()) {
        Remove-Item -Force $path
        throw "SHA-256 no coincide para ${Url}: esperado $Sha256, obtenido $actual."
    }

    return $path
}

# zig para la máquina que compila (no la de destino: zig compila para cualquiera).
$hostArch = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'linux-arm64' } else { 'linux-x64' }
$zigArtifact = $toolchain.$hostArch
$zigDir = Join-Path $cache "zig-$($toolchain.zig)-$hostArch"
if (-not (Test-Path (Join-Path $zigDir 'zig'))) {
    $zigTarball = Get-Verified $zigArtifact.url $zigArtifact.sha256 ("zig-{0}.tar.xz" -f $zigArtifact.sha256.Substring(0, 12))
    $staging = Join-Path $cache ("extract-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $staging | Out-Null
    & tar -xJf $zigTarball -C $staging
    if ($LASTEXITCODE -ne 0) { throw "No se pudo extraer $zigTarball" }
    if (Test-Path $zigDir) { Remove-Item -Recurse -Force $zigDir }
    Move-Item (Join-Path $staging $zigArtifact.stripPrefix) $zigDir
    Remove-Item -Recurse -Force $staging
}

$tarball = Get-Verified $artifact.url $artifact.sha256 ("mupdf-{0}.tar.gz" -f $artifact.sha256.Substring(0, 12))
$work = Join-Path $repo 'artifacts/mupdf-build'
if (Test-Path $work) { Remove-Item -Recurse -Force $work }
New-Item -ItemType Directory -Force -Path $work | Out-Null
& tar -xzf $tarball -C $work
if ($LASTEXITCODE -ne 0) { throw "No se pudo extraer $tarball" }
$source = Join-Path $work "mupdf-$($engine.version)-source"

# cc, c++ y ar de zig, apuntando a la arquitectura y la glibc de destino. Van en el PATH (no en CC=)
# para que una ruta con espacios no rompa make.
$triple = "$(if ($Runtime -eq 'linux-arm64') { 'aarch64' } else { 'x86_64' })-linux-gnu.$($toolchain.glibc)"
$bin = Join-Path $work 'toolchain'
New-Item -ItemType Directory -Force -Path $bin | Out-Null
$zig = Join-Path $zigDir 'zig'
foreach ($tool in @(@{ Name = 'cc'; Command = "cc -target $triple" }, @{ Name = 'c++'; Command = "c++ -target $triple" }, @{ Name = 'ar'; Command = 'ar' })) {
    $wrapper = Join-Path $bin $tool.Name
    Set-Content -Path $wrapper -Value "#!/bin/sh`nexec `"$zig`" $($tool.Command) `"`$@`"`n" -NoNewline
    & chmod +x $wrapper
}

# Solo las herramientas de línea de comandos: sin visores (X11/GLUT), sin red y sin el OCR propio
# de MuPDF (Kumquat usa Tesseract aparte). HAVE_OBJCOPY=no incrusta las fuentes como código C en
# vez de con el ld del sistema: no hace falta binutils y se puede compilar para otra arquitectura.
Write-Host "[..] Compilando mutool $($engine.version) para $Runtime ($triple; tarda unos minutos)..."
$env:PATH = "$bin$([System.IO.Path]::PathSeparator)$env:PATH"
& make -C $source -j ([Environment]::ProcessorCount) build=release `
    HAVE_X11=no HAVE_GLUT=no HAVE_CURL=no HAVE_TESSERACT=no HAVE_OBJCOPY=no CC=cc CXX=c++ AR=ar tools
if ($LASTEXITCODE -ne 0) { throw "make falló ($LASTEXITCODE)." }

$mutool = Join-Path $source 'build/release/mutool'
if ($Runtime -eq $hostArch) {
    $version = & $mutool -v 2>&1
    if ("$version" -notmatch [regex]::Escape("version $($engine.version)")) { throw "El mutool compilado no responde como $($engine.version): $version" }
}

if (Test-Path $target) { Remove-Item -Recurse -Force $target }
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item $mutool (Join-Path $target 'mutool')
Copy-Item (Join-Path $source 'COPYING') (Join-Path $target 'COPYING.txt')
@(
    "mupdf $($engine.version)"
    "Licencia: $($engine.license)"
    "Código fuente: $($engine.source)"
    "Scripts de build: $($engine.buildScripts) (compilado con tools/build-mupdf.ps1: zig $($toolchain.zig), glibc $($toolchain.glibc))"
) | Set-Content -Encoding utf8 -Path (Join-Path $target 'SOURCE.txt')
$expected | Set-Content -Encoding ascii -NoNewline -Path $stamp
Remove-Item -Recurse -Force $work
Write-Host "[ok] mutool $($engine.version) compilado en $target"

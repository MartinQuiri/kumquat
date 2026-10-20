# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Kumquat contributors
<#
.SYNOPSIS
  Escribe el manifiesto de winget de una versión publicada en GitHub Releases (los MSI y sus
  SHA-256 salen de artifacts/). El resultado se envía a microsoft/winget-pkgs con un PR
  (a mano o con wingetcreate); ver docs/RELEASING.md.
.EXAMPLE
  ./tools/winget-manifest.ps1 -Version 0.2.0 -Repository usuario/kumquat -PackageIdentifier Usuario.Kumquat
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $Version,
    # "dueño/repositorio" en GitHub, donde están los releases.
    [Parameter(Mandatory = $true)][ValidatePattern('^[\w.-]+/[\w.-]+$')][string] $Repository,
    # "Publicador.Programa" en winget (p. ej. el usuario u organización de GitHub + .Kumquat).
    [Parameter(Mandatory = $true)][ValidatePattern('^[\w-]+\.[\w-]+$')][string] $PackageIdentifier
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$manifestVersion = '1.9.0'
$publisher = $PackageIdentifier.Split('.')[0]
$out = Join-Path $repo "artifacts/winget/manifests/$($publisher.Substring(0, 1).ToLowerInvariant())/$($PackageIdentifier.Replace('.', '/'))/$Version"
New-Item -ItemType Directory -Force $out | Out-Null

function Write-Manifest([string] $name, [string] $content) {
    # UTF-8 sin BOM y LF: lo que pide el validador de winget-pkgs.
    [IO.File]::WriteAllText((Join-Path $out $name), $content.Replace("`r`n", "`n"), [Text.UTF8Encoding]::new($false))
}

$installers = foreach ($rid in 'win-x64', 'win-arm64') {
    $msi = Join-Path $repo "artifacts/Kumquat-$Version-$rid.msi"
    if (-not (Test-Path $msi)) {
        Write-Warning "Sin ${msi}: la arquitectura $rid no va en el manifiesto."
        continue
    }

    $arch = @{ 'win-x64' = 'x64'; 'win-arm64' = 'arm64' }[$rid]
    $sha = (Get-FileHash -Algorithm SHA256 $msi).Hash
    @"
- Architecture: $arch
  InstallerUrl: https://github.com/$Repository/releases/download/v$Version/Kumquat-$Version-$rid.msi
  InstallerSha256: $sha
"@
}

if (-not $installers) { throw 'No hay ningún MSI en artifacts/: corre build-installer.ps1 primero.' }

$header = "# Creado con tools/winget-manifest.ps1`n"
Write-Manifest "$PackageIdentifier.yaml" @"
$header# yaml-language-server: `$schema=https://aka.ms/winget-manifest.version.$manifestVersion.schema.json
PackageIdentifier: $PackageIdentifier
PackageVersion: $Version
DefaultLocale: es-ES
ManifestType: version
ManifestVersion: $manifestVersion
"@

Write-Manifest "$PackageIdentifier.installer.yaml" @"
$header# yaml-language-server: `$schema=https://aka.ms/winget-manifest.installer.$manifestVersion.schema.json
PackageIdentifier: $PackageIdentifier
PackageVersion: $Version
InstallerType: wix
Scope: user
InstallModes:
- interactive
- silent
- silentWithProgress
UpgradeBehavior: install
# Kumquat se abre solo al terminar de instalar; winget lo instala sin abrirlo.
InstallerSwitches:
  Silent: /quiet NOLAUNCH=1
  SilentWithProgress: /passive NOLAUNCH=1
MinimumOSVersion: 10.0.19045.0
Installers:
$($installers -join "`n")
ManifestType: installer
ManifestVersion: $manifestVersion
"@

$common = @"
PackageIdentifier: $PackageIdentifier
PackageVersion: $Version
Publisher: Kumquat contributors
PublisherUrl: https://github.com/$Repository
PublisherSupportUrl: https://github.com/$Repository/issues
PackageName: Kumquat
PackageUrl: https://github.com/$Repository
License: GPL-3.0-or-later
LicenseUrl: https://github.com/$Repository/blob/main/LICENSE
Copyright: Copyright (C) 2026 Kumquat contributors
ReleaseNotesUrl: https://github.com/$Repository/releases/tag/v$Version
Tags:
- converter
- conversion
- drag-and-drop
- ffmpeg
- ocr
- pdf
"@

Write-Manifest "$PackageIdentifier.locale.es-ES.yaml" @"
$header# yaml-language-server: `$schema=https://aka.ms/winget-manifest.defaultLocale.$manifestVersion.schema.json
$common
PackageLocale: es-ES
ShortDescription: Convierte y edita archivos arrastrándolos a una rueda, sin salir de tu PC.
Description: |-
  Arrastra un archivo, presiona Shift y suéltalo sobre un formato o una herramienta: imágenes, audio,
  video, PDF, documentos de Office y libros electrónicos. Todo se procesa en tu equipo (FFmpeg, libvips,
  MuPDF, qpdf, Tesseract), sin subir nada a internet. También con el teclado (Ctrl+Alt+K) y desde el
  menú del clic derecho.
Moniker: kumquat
ManifestType: defaultLocale
ManifestVersion: $manifestVersion
"@

Write-Manifest "$PackageIdentifier.locale.en-US.yaml" @"
$header# yaml-language-server: `$schema=https://aka.ms/winget-manifest.locale.$manifestVersion.schema.json
$common
PackageLocale: en-US
ShortDescription: Convert and edit files by dragging them onto a wheel, without leaving your PC.
Description: |-
  Drag a file, press Shift and drop it on a format or a tool: images, audio, video, PDF, Office
  documents and e-books. Everything runs on your computer (FFmpeg, libvips, MuPDF, qpdf, Tesseract),
  nothing is uploaded. Also from the keyboard (Ctrl+Alt+K) and the right-click menu.
ManifestType: locale
ManifestVersion: $manifestVersion
"@

Write-Host "[ok] $out"
if (Get-Command winget -ErrorAction SilentlyContinue) {
    winget validate --manifest $out
}

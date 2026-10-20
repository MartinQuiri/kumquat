# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Kumquat contributors
<#
.SYNOPSIS
  Arma el instalador MSI por usuario (installer/) a partir de lo que dejó tools/publish.ps1,
  y escribe su SHA-256 junto al zip.
.EXAMPLE
  ./tools/publish.ps1 -Runtime win-x64 -Version 0.2.0
  ./tools/build-installer.ps1 -Runtime win-x64 -Version 0.2.0
#>
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string] $Runtime = 'win-x64',
    [string] $Version = '0.1.0'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $repo "artifacts/publish/$Runtime"
if (-not (Test-Path (Join-Path $publish 'Kumquat.exe'))) {
    throw "Falta ${publish}: corre primero ./tools/publish.ps1 -Runtime $Runtime -Version $Version"
}

# Las versiones de MSI son numéricas (mayor.menor.compilación): "0.2.0-beta.1" queda en 0.2.0.
$msiVersion = ($Version -split '[-+]')[0]
$platform = @{ 'win-x64' = 'x64'; 'win-arm64' = 'arm64' }[$Runtime]
$build = Join-Path $repo "artifacts/installer/$Runtime"
dotnet build (Join-Path $repo 'installer/Kumquat.Installer.wixproj') -c Release -nologo `
    "-p:PublishDir=$publish" "-p:ProductVersion=$msiVersion" "-p:InstallerPlatform=$platform" -o $build
if ($LASTEXITCODE -ne 0) { throw "WiX falló ($LASTEXITCODE)" }

$msi = Join-Path $repo "artifacts/Kumquat-$Version-$Runtime.msi"
Copy-Item -Force (Join-Path $build 'Kumquat.msi') $msi
$hash = (Get-FileHash -Algorithm SHA256 $msi).Hash.ToLowerInvariant()
"$hash  $(Split-Path -Leaf $msi)" | Set-Content -Encoding ascii -Path "$msi.sha256"
$size = [math]::Round((Get-Item $msi).Length / 1MB, 1)
Write-Host "[ok] $msi ($size MB) sha256=$hash"

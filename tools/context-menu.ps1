# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Kumquat contributors
<#
.SYNOPSIS
  "Kumquat…" en el menú contextual moderno de Windows 11 (paquete disperso, packaging/sparse).
.DESCRIPTION
  -Build       arma artifacts/sparse/<rid>/ (AppxManifest.xml y Kumquat.ContextMenu.msix); con
               -CertificateThumbprint lo firma y lo copia a la carpeta publicada (lo instala el MSI).
  -Register    registra el paquete apuntando a la carpeta de Kumquat (-InstallDir). Con -Dev usa el
               manifiesto sin firmar: solo funciona con el Modo de desarrollador de Windows.
  -Unregister  lo quita (el menú clásico sigue en "Mostrar más opciones").
.EXAMPLE
  ./tools/context-menu.ps1 -Build -Runtime win-x64 -Version 0.2.0
  ./tools/context-menu.ps1 -Register -Dev -InstallDir artifacts/publish/win-x64
  ./tools/context-menu.ps1 -Unregister
#>
[CmdletBinding(DefaultParameterSetName = 'Build')]
param(
    [Parameter(ParameterSetName = 'Build', Mandatory = $true)][switch] $Build,
    [Parameter(ParameterSetName = 'Register', Mandatory = $true)][switch] $Register,
    [Parameter(ParameterSetName = 'Unregister', Mandatory = $true)][switch] $Unregister,
    [Parameter(ParameterSetName = 'Build')][Parameter(ParameterSetName = 'Register')]
    [ValidateSet('win-x64', 'win-arm64')][string] $Runtime = 'win-x64',
    [Parameter(ParameterSetName = 'Build')][string] $Version = '0.2.0',
    # Sujeto del certificado que firma (tiene que coincidir con Publisher del manifiesto).
    [Parameter(ParameterSetName = 'Build')][string] $Publisher = 'CN=Kumquat contributors',
    [Parameter(ParameterSetName = 'Build')][string] $CertificateThumbprint,
    [Parameter(ParameterSetName = 'Register')][string] $InstallDir,
    [Parameter(ParameterSetName = 'Register')][switch] $Dev
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$packageName = 'Kumquat.ContextMenu'
$sparse = Join-Path $repo "artifacts/sparse/$Runtime"

function Find-SdkTool([string] $name) {
    $kits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    $arch = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } else { 'x64' }
    $tool = Get-ChildItem -Path $kits -Filter $name -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.Directory.Name -eq $arch } | Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $tool) { throw "No se encontró $name (Windows SDK)." }
    return $tool.FullName
}

if ($Unregister) {
    Get-AppxPackage -Name $packageName | Remove-AppxPackage
    Write-Host "[ok] $packageName quitado"
    return
}

if ($Build) {
    # Las versiones de paquete tienen 4 números: "0.2.0-beta.1" queda en 0.2.0.0.
    $numbers = @((($Version -split '[-+]')[0] -split '\.') + @('0', '0', '0', '0'))[0..3]
    $arch = @{ 'win-x64' = 'x64'; 'win-arm64' = 'arm64' }[$Runtime]
    New-Item -ItemType Directory -Force $sparse | Out-Null
    $manifest = (Get-Content -Raw -Encoding utf8 (Join-Path $repo 'packaging/sparse/AppxManifest.xml')).
        Replace('$Publisher$', [Security.SecurityElement]::Escape($Publisher)).
        Replace('$Version$', ($numbers -join '.')).
        Replace('$Architecture$', $arch)
    [IO.File]::WriteAllText((Join-Path $sparse 'AppxManifest.xml'), $manifest, [Text.UTF8Encoding]::new($false))

    # El paquete lleva solo el manifiesto y el logo: el resto está en la carpeta de Kumquat.
    $content = Join-Path $sparse 'content'
    New-Item -ItemType Directory -Force (Join-Path $content 'assets/logo') | Out-Null
    Copy-Item (Join-Path $sparse 'AppxManifest.xml') $content
    Copy-Item (Join-Path $repo 'assets/logo/kumquat.png') (Join-Path $content 'assets/logo')
    $msix = Join-Path $sparse "$packageName.msix"
    & (Find-SdkTool 'makeappx.exe') pack /o /d $content /p $msix /nv | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "makeappx falló ($LASTEXITCODE)" }

    if ($CertificateThumbprint) {
        & (Find-SdkTool 'signtool.exe') sign /fd SHA256 /sha1 $CertificateThumbprint /tr http://timestamp.digicert.com /td SHA256 $msix
        if ($LASTEXITCODE -ne 0) { throw "signtool falló ($LASTEXITCODE)" }
        Copy-Item $msix (Join-Path $repo "artifacts/publish/$Runtime")
        Write-Host "[ok] $msix firmado y copiado a la publicación"
    }
    else {
        Write-Host "[ok] $msix (sin firmar: para registrarlo hace falta el Modo de desarrollador, -Register -Dev)"
    }
    return
}

# -Register
if (-not $InstallDir) { throw 'Indica -InstallDir: la carpeta donde está Kumquat.exe.' }
$target = (Resolve-Path $InstallDir).Path
foreach ($file in 'Kumquat.exe', 'Kumquat.ShellExtension.dll') {
    if (-not (Test-Path (Join-Path $target $file))) { throw "Falta $file en $target." }
}

Get-AppxPackage -Name $packageName | Remove-AppxPackage
if ($Dev) {
    Add-AppxPackage -Register (Join-Path $sparse 'AppxManifest.xml') -ExternalLocation $target
}
else {
    Add-AppxPackage -Path (Join-Path $target "$packageName.msix") -ExternalLocation $target
}

Write-Host "[ok] $packageName registrado para $target"

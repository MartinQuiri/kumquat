# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Kumquat contributors
<#
.SYNOPSIS
  Descarga los motores nativos declarados en tools/engines.json, verifica su SHA-256
  y los extrae en engines/<id>/<rid>/. Es idempotente: si la versión ya está, no hace nada.

  En Linux y macOS, los motores sin binario para ese RID (MuPDF y Tesseract en Linux; todos en
  macOS) no se descargan: Kumquat usa los del gestor de paquetes, que encuentra en el PATH.
.EXAMPLE
  ./tools/fetch-engines.ps1 -Runtime win-x64
.EXAMPLE
  ./tools/fetch-engines.ps1 -Runtime linux-x64
#>
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64')]
    [string] $Runtime = $(
        $os = if ($IsLinux) { 'linux' } elseif ($IsMacOS) { 'osx' } else { 'win' }
        $arch = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'arm64' } else { 'x64' }
        "$os-$arch"
    ),
    [string[]] $Only = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue' # Invoke-WebRequest es 10x más lento con la barra de progreso.

$repo = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content -Raw -Path (Join-Path $PSScriptRoot 'engines.json') | ConvertFrom-Json
$cache = Join-Path $repo 'engines/.cache'
New-Item -ItemType Directory -Force -Path $cache | Out-Null

foreach ($engine in $manifest.engines) {
    if ($Only.Count -gt 0 -and $Only -notcontains $engine.id) { continue }

    $artifact = if ($engine.PSObject.Properties["artifacts"]) { $engine.artifacts.PSObject.Properties[$Runtime] } else { $null }
    $windows = $Runtime.StartsWith('win-')

    # Fuera de Windows, lo que no tiene binario propio lo pone el sistema (apt, dnf, Homebrew…).
    if (-not $windows -and $null -eq $artifact) {
        Write-Host "[--] $($engine.id): sin binario para $Runtime; se usa el del sistema (PATH)."
        continue
    }

    # Motores que se compilan desde el código fuente (sin binarios oficiales): su propio script.
    if ($engine.PSObject.Properties["build"]) {
        $built = Join-Path $repo "engines/$($engine.id)/$Runtime/.version"
        if ((Test-Path $built) -and ((Get-Content -Raw $built).Trim() -eq "$($engine.version) $($engine.recipe)")) {
            Write-Host "[ok] $($engine.id) $($engine.version) ($Runtime) ya está compilado."
        } else {
            & (Join-Path $repo $engine.build) -Runtime $Runtime
        }
        continue
    }

    if ($null -eq $artifact) { throw "El motor '$($engine.id)' no tiene artefacto para $Runtime." }
    $artifact = $artifact.Value

    # Artefactos que son código fuente (MuPDF en Linux): los compila su script, que verifica el hash.
    if ($artifact.PSObject.Properties['build']) {
        & (Join-Path $repo $artifact.build) -Runtime $Runtime
        continue
    }

    $target = Join-Path $repo "engines/$($engine.id)/$Runtime"
    $stamp = Join-Path $target '.version'
    if ((Test-Path $stamp) -and ((Get-Content -Raw $stamp).Trim() -eq "$($engine.version) $($artifact.sha256)")) {
        Write-Host "[ok] $($engine.id) $($engine.version) ($Runtime) ya está instalado."
        continue
    }

    $extension = if ($artifact.url.EndsWith('.tar.xz')) { '.tar.xz' } else { '.zip' }
    $zip = Join-Path $cache ("{0}-{1}{2}" -f $engine.id, $artifact.sha256.Substring(0, 12), $extension)
    if (-not (Test-Path $zip)) {
        Write-Host "[..] Descargando $($engine.id) $($engine.version) ($Runtime)..."
        $partial = "$zip.partial"
        Invoke-WebRequest -Uri $artifact.url -OutFile $partial -UseBasicParsing
        Move-Item -Force $partial $zip
    }

    $actual = (Get-FileHash -Algorithm SHA256 -Path $zip).Hash.ToLowerInvariant()
    if ($actual -ne $artifact.sha256.ToLowerInvariant()) {
        Remove-Item -Force $zip
        throw "SHA-256 no coincide para $($engine.id): esperado $($artifact.sha256), obtenido $actual. Descarga descartada."
    }

    $staging = Join-Path $cache ("extract-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $staging | Out-Null
    if ($windows -and $extension -eq '.zip') {
        Expand-Archive -Path $zip -DestinationPath $staging
    } else {
        # tar y unzip conservan el bit de ejecución y los enlaces simbólicos de las bibliotecas.
        if ($extension -eq '.zip') { & unzip -q $zip -d $staging } else { & tar -xJf $zip -C $staging }
        if ($LASTEXITCODE -ne 0) { throw "No se pudo extraer $zip" }
    }

    $prefix = $artifact.PSObject.Properties['stripPrefix']
    $content = if ($prefix) { Join-Path $staging $prefix.Value } else { $staging }

    if (Test-Path $target) { Remove-Item -Recurse -Force $target }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
    Move-Item -Path $content -Destination $target
    if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }

    # Aviso de licencia y origen junto al binario (cumplimiento GPL: oferta de código fuente).
    @(
        "$($engine.id) $($engine.version)"
        "Licencia: $($engine.license)"
        "Código fuente: $($engine.source)"
        "Scripts de build: $($engine.buildScripts)"
    ) | Set-Content -Encoding utf8 -Path (Join-Path $target 'SOURCE.txt')
    "$($engine.version) $($artifact.sha256)" | Set-Content -Encoding ascii -NoNewline -Path $stamp
    Write-Host "[ok] $($engine.id) instalado en $target"
}

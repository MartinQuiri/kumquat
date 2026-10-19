# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Kumquat contributors
<#
.SYNOPSIS
  Descarga los motores nativos declarados en tools/engines.json, verifica su SHA-256
  y los extrae en engines/<id>/<rid>/. Es idempotente: si la versión ya está, no hace nada.
.EXAMPLE
  ./tools/fetch-engines.ps1 -Runtime win-x64
#>
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string] $Runtime = $(if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'win-arm64' } else { 'win-x64' }),
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

    $artifact = $engine.artifacts.$Runtime
    if ($null -eq $artifact) { throw "El motor '$($engine.id)' no tiene artefacto para $Runtime." }

    $target = Join-Path $repo "engines/$($engine.id)/$Runtime"
    $stamp = Join-Path $target '.version'
    if ((Test-Path $stamp) -and ((Get-Content -Raw $stamp).Trim() -eq "$($engine.version) $($artifact.sha256)")) {
        Write-Host "[ok] $($engine.id) $($engine.version) ($Runtime) ya está instalado."
        continue
    }

    $zip = Join-Path $cache ("{0}-{1}.zip" -f $engine.id, $artifact.sha256.Substring(0, 12))
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
    Expand-Archive -Path $zip -DestinationPath $staging
    $content = if ($artifact.stripPrefix) { Join-Path $staging $artifact.stripPrefix } else { $staging }

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

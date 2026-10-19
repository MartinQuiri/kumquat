# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Kumquat contributors
<#
.SYNOPSIS
  Publica Kumquat (app de bandeja + CLI) self-contained y recortado para un RID,
  verifica que los motores carguen y genera el zip y su SHA-256.
.EXAMPLE
  ./tools/publish.ps1 -Runtime win-x64
#>
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string] $Runtime = 'win-x64',
    [string] $Version = '0.1.0'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$platform = if ($Runtime -eq 'win-arm64') { 'ARM64' } else { 'x64' }
$out = Join-Path $repo "artifacts/publish/$Runtime"
# La CLI va en su propia carpeta: cada ejecutable recortado (trimming) lleva su propia versión
# reducida del runtime de .NET; compartir carpeta hace que uno pise los ensamblados del otro.
$cliOut = Join-Path $out 'cli'
$zip = Join-Path $repo "artifacts/Kumquat-$Version-$Runtime.zip"

& (Join-Path $PSScriptRoot 'fetch-engines.ps1') -Runtime $Runtime

if (Test-Path $out) { Remove-Item -Recurse -Force $out }
foreach ($target in @(@{ Project = 'src/Kumquat.App'; Out = $out }, @{ Project = 'src/Kumquat.Cli'; Out = $cliOut })) {
    dotnet publish (Join-Path $repo $target.Project) -c Release -r $Runtime --self-contained `
        -p:Platform=$platform -p:Version=$Version -o $target.Out -nologo
    if ($LASTEXITCODE -ne 0) { throw "Falló la publicación de $($target.Project)" }
}

foreach ($doc in 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'README.md') {
    Copy-Item (Join-Path $repo $doc) $out
}

# Smoke tests (solo si el RID coincide con la máquina): la app debe ARRANCAR de verdad
# (motores, hooks, rueda, bandeja) y la CLI debe ver los motores.
if ($Runtime -eq "win-$($env:PROCESSOR_ARCHITECTURE.ToLowerInvariant() -replace 'amd64','x64')") {
    $app = Start-Process -FilePath (Join-Path $out 'Kumquat.exe') -ArgumentList '--smoke-test' -PassThru -Wait
    if ($app.ExitCode -ne 0) { throw "Kumquat.exe --smoke-test falló con código $($app.ExitCode) (ver visor de eventos / logs)." }
    Write-Host "[ok] Kumquat.exe arranca"

    $engines = & (Join-Path $cliOut 'kumquat-cli.exe') engines
    if ($engines -notmatch 'ffmpeg=ok' -or $engines -notmatch 'vips=ok') { throw "Motores no disponibles en la publicación: $engines" }
    Write-Host "[ok] $engines"
}

if (Test-Path $zip) { Remove-Item -Force $zip }
Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip -CompressionLevel Optimal
$hash = (Get-FileHash -Algorithm SHA256 $zip).Hash.ToLowerInvariant()
"$hash  $(Split-Path -Leaf $zip)" | Set-Content -Encoding ascii -Path "$zip.sha256"
$size = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "[ok] $zip ($size MB) sha256=$hash"

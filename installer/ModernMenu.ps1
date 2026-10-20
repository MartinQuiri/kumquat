# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Kumquat contributors
#
# Lo corre el instalador (sin consola, desde la carpeta de Kumquat): registra o quita "Kumquat…" del
# menú contextual moderno de Windows 11 (paquete disperso Kumquat.ContextMenu).
#   -Register    quita el anterior y registra Kumquat.ContextMenu.msix si vino con la instalación
#                (solo en releases firmados; sin él, queda el menú clásico).
#   -Unregister  lo quita. Windows cierra el proceso que tenía cargada la DLL (dllhost), así que
#                los archivos se pueden reemplazar o borrar sin reiniciar.
param([switch] $Register, [switch] $Unregister)

$ErrorActionPreference = 'Stop'
$name = 'Kumquat.ContextMenu'

Get-AppxPackage -Name $name | Remove-AppxPackage

if ($Register) {
    $msix = Join-Path $PSScriptRoot "$name.msix"
    if (Test-Path -LiteralPath $msix) {
        Add-AppxPackage -Path $msix -ExternalLocation $PSScriptRoot
    }
}

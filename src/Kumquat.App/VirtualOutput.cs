// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.App;

/// <summary>Adónde van los resultados de archivos virtuales y cómo limpiar sus copias temporales.</summary>
/// <param name="Directory">Carpeta de salida (la de Configuración).</param>
/// <param name="Suffix">Para el aviso: " en Escritorio".</param>
/// <param name="Cleanup">Borra las copias temporales (al terminar el lote o al cancelar).</param>
internal sealed record VirtualOutput(string Directory, string Suffix, Action Cleanup);

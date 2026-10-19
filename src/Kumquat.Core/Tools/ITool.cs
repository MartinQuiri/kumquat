// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Formats;
using Kumquat.Core.Results;

namespace Kumquat.Core.Tools;

/// <summary>
/// Herramienta que transforma uno o más archivos: comprimir, rotar, unir, extraer...
/// Aparece en el anillo medio de la rueda.
/// </summary>
public interface ITool
{
    /// <summary>Identificador estable (p. ej. <c>image.compress</c>).</summary>
    string Id { get; }

    /// <summary>Texto corto para la rueda (p. ej. "Comprimir").</summary>
    string Label { get; }

    /// <summary>Sufijo de la salida (p. ej. "comprimido" → <c>foto (comprimido).jpg</c>). Vacío = sin sufijo.</summary>
    string OutputSuffix { get; }

    /// <summary>Indica si el motor necesario está instalado.</summary>
    bool IsAvailable { get; }

    /// <summary>Uno a uno o combinando todos.</summary>
    ToolMode Mode => ToolMode.EachFile;

    /// <summary>Cantidad mínima de entradas para ofrecer la herramienta (p. ej. 2 para "Unir").</summary>
    int MinInputs => 1;

    /// <summary>Indica si acepta carpetas como entrada.</summary>
    bool AcceptsDirectories => false;

    /// <summary>Indica si la herramienta sirve para este formato.</summary>
    bool AppliesTo(FileFormat format);

    /// <summary>Formato del resultado para la (primera) entrada; <c>null</c> = la salida es una carpeta.</summary>
    FileFormat? OutputFormat(FileFormat? input);

    /// <summary>Aplica la herramienta.</summary>
    Task<Result> ApplyAsync(ToolStep step, IProgress<double>? progress, CancellationToken cancellationToken);
}

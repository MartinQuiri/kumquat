// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Formats;
using Kumquat.Core.Results;

namespace Kumquat.Core.Tools;

/// <summary>
/// Herramienta que transforma un archivo sin cambiar (necesariamente) su formato:
/// comprimir, rotar, quitar audio... Aparece en el anillo medio de la rueda.
/// </summary>
public interface ITool
{
    /// <summary>Identificador estable (p. ej. <c>image.compress</c>).</summary>
    string Id { get; }

    /// <summary>Texto corto para la rueda (p. ej. "Comprimir").</summary>
    string Label { get; }

    /// <summary>Sufijo del archivo de salida (p. ej. "comprimido" → <c>foto (comprimido).jpg</c>).</summary>
    string OutputSuffix { get; }

    /// <summary>Indica si el motor necesario está instalado.</summary>
    bool IsAvailable { get; }

    /// <summary>Indica si la herramienta sirve para este formato.</summary>
    bool AppliesTo(FileFormat format);

    /// <summary>Formato del resultado para una entrada dada.</summary>
    FileFormat OutputFormat(FileFormat input);

    /// <summary>Aplica la herramienta.</summary>
    Task<Result> ApplyAsync(ToolStep step, IProgress<double>? progress, CancellationToken cancellationToken);
}

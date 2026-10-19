// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Results;

namespace Kumquat.Core.Conversion;

/// <summary>
/// Adaptador de un motor de conversión. Cada motor (FFmpeg, libvips, PDFium...)
/// declara las aristas que soporta y las ejecuta.
/// </summary>
public interface IConverter
{
    /// <summary>Identificador estable del convertidor.</summary>
    string Id { get; }

    /// <summary>Indica si el motor está instalado y operativo.</summary>
    bool IsAvailable { get; }

    /// <summary>Conversiones directas que soporta.</summary>
    IReadOnlyCollection<ConversionEdge> Edges { get; }

    /// <summary>Ejecuta un paso de conversión.</summary>
    /// <param name="step">Paso a ejecutar; su arista pertenece a <see cref="Edges"/>.</param>
    /// <param name="progress">Avance entre 0 y 1, si el motor lo informa.</param>
    /// <param name="cancellationToken">Cancela la conversión y termina el proceso del motor.</param>
    Task<Result> ConvertAsync(ConversionStep step, IProgress<double>? progress, CancellationToken cancellationToken);
}

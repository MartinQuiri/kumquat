// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Formats;

namespace Kumquat.Core.Conversion;

/// <summary>Ruta de conversión: una o más aristas encadenadas.</summary>
/// <param name="Edges">Aristas en orden de ejecución.</param>
public sealed record ConversionPath(IReadOnlyList<ConversionEdge> Edges)
{
    /// <summary>Formato de origen.</summary>
    public FileFormat From => Edges[0].From;

    /// <summary>Formato de destino.</summary>
    public FileFormat To => Edges[^1].To;

    /// <summary>Costo total de la ruta.</summary>
    public double Cost => Edges.Sum(e => e.Cost);
}

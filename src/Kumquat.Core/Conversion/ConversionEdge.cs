// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Formats;

namespace Kumquat.Core.Conversion;

/// <summary>Una conversión directa que un convertidor sabe hacer.</summary>
/// <param name="From">Formato de entrada.</param>
/// <param name="To">Formato de salida.</param>
/// <param name="ConverterId">Convertidor que la implementa.</param>
/// <param name="Cost">
/// Costo relativo para el planificador: combina tiempo estimado y pérdida de calidad.
/// 1 es una conversión rápida sin pérdida; valores mayores desalientan la arista.
/// </param>
public sealed record ConversionEdge(FileFormat From, FileFormat To, string ConverterId, double Cost = 1.0);

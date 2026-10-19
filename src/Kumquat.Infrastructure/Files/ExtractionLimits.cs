// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Infrastructure.Files;

/// <summary>Límites de extracción para archivos comprimidos no confiables.</summary>
/// <param name="MaxEntries">Máximo de entradas.</param>
/// <param name="MaxTotalBytes">Máximo de bytes descomprimidos en total.</param>
/// <param name="MaxCompressionRatio">Razón máxima descomprimido/comprimido por entrada (anti zip-bomb).</param>
public sealed record ExtractionLimits(int MaxEntries = 10_000, long MaxTotalBytes = 8L * 1024 * 1024 * 1024, double MaxCompressionRatio = 200)
{
    /// <summary>Límites por defecto.</summary>
    public static ExtractionLimits Default { get; } = new();
}

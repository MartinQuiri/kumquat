// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Text.RegularExpressions;

namespace Kumquat.Core.Text;

/// <summary>
/// Rangos de páginas como los escribe una persona (<c>1-3, 5, 8-fin</c>), normalizados a la
/// sintaxis de qpdf (<c>1-3,5,8-z</c>). Solo números, rangos y "fin"/"z": nada más llega al motor.
/// </summary>
public static partial class PageRanges
{
    /// <summary>Normaliza; <c>false</c> si el texto no es un rango válido.</summary>
    public static bool TryNormalize(string? text, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var cleaned = Whitespace().Replace(text.ToLowerInvariant(), string.Empty)
            .Replace("fin", "z", StringComparison.Ordinal)
            .Replace("última", "z", StringComparison.Ordinal)
            .Replace("ultima", "z", StringComparison.Ordinal)
            .Replace(';', ',');
        if (!Valid().IsMatch(cleaned) || cleaned.Split(',', '-').Any(n => string.Equals(n, "0", StringComparison.Ordinal)))
        {
            return false;
        }

        normalized = cleaned;
        return true;
    }

    [GeneratedRegex(@"^(\d+|z)(-(\d+|z))?(,(\d+|z)(-(\d+|z))?)*$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 200)]
    private static partial Regex Valid();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 200)]
    private static partial Regex Whitespace();
}

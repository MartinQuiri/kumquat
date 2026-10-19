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

    /// <summary>
    /// Páginas (base 1) que cubre un rango normalizado en un documento de <paramref name="pageCount"/>
    /// páginas. Los rangos invertidos (<c>5-3</c>) cuentan como 3-5; lo que excede el total se ignora.
    /// </summary>
    public static SortedSet<int> Expand(string normalized, int pageCount)
    {
        ArgumentNullException.ThrowIfNull(normalized);
        var pages = new SortedSet<int>();
        foreach (var part in normalized.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var bounds = part.Split('-');
            var from = Page(bounds[0], pageCount);
            var to = bounds.Length > 1 ? Page(bounds[1], pageCount) : from;
            for (var page = Math.Max(1, Math.Min(from, to)); page <= Math.Min(pageCount, Math.Max(from, to)); page++)
            {
                pages.Add(page);
            }
        }

        return pages;
    }

    /// <summary>Texto compacto de un conjunto de páginas: <c>1-3, 5, 8-10</c>.</summary>
    public static string Describe(IEnumerable<int> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var parts = new List<string>();
        int? start = null;
        var previous = 0;
        foreach (var page in pages.Where(p => p > 0).Distinct().Order())
        {
            if (start is not null && page == previous + 1)
            {
                previous = page;
                continue;
            }

            if (start is not null)
            {
                parts.Add(Range(start.Value, previous));
            }

            start = previous = page;
        }

        if (start is not null)
        {
            parts.Add(Range(start.Value, previous));
        }

        return string.Join(", ", parts);

        static string Range(int from, int to) => from == to
            ? from.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{from}-{to}");
    }

    private static int Page(string token, int pageCount)
        => string.Equals(token, "z", StringComparison.Ordinal) ? pageCount
            : int.TryParse(token, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var page) ? page
            : int.MaxValue;

    [GeneratedRegex(@"^(\d+|z)(-(\d+|z))?(,(\d+|z)(-(\d+|z))?)*$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 200)]
    private static partial Regex Valid();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 200)]
    private static partial Regex Whitespace();
}

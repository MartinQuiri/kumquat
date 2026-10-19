// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Globalization;
using System.Text;

namespace Kumquat.Core.Text;

/// <summary>
/// Búsqueda del buscador de acciones: sin distinguir mayúsculas ni tildes ("cancion" encuentra
/// "Canción"), por prefijo ("pd" → PDF), por inicio de palabra ("ext pag" → "Extraer páginas"),
/// por palabras clave ("jpeg" → JPG) y, en último caso, por letras en orden ("cmpr" → "Comprimir").
/// Cada palabra de la búsqueda tiene que coincidir con algo.
/// </summary>
public static class CommandSearch
{
    private const int Exact = 1000;
    private const int Prefix = 800;
    private const int WordPrefix = 600;
    private const int Contains = 400;
    private const int Subsequence = 200;

    /// <summary>Texto comparable: minúsculas, sin tildes, espacios simples.</summary>
    public static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var space = false;
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsWhiteSpace(c) || c is '-' or '_' or '.' or '…')
            {
                space = builder.Length > 0;
                continue;
            }

            if (space)
            {
                builder.Append(' ');
                space = false;
            }

            builder.Append(c);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Puntaje de un elemento para la búsqueda (mayor es mejor); <c>-1</c> si no coincide. Con la
    /// búsqueda vacía todo coincide con 0.
    /// </summary>
    public static int Score(string query, string title, IEnumerable<string> keywords)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(keywords);
        var words = Normalize(query ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return 0;
        }

        var normalizedTitle = Normalize(title);
        string[] candidates = [normalizedTitle, .. keywords.Select(Normalize)];
        var total = 0;
        foreach (var word in words)
        {
            var best = candidates.Max(candidate => Match(word, candidate));
            if (best < 0)
            {
                return -1;
            }

            total += best;
        }

        // A igual coincidencia, gana lo que coincide en el título (lo que la persona ve).
        return total + (words.All(w => Match(w, normalizedTitle) >= 0) ? 50 : 0);
    }

    /// <summary>Filtra y ordena: mejor puntaje primero; a igual puntaje, el orden original.</summary>
    public static IReadOnlyList<T> Filter<T>(IReadOnlyList<T> items, string query, Func<T, string> title, Func<T, IEnumerable<string>> keywords)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(keywords);
        return [.. items
            .Select((item, index) => (Item: item, Index: index, Score: Score(query, title(item), keywords(item))))
            .Where(x => x.Score >= 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Index)
            .Select(x => x.Item)];
    }

    private static int Match(string word, string candidate)
    {
        if (candidate.Length == 0)
        {
            return -1;
        }

        if (string.Equals(candidate, word, StringComparison.Ordinal))
        {
            return Exact;
        }

        if (candidate.StartsWith(word, StringComparison.Ordinal))
        {
            return Prefix - Math.Min(100, candidate.Length - word.Length);
        }

        if (candidate.Split(' ').Any(part => part.StartsWith(word, StringComparison.Ordinal)))
        {
            return WordPrefix;
        }

        if (candidate.Contains(word, StringComparison.Ordinal))
        {
            return Contains;
        }

        return IsSubsequence(word, candidate) ? Subsequence : -1;
    }

    /// <summary>Las letras de la búsqueda aparecen en orden (no necesariamente juntas).</summary>
    private static bool IsSubsequence(string word, string candidate)
    {
        // Solo desde 3 letras: con 1 o 2, casi todo coincidiría.
        if (word.Length < 3)
        {
            return false;
        }

        var i = 0;
        foreach (var c in candidate)
        {
            if (i < word.Length && c == word[i])
            {
                i++;
            }
        }

        return i == word.Length;
    }
}

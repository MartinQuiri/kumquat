// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Core.Text;

/// <summary>
/// Orden "natural" como el del Explorador: <c>foto2</c> antes que <c>foto10</c>. Decide el
/// orden al unir archivos, así que tiene que coincidir con lo que el usuario ve en la carpeta.
/// </summary>
public sealed class NaturalComparer : IComparer<string>
{
    /// <summary>Instancia compartida (sin distinguir mayúsculas).</summary>
    public static NaturalComparer Instance { get; } = new();

    /// <inheritdoc />
    public int Compare(string? x, string? y)
    {
        if (x is null || y is null)
        {
            return x is null ? (y is null ? 0 : -1) : 1;
        }

        int i = 0, j = 0;
        while (i < x.Length && j < y.Length)
        {
            int order;
            if (char.IsAsciiDigit(x[i]) && char.IsAsciiDigit(y[j]))
            {
                order = CompareNumbers(x, ref i, y, ref j);
            }
            else
            {
                order = char.ToUpperInvariant(x[i]).CompareTo(char.ToUpperInvariant(y[j]));
                i++;
                j++;
            }

            if (order != 0)
            {
                return order;
            }
        }

        return (x.Length - i).CompareTo(y.Length - j);
    }

    /// <summary>Compara las secuencias de dígitos que empiezan en i y j por su valor (sin límite de tamaño).</summary>
    private static int CompareNumbers(string x, ref int i, string y, ref int j)
    {
        var numberX = TakeDigits(x, ref i).TrimStart('0');
        var numberY = TakeDigits(y, ref j).TrimStart('0');
        var byLength = numberX.Length.CompareTo(numberY.Length);
        return byLength != 0 ? byLength : numberX.CompareTo(numberY, StringComparison.Ordinal);
    }

    private static ReadOnlySpan<char> TakeDigits(string s, ref int index)
    {
        var start = index;
        while (index < s.Length && char.IsAsciiDigit(s[index]))
        {
            index++;
        }

        return s.AsSpan(start, index - start);
    }
}

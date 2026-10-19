// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Globalization;

namespace Kumquat.Core.Text;

/// <summary>Tamaño de archivo legible, como lo muestra el Explorador (base 1024, un decimal bajo 10).</summary>
public static class FileSizeText
{
    private static readonly string[] Units = ["KB", "MB", "GB", "TB"];

    /// <summary>Formatea un tamaño: <c>820 bytes</c>, <c>12 KB</c>, <c>2,4 MB</c>, <c>1,2 GB</c>.</summary>
    public static string Format(long bytes, CultureInfo? culture = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        culture ??= CultureInfo.CurrentCulture;
        if (bytes < 1024)
        {
            return bytes == 1 ? "1 byte" : string.Create(culture, $"{bytes} bytes");
        }

        var value = (double)bytes;
        var unit = -1;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        // Un decimal solo cuando aporta (2,4 MB); desde 10 basta el entero (24 MB).
        var rounded = value < 10 ? Math.Round(value, 1, MidpointRounding.AwayFromZero) : Math.Round(value, 0, MidpointRounding.AwayFromZero);
        return string.Create(culture, $"{rounded.ToString(rounded < 10 ? "0.#" : "0", culture)} {Units[unit]}");
    }
}

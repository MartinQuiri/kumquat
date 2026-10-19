// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Formats;

namespace Kumquat.Interaction.Wheel;

/// <summary>
/// Decide qué destinos entran en la rueda y en qué posición. Las posiciones son estables
/// para un mismo tipo de origen, para que el usuario forme memoria muscular.
/// </summary>
public static class WheelItemOrdering
{
    /// <summary>Destinos preferidos por familia, de más a menos usado; los primeros caen en los ejes.</summary>
    private static readonly Dictionary<FormatCategory, string[]> Preferred = new()
    {
        [FormatCategory.Image] = ["jpg", "png", "webp", "pdf", "avif", "gif", "tiff", "bmp"],
        [FormatCategory.Audio] = ["mp3", "m4a", "wav", "flac", "ogg", "opus", "aiff", "wma"],
        [FormatCategory.Video] = ["mp4", "mp3", "gif", "webm", "mov", "mkv", "m4a", "avi", "wav", "wmv"],
        [FormatCategory.Pdf] = ["docx", "jpg", "png", "txt", "md", "epub", "pptx", "xlsx"],
        [FormatCategory.Document] = ["pdf", "docx", "md", "txt", "html", "epub", "rtf", "odt"],
        [FormatCategory.Spreadsheet] = ["xlsx", "csv", "pdf", "ods", "tsv"],
        [FormatCategory.Slides] = ["pdf", "pptx", "jpg", "png"],
        [FormatCategory.Ebook] = ["pdf", "docx", "md", "txt", "html"],
        [FormatCategory.Subtitle] = ["srt", "vtt", "txt"],
        [FormatCategory.Archive] = ["zip", "tar", "gz"],
    };

    /// <summary>
    /// Ordena y recorta los destinos. Orden de los ejes en una rueda de 8:
    /// norte(0), este(2), sur(4), oeste(6), y luego las diagonales.
    /// </summary>
    /// <param name="sourceCategory">Familia del origen (decide la preferencia).</param>
    /// <param name="targets">Destinos disponibles según el grafo.</param>
    /// <param name="max">Cantidad máxima de sectores.</param>
    public static IReadOnlyList<WheelItem> Arrange(FormatCategory sourceCategory, IReadOnlyCollection<FileFormat> targets, int max = WheelLayout.MaxSectors)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentOutOfRangeException.ThrowIfLessThan(max, 1);

        var preferred = Preferred.GetValueOrDefault(sourceCategory, []);
        var ranked = targets
            .Distinct()
            .OrderBy(t => Array.IndexOf(preferred, t.Id) is var i and >= 0 ? i : int.MaxValue)
            .ThenBy(t => t.Category == sourceCategory ? 0 : 1)
            .ThenBy(t => t.Id, StringComparer.Ordinal)
            .Take(max)
            .ToList();

        if (ranked.Count != WheelLayout.MaxSectors)
        {
            return [.. ranked.Select(ToItem)];
        }

        // Con 8 sectores, los 4 favoritos van a los ejes cardinales y el resto a las diagonales.
        int[] slots = [0, 2, 4, 6, 1, 3, 5, 7];
        var items = new WheelItem[WheelLayout.MaxSectors];
        for (var i = 0; i < ranked.Count; i++)
        {
            items[slots[i]] = ToItem(ranked[i]);
        }

        return items;
    }

    private static WheelItem ToItem(FileFormat format) => new(format.DisplayName, format);
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Formats;

namespace Kumquat.Core.Conversion;

/// <summary>
/// Qué destinos tienen sentido para cada familia de origen. El grafo puede encadenar
/// PNG→GIF→MP4→MP3, pero un MP3 hecho de una foto no es algo que alguien quiera:
/// esta política filtra los destinos finales (los intermedios son libres).
/// </summary>
public static class ConversionPolicy
{
    private static readonly Dictionary<FormatCategory, FormatCategory[]> Allowed = new()
    {
        [FormatCategory.Image] = [FormatCategory.Image, FormatCategory.Pdf],
        [FormatCategory.Audio] = [FormatCategory.Audio],
        [FormatCategory.Video] = [FormatCategory.Video, FormatCategory.Audio],
        [FormatCategory.Pdf] = [FormatCategory.Pdf, FormatCategory.Image, FormatCategory.Document, FormatCategory.Ebook, FormatCategory.Slides, FormatCategory.Spreadsheet],
        [FormatCategory.Document] = [FormatCategory.Document, FormatCategory.Pdf, FormatCategory.Ebook],
        [FormatCategory.Spreadsheet] = [FormatCategory.Spreadsheet, FormatCategory.Pdf],
        [FormatCategory.Slides] = [FormatCategory.Slides, FormatCategory.Pdf, FormatCategory.Image],
        [FormatCategory.Ebook] = [FormatCategory.Ebook, FormatCategory.Document, FormatCategory.Pdf],
        [FormatCategory.Subtitle] = [FormatCategory.Subtitle, FormatCategory.Document],
        [FormatCategory.Archive] = [FormatCategory.Archive],
    };

    /// <summary>Indica si convertir <paramref name="source"/> en <paramref name="target"/> es razonable.</summary>
    public static bool IsMeaningful(FileFormat source, FileFormat target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        // Excepciones explícitas: el GIF es imagen y video a la vez.
        if (source == KnownFormats.Gif && target.Category == FormatCategory.Video)
        {
            return true;
        }

        if (source.Category == FormatCategory.Video && target == KnownFormats.Gif)
        {
            return true;
        }

        return Allowed.TryGetValue(source.Category, out var targets) && targets.Contains(target.Category);
    }
}

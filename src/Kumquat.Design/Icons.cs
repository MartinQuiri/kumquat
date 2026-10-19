// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Collections.Concurrent;
using Kumquat.Core.Formats;

namespace Kumquat.Design;

/// <summary>
/// Ícono de cada herramienta, acción y familia de formatos. Los trazados son de Phosphor
/// (viewBox 256×256, relleno con regla no-cero); el nombre lógico desacopla el resto del código
/// del set de íconos.
/// </summary>
public static class Icons
{
    /// <summary>Lado del viewBox de los trazados.</summary>
    public const double ViewBox = 256;

    private static readonly ConcurrentDictionary<string, IReadOnlyList<PathFigure>> Parsed = new(StringComparer.Ordinal);

    private static readonly Dictionary<string, string> ByItem = new(StringComparer.Ordinal)
    {
        ["image.compress"] = "compress",
        ["pdf.compress"] = "compress",
        ["video.compress"] = "compress",
        ["image.rotate"] = "rotate",
        ["pdf.rotate"] = "rotate",
        ["video.rotate"] = "rotate",
        ["image.flip"] = "flip",
        ["image.fit"] = "fit",
        ["image.gray"] = "gray",
        ["image.metadata"] = "metadata",
        ["pdf.metadata"] = "metadata",
        ["media.metadata"] = "metadata",
        ["office.metadata"] = "metadata",
        ["image.strip"] = "strip",
        ["image.to-pdf"] = "to-pdf",
        ["pdf.merge"] = "merge",
        ["pdf.split"] = "split",
        ["pdf.to-jpg"] = "pages-to-images",
        ["pdf.extract"] = "extract-pages",
        ["pdf.protect"] = "protect",
        ["pdf.unlock"] = "unlock",
        ["video.mute"] = "mute",
        ["audio.normalize"] = "normalize",
        ["video.frame"] = "frame",
        ["video.merge"] = "merge-video",
        ["audio.merge"] = "merge-audio",
        ["media.trim"] = "trim",
        ["archive.zip"] = "zip",
        ["archive.extract"] = "extract",
        ["image.ocr-text"] = "ocr-text",
        ["image.ocr-pdf"] = "ocr-pdf",
        ["pdf.ocr"] = "ocr",
        ["action.copy-path"] = "copy-path",
        ["action.copy-to"] = "copy-to",
        ["action.move-to"] = "move-to",
        ["action.show-in-folder"] = "show-in-folder",
        ["action.terminal"] = "terminal",
    };

    /// <summary>Nombres lógicos disponibles.</summary>
    public static IEnumerable<string> Names => IconPaths.Names;

    /// <summary>Ícono de una herramienta o acción por su id, o <c>null</c> si no tiene.</summary>
    public static string? ForItem(string id) => ByItem.GetValueOrDefault(id);

    /// <summary>Ícono de una familia de formatos.</summary>
    public static string ForCategory(FormatCategory category) => category switch
    {
        FormatCategory.Image => "image",
        FormatCategory.Audio => "audio",
        FormatCategory.Video => "video",
        FormatCategory.Pdf => "pdf",
        FormatCategory.Spreadsheet => "spreadsheet",
        FormatCategory.Slides => "slides",
        FormatCategory.Ebook => "ebook",
        FormatCategory.Subtitle => "subtitle",
        FormatCategory.Archive => "archive",
        FormatCategory.Document => "document",
        _ => "text",
    };

    /// <summary>Trazado interpretado de un ícono (en caché). Lanza si el nombre no existe.</summary>
    public static IReadOnlyList<PathFigure> Figures(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Parsed.GetOrAdd(name, static n => SvgPath.Parse(IconPaths.Get(n) ?? throw new KeyNotFoundException($"Ícono desconocido: {n}.")));
    }
}

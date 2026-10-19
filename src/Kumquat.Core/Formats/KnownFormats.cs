// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Collections.Frozen;

namespace Kumquat.Core.Formats;

/// <summary>Catálogo de formatos que Kumquat reconoce.</summary>
public static class KnownFormats
{
#pragma warning disable CS1591 // Los nombres son autoexplicativos.
    public static readonly FileFormat Jpg = new("jpg", "JPG", FormatCategory.Image, ["jpg", "jpeg", "jfif"]);
    public static readonly FileFormat Png = new("png", "PNG", FormatCategory.Image, ["png"]);
    public static readonly FileFormat WebP = new("webp", "WebP", FormatCategory.Image, ["webp"]);
    public static readonly FileFormat Gif = new("gif", "GIF", FormatCategory.Image, ["gif"]);
    public static readonly FileFormat Heic = new("heic", "HEIC", FormatCategory.Image, ["heic", "heif"]);
    public static readonly FileFormat Avif = new("avif", "AVIF", FormatCategory.Image, ["avif"]);
    public static readonly FileFormat Tiff = new("tiff", "TIFF", FormatCategory.Image, ["tiff", "tif"]);
    public static readonly FileFormat Bmp = new("bmp", "BMP", FormatCategory.Image, ["bmp"]);
    public static readonly FileFormat Svg = new("svg", "SVG", FormatCategory.Image, ["svg"]);
    public static readonly FileFormat Ico = new("ico", "ICO", FormatCategory.Image, ["ico"]);

    public static readonly FileFormat Mp3 = new("mp3", "MP3", FormatCategory.Audio, ["mp3"]);
    public static readonly FileFormat M4a = new("m4a", "M4A", FormatCategory.Audio, ["m4a"]);
    public static readonly FileFormat Wav = new("wav", "WAV", FormatCategory.Audio, ["wav"]);
    public static readonly FileFormat Flac = new("flac", "FLAC", FormatCategory.Audio, ["flac"]);
    public static readonly FileFormat Ogg = new("ogg", "OGG", FormatCategory.Audio, ["ogg", "oga"]);
    public static readonly FileFormat Opus = new("opus", "Opus", FormatCategory.Audio, ["opus"]);
    public static readonly FileFormat Aiff = new("aiff", "AIFF", FormatCategory.Audio, ["aiff", "aif", "aifc"]);
    public static readonly FileFormat Wma = new("wma", "WMA", FormatCategory.Audio, ["wma"]);

    public static readonly FileFormat Mp4 = new("mp4", "MP4", FormatCategory.Video, ["mp4", "m4v"]);
    public static readonly FileFormat Mov = new("mov", "MOV", FormatCategory.Video, ["mov", "qt"]);
    public static readonly FileFormat Mkv = new("mkv", "MKV", FormatCategory.Video, ["mkv"]);
    public static readonly FileFormat WebM = new("webm", "WebM", FormatCategory.Video, ["webm"]);
    public static readonly FileFormat Avi = new("avi", "AVI", FormatCategory.Video, ["avi"]);
    public static readonly FileFormat Wmv = new("wmv", "WMV", FormatCategory.Video, ["wmv"]);

    public static readonly FileFormat Pdf = new("pdf", "PDF", FormatCategory.Pdf, ["pdf"]);

    public static readonly FileFormat Docx = new("docx", "DOCX", FormatCategory.Document, ["docx"]);
    public static readonly FileFormat Odt = new("odt", "ODT", FormatCategory.Document, ["odt"]);
    public static readonly FileFormat Rtf = new("rtf", "RTF", FormatCategory.Document, ["rtf"]);
    public static readonly FileFormat Markdown = new("md", "Markdown", FormatCategory.Document, ["md", "markdown"]);
    public static readonly FileFormat Txt = new("txt", "TXT", FormatCategory.Document, ["txt"]);
    public static readonly FileFormat Html = new("html", "HTML", FormatCategory.Document, ["html", "htm"]);

    public static readonly FileFormat Xlsx = new("xlsx", "XLSX", FormatCategory.Spreadsheet, ["xlsx"]);
    public static readonly FileFormat Xls = new("xls", "XLS", FormatCategory.Spreadsheet, ["xls"]);
    public static readonly FileFormat Ods = new("ods", "ODS", FormatCategory.Spreadsheet, ["ods"]);
    public static readonly FileFormat Csv = new("csv", "CSV", FormatCategory.Spreadsheet, ["csv"]);
    public static readonly FileFormat Tsv = new("tsv", "TSV", FormatCategory.Spreadsheet, ["tsv"]);

    public static readonly FileFormat Pptx = new("pptx", "PPTX", FormatCategory.Slides, ["pptx"]);
    public static readonly FileFormat Ppt = new("ppt", "PPT", FormatCategory.Slides, ["ppt"]);

    public static readonly FileFormat Epub = new("epub", "EPUB", FormatCategory.Ebook, ["epub"]);

    public static readonly FileFormat Srt = new("srt", "SRT", FormatCategory.Subtitle, ["srt"]);
    public static readonly FileFormat Vtt = new("vtt", "VTT", FormatCategory.Subtitle, ["vtt"]);

    public static readonly FileFormat Zip = new("zip", "ZIP", FormatCategory.Archive, ["zip"]);
    public static readonly FileFormat Tar = new("tar", "TAR", FormatCategory.Archive, ["tar"]);
    public static readonly FileFormat Gzip = new("gz", "GZIP", FormatCategory.Archive, ["gz", "tgz"]);
    public static readonly FileFormat Rar = new("rar", "RAR", FormatCategory.Archive, ["rar"]);
    public static readonly FileFormat SevenZip = new("7z", "7Z", FormatCategory.Archive, ["7z"]);
#pragma warning restore CS1591

    private static readonly FrozenDictionary<string, FileFormat> ById;
    private static readonly FrozenDictionary<string, FileFormat> ByExtension;

    static KnownFormats()
    {
        All =
        [
            Jpg, Png, WebP, Gif, Heic, Avif, Tiff, Bmp, Svg, Ico,
            Mp3, M4a, Wav, Flac, Ogg, Opus, Aiff, Wma,
            Mp4, Mov, Mkv, WebM, Avi, Wmv,
            Pdf,
            Docx, Odt, Rtf, Markdown, Txt, Html,
            Xlsx, Xls, Ods, Csv, Tsv,
            Pptx, Ppt,
            Epub,
            Srt, Vtt,
            Zip, Tar, Gzip, Rar, SevenZip,
        ];

        ById = All.ToFrozenDictionary(f => f.Id, StringComparer.Ordinal);
        ByExtension = All
            .SelectMany(f => f.Extensions.Select(e => (Extension: e, Format: f)))
            .ToFrozenDictionary(p => p.Extension, p => p.Format, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Todos los formatos conocidos.</summary>
    public static IReadOnlyList<FileFormat> All { get; }

    /// <summary>Busca un formato por su identificador.</summary>
    public static FileFormat? FindById(string id) => ById.GetValueOrDefault(id);

    /// <summary>Busca un formato por extensión, con o sin punto inicial.</summary>
    public static FileFormat? FindByExtension(string extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        return ByExtension.GetValueOrDefault(extension.TrimStart('.'));
    }
}

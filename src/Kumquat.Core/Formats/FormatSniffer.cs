// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Text.Unicode;

namespace Kumquat.Core.Formats;

/// <summary>
/// Detecta el formato real de un archivo por sus bytes mágicos. La extensión solo
/// se usa para desambiguar dentro de una misma familia (p. ej. ZIP vs DOCX) o
/// cuando el contenido no tiene firma reconocible (texto plano, CSV...).
/// </summary>
public static class FormatSniffer
{
    /// <summary>Bytes de cabecera que el detector necesita como máximo.</summary>
    public const int HeaderLength = 1024;

    private static readonly FileFormat[] ZipFamily =
        [KnownFormats.Zip, KnownFormats.Docx, KnownFormats.Xlsx, KnownFormats.Pptx, KnownFormats.Epub, KnownFormats.Odt, KnownFormats.Ods, KnownFormats.Odp];

    private static readonly FileFormat[] AsfFamily = [KnownFormats.Wmv, KnownFormats.Wma];

    private static readonly FileFormat[] OleFamily = [KnownFormats.Doc, KnownFormats.Xls, KnownFormats.Ppt];

    private static readonly FileFormat[] MobiFamily = [KnownFormats.Mobi, KnownFormats.Azw3];

    private static readonly FileFormat[] IsoBmffFamily = [KnownFormats.Mp4, KnownFormats.M4a, KnownFormats.Mov];

    private static readonly FileFormat[] TextFamily =
        [KnownFormats.Txt, KnownFormats.Markdown, KnownFormats.Csv, KnownFormats.Tsv, KnownFormats.Html, KnownFormats.Srt, KnownFormats.Svg, KnownFormats.Vtt];

    private delegate FileFormat? SignatureGroup(ReadOnlySpan<byte> header, FileFormat? byExtension);

    private static ReadOnlySpan<byte> PngMagic => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static ReadOnlySpan<byte> JpegMagic => [0xFF, 0xD8, 0xFF];

    private static ReadOnlySpan<byte> IcoMagic => [0x00, 0x00, 0x01, 0x00];

    private static ReadOnlySpan<byte> EbmlMagic => [0x1A, 0x45, 0xDF, 0xA3];

    private static ReadOnlySpan<byte> AsfMagic => [0x30, 0x26, 0xB2, 0x75, 0x8E, 0x66, 0xCF, 0x11];

    private static ReadOnlySpan<byte> OleMagic => [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    private static ReadOnlySpan<byte> SevenZipMagic => [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C];

    private static ReadOnlySpan<byte> GzipMagic => [0x1F, 0x8B];

    private static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];

    /// <summary>Detecta el formato de un archivo en disco.</summary>
    /// <returns>El formato, o <c>null</c> si no se reconoce.</returns>
    public static FileFormat? DetectFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        Span<byte> buffer = stackalloc byte[HeaderLength];
        int read;
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize: 1, FileOptions.None))
        {
            read = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
        }

        return Detect(buffer[..read], Path.GetExtension(path));
    }

    /// <summary>Detecta el formato a partir de la cabecera y la extensión.</summary>
    /// <param name="header">Primeros bytes del archivo (idealmente <see cref="HeaderLength"/>).</param>
    /// <param name="extension">Extensión del nombre, con o sin punto; puede ser vacía.</param>
    /// <returns>El formato, o <c>null</c> si no se reconoce.</returns>
    public static FileFormat? Detect(ReadOnlySpan<byte> header, string? extension)
    {
        var byExtension = string.IsNullOrEmpty(extension) ? null : KnownFormats.FindByExtension(extension);

        SignatureGroup[] groups = [DetectImage, DetectAudio, DetectContainer, DetectDocument];
        foreach (var group in groups)
        {
            var found = group(header, byExtension);
            if (found is not null)
            {
                return found;
            }
        }

        // Sin firma: solo se confía en la extensión si es un formato de texto y el contenido lo parece.
        return byExtension is not null && TextFamily.Contains(byExtension) && LooksLikeText(header) ? byExtension : null;
    }

    private static FileFormat? DetectImage(ReadOnlySpan<byte> h, FileFormat? byExtension)
    {
        if (h.StartsWith(PngMagic))
        {
            return KnownFormats.Png;
        }

        if (h.StartsWith(JpegMagic))
        {
            return KnownFormats.Jpg;
        }

        if (h.StartsWith("GIF87a"u8) || h.StartsWith("GIF89a"u8))
        {
            return KnownFormats.Gif;
        }

        if (IsRiff(h, "WEBP"u8))
        {
            return KnownFormats.WebP;
        }

        if (h.StartsWith("BM"u8) && h.Length >= 14)
        {
            return KnownFormats.Bmp;
        }

        if (h.StartsWith("II*\0"u8) || h.StartsWith("MM\0*"u8))
        {
            return KnownFormats.Tiff;
        }

        // La firma ICO es demasiado genérica: se exige además la extensión.
        return h.StartsWith(IcoMagic) && byExtension == KnownFormats.Ico ? KnownFormats.Ico : null;
    }

    private static FileFormat? DetectAudio(ReadOnlySpan<byte> h, FileFormat? byExtension)
    {
        if (IsRiff(h, "WAVE"u8))
        {
            return KnownFormats.Wav;
        }

        if (h.StartsWith("FORM"u8) && h.Length >= 12 && (h[8..12].SequenceEqual("AIFF"u8) || h[8..12].SequenceEqual("AIFC"u8)))
        {
            return KnownFormats.Aiff;
        }

        if (h.StartsWith("fLaC"u8))
        {
            return KnownFormats.Flac;
        }

        if (h.StartsWith("OggS"u8))
        {
            return h.IndexOf("OpusHead"u8) >= 0 ? KnownFormats.Opus : KnownFormats.Ogg;
        }

        // ID3, o frame sync MPEG Layer III (11 bits a 1 + layer 01). AAC ADTS tiene layer 00.
        if (h.StartsWith("ID3"u8) || (h.Length >= 2 && h[0] == 0xFF && (h[1] & 0xE6) == 0xE2))
        {
            return KnownFormats.Mp3;
        }

        return null;
    }

    private static FileFormat? DetectContainer(ReadOnlySpan<byte> h, FileFormat? byExtension)
    {
        if (IsRiff(h, "AVI "u8))
        {
            return KnownFormats.Avi;
        }

        if (h.StartsWith(EbmlMagic))
        {
            return h.IndexOf("webm"u8) >= 0 ? KnownFormats.WebM : KnownFormats.Mkv;
        }

        if (h.StartsWith(AsfMagic))
        {
            return PreferExtension(AsfFamily, byExtension);
        }

        if (h.Length >= 12 && h[4..8].SequenceEqual("ftyp"u8))
        {
            return DetectIsoBmff(h, byExtension);
        }

        if (h.StartsWith("PK\x03\x04"u8))
        {
            return PreferExtension(ZipFamily, byExtension);
        }

        if (h.StartsWith(OleMagic))
        {
            // Contenedor OLE2 (Office 97-2003): solo la extensión distingue XLS de PPT.
            return byExtension is not null && OleFamily.Contains(byExtension) ? byExtension : null;
        }

        if (h.StartsWith("Rar!\x1A\x07"u8))
        {
            return KnownFormats.Rar;
        }

        if (h.StartsWith(SevenZipMagic))
        {
            return KnownFormats.SevenZip;
        }

        if (h.StartsWith(GzipMagic))
        {
            return KnownFormats.Gzip;
        }

        return h.Length >= 262 && h[257..262].SequenceEqual("ustar"u8) ? KnownFormats.Tar : null;
    }

    private static FileFormat? DetectDocument(ReadOnlySpan<byte> h, FileFormat? byExtension)
    {
        if (h.StartsWith("%PDF-"u8))
        {
            return KnownFormats.Pdf;
        }

        if (h.StartsWith(@"{\rtf"u8))
        {
            return KnownFormats.Rtf;
        }

        // Kindle (base de datos Palm): "BOOKMOBI" en el byte 60.
        if (h.Length >= 68 && h[60..68].SequenceEqual("BOOKMOBI"u8))
        {
            return DetectMobi(h, byExtension);
        }

        var text = SkipBom(h);
        if (text.StartsWith("WEBVTT"u8))
        {
            return KnownFormats.Vtt;
        }

        if (!LooksLikeText(h) || !text.TrimStart(" \t\r\n"u8).StartsWith("<"u8))
        {
            return null;
        }

        return text.IndexOf("<FictionBook"u8) >= 0 ? KnownFormats.Fb2
            : ContainsSvgRoot(text) ? KnownFormats.Svg
            : null;
    }

    /// <summary>
    /// MOBI y AZW3 comparten contenedor; la versión del encabezado MOBI dice cuál es (8 = KF8, AZW3).
    /// Si el primer registro no entra en la cabecera leída, decide la extensión.
    /// </summary>
    private static FileFormat DetectMobi(ReadOnlySpan<byte> h, FileFormat? byExtension)
    {
        if (h.Length >= 82)
        {
            var record = (int)Math.Min(System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(h[78..82]), int.MaxValue);
            if (record <= h.Length - 40 && h[(record + 16)..(record + 20)].SequenceEqual("MOBI"u8))
            {
                return System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(h[(record + 36)..(record + 40)]) == 8 ? KnownFormats.Azw3 : KnownFormats.Mobi;
            }
        }

        return PreferExtension(MobiFamily, byExtension);
    }

    private static FileFormat DetectIsoBmff(ReadOnlySpan<byte> h, FileFormat? byExtension)
    {
        var major = h[8..12];
        if (major.SequenceEqual("heic"u8) || major.SequenceEqual("heix"u8) || major.SequenceEqual("hevc"u8) || major.SequenceEqual("hevx"u8)
            || major.SequenceEqual("heim"u8) || major.SequenceEqual("heis"u8))
        {
            return KnownFormats.Heic;
        }

        if (major.SequenceEqual("avif"u8) || major.SequenceEqual("avis"u8))
        {
            return KnownFormats.Avif;
        }

        if (major.SequenceEqual("mif1"u8) || major.SequenceEqual("msf1"u8))
        {
            // Marca genérica HEIF: AVIF la declara entre las marcas compatibles.
            return h[..Math.Min(h.Length, 64)].IndexOf("avif"u8) >= 0 ? KnownFormats.Avif : KnownFormats.Heic;
        }

        if (major.SequenceEqual("qt  "u8))
        {
            return KnownFormats.Mov;
        }

        if (major.SequenceEqual("M4A "u8) || major.SequenceEqual("M4B "u8))
        {
            return KnownFormats.M4a;
        }

        return PreferExtension(IsoBmffFamily, byExtension);
    }

    private static bool IsRiff(ReadOnlySpan<byte> h, ReadOnlySpan<byte> kind)
        => h.Length >= 12 && h.StartsWith("RIFF"u8) && h[8..12].SequenceEqual(kind);

    private static FileFormat PreferExtension(FileFormat[] family, FileFormat? byExtension)
        => byExtension is not null && family.Contains(byExtension) ? byExtension : family[0];

    private static ReadOnlySpan<byte> SkipBom(ReadOnlySpan<byte> h)
        => h.StartsWith(Utf8Bom) ? h[3..] : h;

    private static bool ContainsSvgRoot(ReadOnlySpan<byte> text)
        => text.TrimStart(" \t\r\n"u8).StartsWith("<"u8) && text.IndexOf("<svg"u8) >= 0;

    /// <summary>Heurística: UTF-8 válido y sin bytes de control salvo tab/CR/LF.</summary>
    private static bool LooksLikeText(ReadOnlySpan<byte> h)
    {
        var body = SkipBom(h);
        foreach (var b in body)
        {
            if (b < 0x20 && b is not (byte)'\t' and not (byte)'\n' and not (byte)'\r')
            {
                return false;
            }
        }

        // La cabecera puede cortar un carácter multibyte al final: se toleran hasta 3 bytes.
        for (var cut = 0; cut <= Math.Min(3, body.Length); cut++)
        {
            if (Utf8.IsValid(body[..^cut]))
            {
                return true;
            }
        }

        return false;
    }
}

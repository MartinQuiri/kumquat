// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Text;
using CsCheck;
using Kumquat.Core.Formats;

namespace Kumquat.Core.Tests.Formats;

public sealed class FormatSnifferTests
{
    public static TheoryData<string, string, string> Signatures => new()
    {
        { "png", "89504E470D0A1A0A0000000D49484452", ".png" },
        { "jpg", "FFD8FFE000104A464946", ".jpg" },
        { "gif", Hex("GIF89a") + "0100", ".gif" },
        { "webp", Hex("RIFF") + "00000000" + Hex("WEBPVP8 "), ".webp" },
        { "wav", Hex("RIFF") + "00000000" + Hex("WAVEfmt "), ".wav" },
        { "avi", Hex("RIFF") + "00000000" + Hex("AVI LIST"), ".avi" },
        { "aiff", Hex("FORM") + "00000000" + Hex("AIFFCOMM"), ".aiff" },
        { "bmp", Hex("BM") + "000000000000000000000000", ".bmp" },
        { "tiff", "49492A00080000000000", ".tif" },
        { "tiff", "4D4D002A000000080000", ".tiff" },
        { "pdf", Hex("%PDF-1.7\n"), ".pdf" },
        { "flac", Hex("fLaC") + "00000022", ".flac" },
        { "ogg", Hex("OggS") + "0002" + Hex("vorbis"), ".ogg" },
        { "opus", Hex("OggS") + "0002" + Hex("OpusHead"), ".ogg" },
        { "mp3", Hex("ID3") + "0400", ".mp3" },
        { "mp3", "FFFB9064", ".mp3" },
        { "mkv", "1A45DFA3" + Hex("matroska"), ".mkv" },
        { "webm", "1A45DFA3" + Hex("webm"), ".mkv" },
        { "heic", "00000018" + Hex("ftypheic") + "00000000", ".heic" },
        { "avif", "0000001C" + Hex("ftypavif") + "00000000", ".avif" },
        { "avif", "00000020" + Hex("ftypmif1") + "00000000" + Hex("avifmiaf"), ".heic" },
        { "mov", "00000014" + Hex("ftypqt  ") + "00000000", ".mov" },
        { "m4a", "00000020" + Hex("ftypM4A ") + "00000000", ".m4a" },
        { "mp4", "00000020" + Hex("ftypisom") + "00000200", ".mp4" },
        { "rar", Hex("Rar!") + "1A0700", ".rar" },
        { "7z", "377ABCAF271C0004", ".7z" },
        { "gz", "1F8B0800", ".gz" },
        { "rtf", Hex(@"{\rtf1\ansi"), ".rtf" },
        { "vtt", Hex("WEBVTT\n\n00:00.000 --> 00:01.000"), ".vtt" },
        { "svg", Hex("<?xml version=\"1.0\"?>\n<svg xmlns=\"http://www.w3.org/2000/svg\"/>"), ".svg" },
    };

    [Theory]
    [MemberData(nameof(Signatures))]
    public void Detect_ReconoceFirma(string expectedId, string headerHex, string extension)
    {
        FormatSniffer.Detect(Convert.FromHexString(headerHex), extension).ShouldNotBeNull().Id.ShouldBe(expectedId);
    }

    [Fact]
    public void Detect_ContenidoGanaALaExtension()
    {
        // Un PNG renombrado a .jpg sigue siendo PNG.
        FormatSniffer.Detect(Convert.FromHexString("89504E470D0A1A0A"), ".jpg").ShouldBe(KnownFormats.Png);
    }

    [Theory]
    [InlineData(".docx", "docx")]
    [InlineData(".xlsx", "xlsx")]
    [InlineData(".epub", "epub")]
    [InlineData(".zip", "zip")]
    [InlineData(".png", "zip")]
    [InlineData("", "zip")]
    public void Detect_ZipSeDesambiguaPorExtension(string extension, string expected)
    {
        FormatSniffer.Detect("PK\x03\x04\x14\0\0\0"u8, extension)!.Id.ShouldBe(expected);
    }

    [Theory]
    [InlineData(".wma", "wma")]
    [InlineData(".wmv", "wmv")]
    [InlineData(".asf", "wmv")]
    public void Detect_AsfSeDesambiguaPorExtension(string extension, string expected)
    {
        FormatSniffer.Detect(Convert.FromHexString("3026B2758E66CF11A6D900AA0062CE6C"), extension)!.Id.ShouldBe(expected);
    }

    [Theory]
    [InlineData(".xls", "xls")]
    [InlineData(".ppt", "ppt")]
    public void Detect_Ole2RequiereExtension(string extension, string expected)
    {
        var header = Convert.FromHexString("D0CF11E0A1B11AE1");
        FormatSniffer.Detect(header, extension)!.Id.ShouldBe(expected);
        FormatSniffer.Detect(header, ".doc").ShouldBeNull();
    }

    [Fact]
    public void Detect_Tar_PorUstarEnOffset257()
    {
        var header = new byte[512];
        "ustar"u8.CopyTo(header.AsSpan(257));
        FormatSniffer.Detect(header, ".tar").ShouldBe(KnownFormats.Tar);
    }

    [Theory]
    [InlineData(".txt", "txt")]
    [InlineData(".md", "md")]
    [InlineData(".csv", "csv")]
    [InlineData(".srt", "srt")]
    public void Detect_TextoSinFirma_UsaExtension(string extension, string expected)
    {
        FormatSniffer.Detect("hola, mundo\r\n1,2,3\n"u8, extension)!.Id.ShouldBe(expected);
    }

    [Fact]
    public void Detect_TextoConMultibyteCortadoAlFinal_SigueSiendoTexto()
    {
        var bytes = Encoding.UTF8.GetBytes("canción ñandú");
        FormatSniffer.Detect(bytes.AsSpan(0, bytes.Length - 1), ".txt").ShouldBe(KnownFormats.Txt);
    }

    [Fact]
    public void Detect_BinarioConExtensionDeTexto_NoSeAcepta()
    {
        FormatSniffer.Detect([0x00, 0x01, 0x02, 0x03, 0xFE], ".txt").ShouldBeNull();
    }

    [Fact]
    public void Detect_IcoSinExtension_NoSeAcepta()
    {
        FormatSniffer.Detect([0x00, 0x00, 0x01, 0x00, 0x01, 0x00], ".bin").ShouldBeNull();
        FormatSniffer.Detect([0x00, 0x00, 0x01, 0x00, 0x01, 0x00], ".ico").ShouldBe(KnownFormats.Ico);
    }

    [Fact]
    public void Detect_CabeceraVaciaODesconocida_DevuelveNull()
    {
        FormatSniffer.Detect([], ".exe").ShouldBeNull();
        FormatSniffer.Detect([0x4D, 0x5A, 0x90, 0x00], ".exe").ShouldBeNull();
    }

    [Fact]
    public void Detect_NuncaLanza_ConEntradasArbitrarias()
    {
        Gen.Select(Gen.Byte.Array[0, 2048], Gen.String).Sample((bytes, ext) =>
        {
            _ = FormatSniffer.Detect(bytes, ext);
        });
    }

    [Fact]
    public void DetectFile_LeeDesdeDisco()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kumquat-sniff-{Guid.NewGuid():N}.dat");
        File.WriteAllBytes(path, Convert.FromHexString("89504E470D0A1A0A0000"));
        try
        {
            FormatSniffer.DetectFile(path).ShouldBe(KnownFormats.Png);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string Hex(string ascii) => Convert.ToHexString(Encoding.ASCII.GetBytes(ascii));
}

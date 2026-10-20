// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.IO.Compression;
using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;
using Kumquat.Engines.Calibre;

namespace Kumquat.Engines.Tests.Integration;

public sealed class CalibreConverterTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("kumquat-calibre-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void SinCalibre_NoOfreceNada()
    {
        var converter = new CalibreConverter(null, TestEngines.Runner, TestEngines.Workspaces);

        converter.IsAvailable.ShouldBeFalse();
        converter.Edges.ShouldBeEmpty();
    }

    [Fact]
    public void Aristas_LibrosEntreSiYAPdf_SinPisarAMuPdfNiAOffice()
    {
        var converter = new CalibreConverter(@"C:\Calibre2\ebook-convert.exe", TestEngines.Runner, TestEngines.Workspaces);

        converter.Edges.ShouldContain(e => e.From == KnownFormats.Mobi && e.To == KnownFormats.Epub);
        converter.Edges.ShouldContain(e => e.From == KnownFormats.Docx && e.To == KnownFormats.Epub);
        converter.Edges.ShouldContain(e => e.From == KnownFormats.Fb2 && e.To == KnownFormats.Pdf);
        converter.Edges.ShouldNotContain(e => e.From == KnownFormats.Epub && e.To == KnownFormats.Pdf, "lo hace MuPDF");
        converter.Edges.ShouldNotContain(e => e.From == KnownFormats.Docx && e.To == KnownFormats.Pdf, "lo hace Office");
        converter.Edges.ShouldNotContain(e => e.From == e.To);
    }

    [Theory]
    [InlineData("1% Convirtiendo entrada a HTML...", 0.01)]
    [InlineData("34% Running transforms on e-book...", 0.34)]
    [InlineData("100% Creando AZW3...", 1.0)]
    public void Progreso_SeLeeDeLaSalida(string line, double expected)
        => CalibreConverter.ParseProgress(line).ShouldBe(expected);

    [Theory]
    [InlineData("Salida guardada en C:\\libro.azw3")]
    [InlineData("InputFormatPlugin: EPUB Input running")]
    [InlineData("1000% raro")]
    [InlineData("")]
    public void Progreso_LoDemasSeIgnora(string line)
        => CalibreConverter.ParseProgress(line).ShouldBeNull();

    [Theory]
    [InlineData("azw3")]
    [InlineData("mobi")]
    public async Task IdaYVuelta_ConservaElTexto(string formatId)
    {
        Assert.SkipUnless(TestEngines.Calibre.IsAvailable, "Calibre no está instalado (o KUMQUAT_TEST_CALIBRE).");
        var format = KnownFormats.FindById(formatId)!;
        var book = Path.Combine(_dir, "libro" + format.DefaultExtension);
        var epub = (await Corpus.GetAsync(KnownFormats.Epub))!;

        (await Convert(epub, book, KnownFormats.Epub, format)).IsSuccess.ShouldBeTrue();
        FormatSniffer.DetectFile(book).ShouldBe(format, "se detecta por contenido");

        var back = Path.Combine(_dir, "vuelta.epub");
        (await Convert(book, back, format, KnownFormats.Epub)).IsSuccess.ShouldBeTrue();
        (await EpubTextAsync(back)).ShouldContain("Kumquat convierte libros");
    }

    [Fact]
    public async Task Fb2_AEpubYAPdf()
    {
        Assert.SkipUnless(TestEngines.Calibre.IsAvailable, "Calibre no está instalado (o KUMQUAT_TEST_CALIBRE).");
        var fb2 = (await Corpus.GetAsync(KnownFormats.Fb2))!;
        var epub = Path.Combine(_dir, "desde-fb2.epub");
        var pdf = Path.Combine(_dir, "desde-fb2.pdf");

        (await Convert(fb2, epub, KnownFormats.Fb2, KnownFormats.Epub)).IsSuccess.ShouldBeTrue();
        (await Convert(fb2, pdf, KnownFormats.Fb2, KnownFormats.Pdf)).IsSuccess.ShouldBeTrue();

        (await EpubTextAsync(epub)).ShouldContain("Kumquat convierte libros");
        FormatSniffer.DetectFile(pdf).ShouldBe(KnownFormats.Pdf);
    }

    [Fact]
    public async Task WordALibro()
    {
        Assert.SkipUnless(TestEngines.Calibre.IsAvailable, "Calibre no está instalado (o KUMQUAT_TEST_CALIBRE).");
        Assert.SkipUnless(OfficeSamples.CanCreate(KnownFormats.Docx), "Sin Word para crear el documento.");
        var epub = Path.Combine(_dir, "documento.epub");

        (await Convert((await Corpus.GetAsync(KnownFormats.Docx))!, epub, KnownFormats.Docx, KnownFormats.Epub)).IsSuccess.ShouldBeTrue();

        (await EpubTextAsync(epub)).ShouldContain(OfficeSamples.Text);
    }

    [Fact]
    public async Task LibroDañado_ErrorClaro()
    {
        Assert.SkipUnless(TestEngines.Calibre.IsAvailable, "Calibre no está instalado (o KUMQUAT_TEST_CALIBRE).");
        var broken = Path.Combine(_dir, "roto.mobi");
        var bytes = new byte[300];
        "BOOKMOBI"u8.CopyTo(bytes.AsSpan(60));
        await File.WriteAllBytesAsync(broken, bytes, TestContext.Current.CancellationToken);

        var result = await Convert(broken, Path.Combine(_dir, "roto.epub"), KnownFormats.Mobi, KnownFormats.Epub);

        result.IsSuccess.ShouldBeFalse();
        result.Error!.Message.ShouldStartWith("Calibre no pudo convertir");
        File.Exists(Path.Combine(_dir, "roto.epub")).ShouldBeFalse();
    }

    [Fact]
    public async Task NoTocaLaConfiguracionDeCalibreDeLaPersona()
    {
        Assert.SkipUnless(TestEngines.Calibre.IsAvailable, "Calibre no está instalado (o KUMQUAT_TEST_CALIBRE).");
        var config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "calibre");
        Assert.SkipWhen(Directory.Exists(config), "Calibre ya se usó en este equipo: no se puede comprobar.");

        (await Convert((await Corpus.GetAsync(KnownFormats.Epub))!, Path.Combine(_dir, "x.azw3"), KnownFormats.Epub, KnownFormats.Azw3)).IsSuccess.ShouldBeTrue();

        Directory.Exists(config).ShouldBeFalse();
    }

    private static Task<Kumquat.Core.Results.Result> Convert(string input, string output, FileFormat from, FileFormat to)
        => TestEngines.Calibre.ConvertAsync(new ConversionStep(input, output, new ConversionEdge(from, to, CalibreConverter.ConverterId)), null, TestContext.Current.CancellationToken);

    private static async Task<string> EpubTextAsync(string epub)
    {
        using var zip = await ZipFile.OpenReadAsync(epub, TestContext.Current.CancellationToken);
        var text = new System.Text.StringBuilder();
        foreach (var entry in zip.Entries.Where(e => e.FullName.EndsWith("html", StringComparison.OrdinalIgnoreCase)))
        {
            using var reader = new StreamReader(await entry.OpenAsync(TestContext.Current.CancellationToken));
            text.Append(await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
        }

        return text.ToString();
    }
}

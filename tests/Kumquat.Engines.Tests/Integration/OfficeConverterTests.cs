// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.IO.Compression;
using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;
using Kumquat.Engines.Office;

namespace Kumquat.Engines.Tests.Integration;

public sealed class OfficeConverterTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("kumquat-office-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void SinNadaInstalado_NoOfreceConversiones()
    {
        var converter = new OfficeConverter(new OfficeApps(false, false, false, null), TestEngines.Runner, TestEngines.Workspaces);

        converter.IsAvailable.ShouldBeFalse();
        converter.Edges.ShouldBeEmpty();
    }

    [Fact]
    public void SoloLibreOffice_TodoPorLibreOffice_YPdfAWordMasCaro()
    {
        var converter = new OfficeConverter(new OfficeApps(false, false, false, @"C:\LibreOffice\program\soffice.exe"), TestEngines.Runner, TestEngines.Workspaces);

        converter.Edges.Count.ShouldBe(OfficeConverter.TextDocuments.Length + OfficeConverter.Spreadsheets.Length + OfficeConverter.Presentations.Length + 1);
        converter.Edges.Single(e => e.From == KnownFormats.Pdf).Cost.ShouldBe(3.0);
    }

    [Fact]
    public void SoloWord_DocumentosYPdfAWord_SinHojasNiPresentaciones()
    {
        var converter = new OfficeConverter(new OfficeApps(true, false, false, null), TestEngines.Runner, TestEngines.Workspaces);

        converter.Edges.Select(e => e.From).ShouldBe([.. OfficeConverter.TextDocuments, KnownFormats.Pdf], ignoreOrder: true);
        converter.Edges.ShouldAllBe(e => e.From == KnownFormats.Pdf ? e.To == KnownFormats.Docx : e.To == KnownFormats.Pdf);
    }

    [Theory]
    [InlineData("docx")]
    [InlineData("xlsx")]
    [InlineData("pptx")]
    public async Task ConOffice_ElPdfConservaElTexto(string formatId)
    {
        var format = KnownFormats.FindById(formatId)!;
        Assert.SkipUnless(OfficeSamples.CanCreate(format), "Microsoft Office no está instalado.");
        var sample = await Corpus.GetAsync(format);
        sample.ShouldNotBeNull();
        var pdf = Path.Combine(_dir, $"{formatId}.pdf");

        var result = await TestEngines.Office.ConvertAsync(new ConversionStep(sample, pdf, new ConversionEdge(format, KnownFormats.Pdf, OfficeConverter.ConverterId)), null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
        FormatSniffer.DetectFile(pdf).ShouldBe(KnownFormats.Pdf);
        (await PdfTextAsync(pdf)).ShouldContain(OfficeSamples.Text);
    }

    [Fact]
    public async Task ConWord_PdfAWord_TextoEditable()
    {
        Assert.SkipUnless(OfficeSamples.CanCreate(KnownFormats.Docx), "Microsoft Word no está instalado.");
        var source = Path.Combine(_dir, "origen.pdf");
        var original = await Corpus.GetAsync(KnownFormats.Docx);
        (await TestEngines.Office.ConvertAsync(new ConversionStep(original!, source, new ConversionEdge(KnownFormats.Docx, KnownFormats.Pdf, OfficeConverter.ConverterId)), null, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var docx = Path.Combine(_dir, "editable.docx");

        var result = await TestEngines.Office.ConvertAsync(new ConversionStep(source, docx, new ConversionEdge(KnownFormats.Pdf, KnownFormats.Docx, OfficeConverter.ConverterId)), null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
        FormatSniffer.DetectFile(docx).ShouldBe(KnownFormats.Docx);
        using var zip = await ZipFile.OpenReadAsync(docx, TestContext.Current.CancellationToken);
        using var reader = new StreamReader(await zip.GetEntry("word/document.xml")!.OpenAsync(TestContext.Current.CancellationToken));
        (await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).ShouldContain("Kumquat");
    }

    [Fact]
    public async Task DocumentoDañado_ErrorClaro_SinDejarOfficeAbierto()
    {
        Assert.SkipUnless(OfficeSamples.CanCreate(KnownFormats.Docx), "Microsoft Word no está instalado.");
        var broken = Path.Combine(_dir, "roto.docx");
        await File.WriteAllBytesAsync(broken, [0x50, 0x4B, 0x03, 0x04, 1, 2, 3, 4, 5], TestContext.Current.CancellationToken);
        var before = System.Diagnostics.Process.GetProcessesByName("WINWORD").Length;

        var result = await TestEngines.Office.ConvertAsync(new ConversionStep(broken, Path.Combine(_dir, "roto.pdf"), new ConversionEdge(KnownFormats.Docx, KnownFormats.Pdf, OfficeConverter.ConverterId)), null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Error!.Message.ShouldStartWith("Microsoft Word");
        // Otros tests convierten con Word a la vez (por turnos): se espera a que no quede ninguno de más.
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (System.Diagnostics.Process.GetProcessesByName("WINWORD").Length > before && DateTime.UtcNow < deadline)
        {
            await Task.Delay(500, TestContext.Current.CancellationToken);
        }

        System.Diagnostics.Process.GetProcessesByName("WINWORD").Length.ShouldBeLessThanOrEqualTo(before, "Word se cierra aunque falle");
    }

    private async Task<string> PdfTextAsync(string pdf)
    {
        var text = Path.Combine(_dir, Path.GetFileNameWithoutExtension(pdf) + ".txt");
        (await TestEngines.Pdf.RunMutoolAsync(["draw", "-q", "-F", "txt", "-o", text, pdf], TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        // PowerPoint parte la frase en líneas: se compara con los espacios normalizados.
        return string.Join(' ', (await File.ReadAllTextAsync(text, TestContext.Current.CancellationToken)).Split((char[])[' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries));
    }
}

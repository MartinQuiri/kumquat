// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Text;
using Kumquat.Core.Formats;
using Kumquat.Core.Tools;
using Kumquat.Engines.Ocr;

namespace Kumquat.Engines.Tests.Integration;

/// <summary>
/// OCR con Tesseract sobre imágenes con texto dibujadas por el test (sin archivos de terceros).
/// Se omite si el motor no está compilado (tools/build-tesseract.ps1).
/// </summary>
public sealed class OcrTests : IDisposable
{
    private const string Sentence = "Kumquat convierte archivos sin abrir nada";
    private const string Accents = "Canción del año para el niño";

    private static readonly ToolService Service = new(OcrTool.CreateAll(TestEngines.Tesseract, TestEngines.Pdf, TestEngines.Workspaces));

    private readonly string _dir = Directory.CreateTempSubdirectory("kumquat-ocr-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void ArgumentosDeUnion_ReemplazanSoloLasPaginasEscaneadas()
    {
        var arguments = PdfOcr.MergeArguments("in.pdf", 7, new HashSet<int> { 2, 3, 6 }, page => $"ocr-{page}.pdf", "out.pdf");

        arguments.ShouldBe(["in.pdf", "--pages", "in.pdf", "1", "ocr-2.pdf", "1", "ocr-3.pdf", "1", "in.pdf", "4-5", "ocr-6.pdf", "1", "in.pdf", "7", "--", "out.pdf"]);
    }

    [Fact]
    public void PaginaConPocoTexto_SeReconoce()
    {
        var file = Path.Combine(_dir, "t.txt");
        File.WriteAllText(file, "  3  \n");
        PdfOcr.HasText(file).ShouldBeFalse("un número de página suelto no es texto");
        File.WriteAllText(file, Sentence);
        PdfOcr.HasText(file).ShouldBeTrue();
        PdfOcr.HasText(Path.Combine(_dir, "no-existe.txt")).ShouldBeFalse();
    }

    [Theory]
    [InlineData("png")]
    [InlineData("jpg")]
    public async Task Imagen_ExtraerTexto_ConTildesYEñes(string formatId)
    {
        RequireEngine();
        var image = TextImage("foto." + formatId, formatId == "png" ? ImageFormat.Png : ImageFormat.Jpeg);

        var result = await Service.ApplyAsync([image], "image.ocr-text", null, null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
        var text = await File.ReadAllTextAsync(result.Value!, TestContext.Current.CancellationToken);
        text.ShouldContain(Sentence, Case.Insensitive);
        text.ShouldContain(Accents, Case.Insensitive, "el modelo de español reconoce tildes y eñes");
        text.ShouldNotContain("\f");
    }

    [Fact]
    public async Task Imagen_PdfBuscable_TieneElTextoSeleccionable()
    {
        RequireEngine();
        var image = TextImage("escaneo.jpg", ImageFormat.Jpeg);

        var result = await Service.ApplyAsync([image], "image.ocr-pdf", null, null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
        FormatSniffer.DetectFile(result.Value!).ShouldBe(KnownFormats.Pdf);
        (await PdfTextAsync(result.Value!)).ShouldContain(Sentence, Case.Insensitive);
    }

    [Fact]
    public async Task ImagenSinTexto_ErrorClaro()
    {
        RequireEngine();
        var blank = Path.Combine(_dir, "vacia.png");
        using (var bitmap = new Bitmap(800, 400))
        {
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.White);
            }

            bitmap.Save(blank, ImageFormat.Png);
        }

        var result = await Service.ApplyAsync([blank], "image.ocr-text", null, null, TestContext.Current.CancellationToken);

        result.Error!.Code.ShouldBe("no-text-found");
    }

    [Fact]
    public async Task PdfMixto_SoloSeReconoceLaPaginaEscaneada()
    {
        RequireEngine();
        var pdf = await MixedPdfAsync();

        var result = await Service.ApplyAsync([pdf], "pdf.ocr", null, null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
        var pages = await PdfPagesTextAsync(result.Value!);
        pages.Count.ShouldBe(2);
        pages[0].ShouldContain("Texto vectorial original", Case.Insensitive, "la página con texto queda intacta");
        pages[1].ShouldContain(Sentence, Case.Insensitive, "la página escaneada ahora tiene texto");
    }

    [Fact]
    public async Task PdfConTextoEnTodasLasPaginas_NoHaceFaltaOcr()
    {
        RequireEngine();
        var pdf = await VectorPdfAsync(Path.Combine(_dir, "texto.pdf"));

        var result = await Service.ApplyAsync([pdf], "pdf.ocr", null, null, TestContext.Current.CancellationToken);

        result.Error!.Code.ShouldBe("already-searchable");
    }

    private static void RequireEngine()
        => Assert.SkipUnless(TestEngines.Tesseract.IsAvailable, "Tesseract no está compilado (tools/build-tesseract.ps1).");

    /// <summary>Dos renglones negros sobre blanco, a 300 ppp, como una hoja escaneada.</summary>
    private string TextImage(string name, ImageFormat format)
    {
        var path = Path.Combine(_dir, name);
        using var bitmap = new Bitmap(2400, 700);
        bitmap.SetResolution(300, 300);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.White);
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using var font = new Font("Segoe UI", 28, FontStyle.Regular, GraphicsUnit.Point);
            g.DrawString(Sentence, font, Brushes.Black, 120, 140);
            g.DrawString(Accents, font, Brushes.Black, 120, 380);
        }

        bitmap.Save(path, format);
        return path;
    }

    private async Task<string> VectorPdfAsync(string path)
    {
        // Página con texto real (no imagen), hecha con mutool create.
        var page = Path.Combine(_dir, "pagina.txt");
        await File.WriteAllTextAsync(page, "%%MediaBox 0 0 595 842\n%%Font F1 Helvetica\nBT /F1 24 Tf 72 720 Td (Texto vectorial original de esta pagina) Tj ET\n", TestContext.Current.CancellationToken);
        var created = await TestEngines.Pdf.RunMutoolAsync(["create", "-o", path, page], TestContext.Current.CancellationToken);
        created.IsSuccess.ShouldBeTrue(created.Error?.ToString());
        return path;
    }

    private async Task<string> MixedPdfAsync()
    {
        var vector = await VectorPdfAsync(Path.Combine(_dir, "vectorial.pdf"));
        var scan = Path.Combine(_dir, "escaneo.pdf");
        var built = await TestEngines.ImagePdf.BuildAsync([(TextImage("hoja.jpg", ImageFormat.Jpeg), KnownFormats.Jpg)], scan, TestContext.Current.CancellationToken);
        built.IsSuccess.ShouldBeTrue(built.Error?.ToString());

        var mixed = Path.Combine(_dir, "mixto.pdf");
        var merged = await TestEngines.Pdf.RunQpdfAsync(["--empty", "--pages", vector, scan, "--", mixed], TestContext.Current.CancellationToken);
        merged.IsSuccess.ShouldBeTrue(merged.Error?.ToString());
        return mixed;
    }

    private async Task<string> PdfTextAsync(string pdf)
        => string.Join('\n', await PdfPagesTextAsync(pdf));

    private async Task<List<string>> PdfPagesTextAsync(string pdf)
    {
        var pattern = Path.Combine(_dir, $"{Path.GetFileNameWithoutExtension(pdf)}-%d.txt");
        var drawn = await TestEngines.Pdf.RunMutoolAsync(["draw", "-q", "-F", "txt", "-o", pattern, pdf], TestContext.Current.CancellationToken);
        drawn.IsSuccess.ShouldBeTrue(drawn.Error?.ToString());
        return [.. Enumerable.Range(1, 50).Select(i => pattern.Replace("%d", i.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)).TakeWhile(File.Exists).Select(f => File.ReadAllText(f, Encoding.UTF8))];
    }
}

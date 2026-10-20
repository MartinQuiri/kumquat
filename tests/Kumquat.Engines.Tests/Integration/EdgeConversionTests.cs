// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;

namespace Kumquat.Engines.Tests.Integration;

/// <summary>
/// Un test por cada arista directa que declaran los motores. Si alguien agrega una
/// conversión al grafo, aparece aquí automáticamente: nadie tiene que acordarse de probarla.
/// </summary>
[Trait("Category", "Integration")]
public sealed class EdgeConversionTests
{
    public static TheoryData<string, string, string> AllEdges()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var edge in TestEngines.All.Where(c => c.IsAvailable).SelectMany(c => c.Edges).OrderBy(e => e.ConverterId, StringComparer.Ordinal).ThenBy(e => e.From.Id, StringComparer.Ordinal).ThenBy(e => e.To.Id, StringComparer.Ordinal))
        {
            data.Add(edge.ConverterId, edge.From.Id, edge.To.Id);
        }

        return data;
    }

    [Fact]
    public void LosMotoresEstanInstalados()
    {
        // Falla ruidosamente en CI si alguien olvida tools/fetch-engines.ps1.
        TestEngines.Ffmpeg.IsAvailable.ShouldBeTrue("FFmpeg no está: ejecuta ./tools/fetch-engines.ps1");
        TestEngines.Vips.IsAvailable.ShouldBeTrue("libvips no cargó");
        AllEdges().Count.ShouldBeGreaterThan(150);
    }

    [Theory]
    [MemberData(nameof(AllEdges))]
    public async Task Arista_ProduceUnArchivoValidoDelFormatoDestino(string converterId, string fromId, string toId)
    {
        var from = KnownFormats.FindById(fromId)!;
        var to = KnownFormats.FindById(toId)!;
        var converter = TestEngines.ById(converterId);
        var edge = converter.Edges.Single(e => e.From == from && e.To == to);

        var input = await Corpus.GetAsync(from);
        Assert.SkipWhen(input is null, $"El entorno no puede generar una muestra {from.DisplayName}.");

        var output = Path.Combine(Path.GetTempPath(), "kumquat-out", Guid.NewGuid().ToString("N") + to.DefaultExtension);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        try
        {
            var reported = new List<double>();
            var result = await converter.ConvertAsync(new ConversionStep(input!, output, edge), new SyncProgress(reported.Add), TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
            new FileInfo(output).Length.ShouldBeGreaterThan(0);
            FormatSniffer.DetectFile(output).ShouldBe(to, $"El contenido escrito no es {to.DisplayName}");
            reported.ShouldAllBe(p => p >= 0 && p <= 1);

            await AssertContentAsync(from, to, output);
        }
        finally
        {
            File.Delete(output);
        }
    }

    private static async Task AssertContentAsync(FileFormat from, FileFormat to, string output)
    {
        if (to == KnownFormats.Txt)
        {
            var text = await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken);
            text.ShouldContain(from == KnownFormats.Epub ? "Kumquat convierte libros" : "Kumquat pagina 2");

            return;
        }

        // Documentos y libros: el contenido lo revisan OfficeConverterTests y CalibreConverterTests; aquí basta el formato.
        if (to == KnownFormats.Pdf || to.Category is FormatCategory.Document or FormatCategory.Ebook)
        {
            new FileInfo(output).Length.ShouldBeGreaterThan(200);
            return;
        }

        if (to.Category is FormatCategory.Audio)
        {
            var info = await MediaProbe.ProbeAsync(output, TestContext.Current.CancellationToken);
            info.HasAudio.ShouldBeTrue();
            info.HasVideo.ShouldBeFalse("La salida de audio no debe arrastrar pistas de video ni carátulas.");
            info.DurationSeconds.ShouldNotBeNull().ShouldBe(Corpus.Duration.TotalSeconds, tolerance: 0.15);
            return;
        }

        if (to.Category is FormatCategory.Video || (to == KnownFormats.Gif && from.Category is FormatCategory.Video))
        {
            var info = await MediaProbe.ProbeAsync(output, TestContext.Current.CancellationToken);
            info.HasVideo.ShouldBeTrue();
            info.Width.ShouldBe(Corpus.Width);
            info.Height.ShouldBe(Corpus.Height);
            info.DurationSeconds.ShouldNotBeNull().ShouldBe(Corpus.Duration.TotalSeconds, tolerance: 0.25);
            if (from.Category is FormatCategory.Video && to != KnownFormats.Gif)
            {
                info.HasAudio.ShouldBeTrue("Se perdió la pista de audio.");
            }

            return;
        }

        if (from == KnownFormats.Gif && to == KnownFormats.WebP)
        {
            // ffprobe no decodifica WebP animado; libvips sí, y además permite verificar que la animación sobrevivió.
            using var animated = NetVips.Image.NewFromFile(output, kwargs: new NetVips.VOption { { "n", -1 } });
            animated.Width.ShouldBe(Corpus.Width);
            animated.PageHeight.ShouldBe(Corpus.Height);
            ((int)animated.Get("n-pages")).ShouldBeGreaterThan(1, "Se perdió la animación.");
            return;
        }

        // Imágenes: mismas dimensiones que la muestra (ffprobe lee todos los formatos de salida, incluido BMP).
        var picture = await MediaProbe.ProbeAsync(output, TestContext.Current.CancellationToken);
        picture.Width.ShouldBe(Corpus.Width);
        picture.Height.ShouldBe(Corpus.Height);
    }

    private sealed class SyncProgress(Action<double> onReport) : IProgress<double>
    {
        public void Report(double value) => onReport(value);
    }
}

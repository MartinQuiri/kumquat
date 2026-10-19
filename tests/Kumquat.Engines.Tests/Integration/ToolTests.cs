// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Formats;
using Kumquat.Core.Tools;
using Kumquat.Engines.Archives;
using Kumquat.Engines.FFmpeg;
using Kumquat.Engines.Pdf;
using Kumquat.Engines.Vips;

namespace Kumquat.Engines.Tests.Integration;

/// <summary>
/// Un test por cada (herramienta, formato aplicable), generado desde el catálogo: igual que
/// las conversiones, una herramienta nueva queda probada sin escribir nada. Las herramientas que
/// combinan se prueban con dos copias de la muestra.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ToolTests : IDisposable
{
    /// <summary>"Comprimir en ZIP" sirve para todo: se prueba con una muestra por familia, no con los 46 formatos.</summary>
    private static readonly FileFormat[] ZipSamples = [KnownFormats.Png, KnownFormats.Pdf, KnownFormats.Mp3, KnownFormats.Txt];

    private static readonly IReadOnlyList<ITool> Tools =
    [
        .. VipsTool.CreateAll(),
        .. FfmpegTool.CreateAll(TestEngines.FfmpegRunner),
        .. PdfTool.CreateAll(TestEngines.Pdf, TestEngines.ImagePdf),
        .. ArchiveTools.CreateAll(),
    ];

    private static readonly ToolService Service = new(Tools);

    private readonly string _dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "kumquat-tools", Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    public static TheoryData<string, string> AllApplications()
    {
        var data = new TheoryData<string, string>();
        foreach (var tool in Tools.Where(t => t.IsAvailable))
        {
            var formats = tool.Id == "archive.zip" ? ZipSamples : KnownFormats.All.Where(tool.AppliesTo);
            foreach (var format in formats)
            {
                data.Add(tool.Id, format.Id);
            }
        }

        return data;
    }

    [Fact]
    public void TodasLasHerramientasEstanDisponibles()
    {
        Tools.Where(t => !t.IsAvailable).Select(t => t.Id).ShouldBeEmpty("¿Faltan motores? ejecuta ./tools/fetch-engines.ps1");
        Tools.Count.ShouldBeGreaterThanOrEqualTo(20);
    }

    [Theory]
    [MemberData(nameof(AllApplications))]
    public async Task Herramienta_ProduceUnResultadoValido(string toolId, string formatId)
    {
        var tool = Tools.Single(t => t.Id == toolId);
        var format = KnownFormats.FindById(formatId)!;
        var sample = await Corpus.GetAsync(format);
        Assert.SkipWhen(sample is null, $"El entorno no puede generar una muestra {format.DisplayName}.");

        // Copias aisladas: la salida aparece junto a la entrada. Las que combinan reciben dos.
        var inputs = Enumerable.Range(1, Math.Max(tool.MinInputs, tool.Mode == ToolMode.Combine ? 2 : 1))
            .Select(n => Copy(sample!, $"muestra{n}{format.DefaultExtension}"))
            .ToList();

        var result = await Service.ApplyAsync(inputs, toolId, null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
        var output = result.Value!;
        inputs.ShouldAllBe(i => File.Exists(i), "Los originales nunca se tocan.");
        var expected = tool.OutputFormat(format);
        if (expected is null)
        {
            Directory.EnumerateFileSystemEntries(output).ShouldNotBeEmpty();
        }
        else
        {
            FormatSniffer.DetectFile(output).ShouldBe(expected);
        }

        await AssertEffectAsync(toolId, format, inputs[0], output);
    }

    [Fact]
    public async Task ComprimirCarpetaEnZip_YExtraerla_RecuperaLoMismo()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_dir, "Mis cosas")).FullName;
        await File.WriteAllTextAsync(Path.Combine(folder, "a.txt"), "a", TestContext.Current.CancellationToken);
        Directory.CreateDirectory(Path.Combine(folder, "sub"));
        await File.WriteAllTextAsync(Path.Combine(folder, "sub", "b.txt"), "bé", TestContext.Current.CancellationToken);

        var zip = await Service.ApplyAsync([folder], "archive.zip", null, TestContext.Current.CancellationToken);
        zip.Value.ShouldBe(Path.Combine(_dir, "Mis cosas.zip"));

        var extracted = await Service.ApplyAsync([zip.Value!], "archive.extract", null, TestContext.Current.CancellationToken);
        extracted.Value.ShouldBe(Path.Combine(_dir, "Mis cosas (2)"));
        (await File.ReadAllTextAsync(Path.Combine(extracted.Value!, "Mis cosas", "sub", "b.txt"), TestContext.Current.CancellationToken)).ShouldBe("bé");
    }

    private static async Task AssertEffectAsync(string toolId, FileFormat format, string input, string output)
    {
        var ct = TestContext.Current.CancellationToken;
        switch (toolId)
        {
            case "image.compress" or "video.compress" when format != KnownFormats.Avi && format != KnownFormats.Wmv && format != KnownFormats.Tiff:
                new FileInfo(output).Length.ShouldBeLessThan(new FileInfo(input).Length, "Comprimir debe achicar el archivo.");
                break;
            case "image.rotate" or "video.rotate":
                var rotated = await MediaProbe.ProbeAsync(output, ct);
                (rotated.Width, rotated.Height).ShouldBe((Corpus.Height, Corpus.Width));
                break;
            case "image.flip" or "image.gray" or "image.fit":
                var same = await MediaProbe.ProbeAsync(output, ct);
                (same.Width, same.Height).ShouldBe((Corpus.Width, Corpus.Height));
                break;
            case "video.mute":
                var muted = await MediaProbe.ProbeAsync(output, ct);
                muted.HasVideo.ShouldBeTrue();
                muted.HasAudio.ShouldBeFalse();
                break;
            case "audio.normalize":
                var normalized = await MediaProbe.ProbeAsync(output, ct);
                normalized.HasAudio.ShouldBeTrue();
                normalized.DurationSeconds.ShouldNotBeNull().ShouldBe(Corpus.Duration.TotalSeconds, tolerance: 0.15);
                normalized.SampleRate.ShouldNotBeNull().ShouldBeLessThanOrEqualTo(48000, "loudnorm no debe dejar el audio a 192 kHz.");
                break;
            case "video.merge" or "audio.merge":
                var merged = await MediaProbe.ProbeAsync(output, ct);
                merged.DurationSeconds.ShouldNotBeNull().ShouldBe(2 * Corpus.Duration.TotalSeconds, tolerance: 0.4, "Unir dos clips debe sumar sus duraciones.");
                break;
            case "pdf.split" or "pdf.to-jpg":
                Directory.GetFiles(output).Length.ShouldBe(2, "La muestra tiene 2 páginas.");
                break;
            case "archive.extract":
                Directory.EnumerateFiles(output, "*.txt", SearchOption.AllDirectories).Count().ShouldBe(2);
                break;
        }
    }

    private string Copy(string sample, string name)
    {
        var path = Path.Combine(_dir, name);
        File.Copy(sample, path);
        return path;
    }
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Formats;
using Kumquat.Core.Tools;
using Kumquat.Engines.FFmpeg;
using Kumquat.Engines.Vips;

namespace Kumquat.Engines.Tests.Integration;

/// <summary>
/// Un test por cada (herramienta, formato aplicable), generado desde el catálogo: igual que
/// las conversiones, una herramienta nueva queda probada sin escribir nada.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ToolTests
{
    private static readonly IReadOnlyList<ITool> Tools = [.. VipsTool.CreateAll(), .. FfmpegTool.CreateAll(TestEngines.FfmpegRunner)];
    private static readonly ToolService Service = new(Tools);

    public static TheoryData<string, string> AllApplications()
    {
        var data = new TheoryData<string, string>();
        foreach (var tool in Tools.Where(t => t.IsAvailable))
        {
            foreach (var format in KnownFormats.All.Where(tool.AppliesTo))
            {
                data.Add(tool.Id, format.Id);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllApplications))]
    public async Task Herramienta_ProduceUnArchivoValido(string toolId, string formatId)
    {
        var tool = Tools.Single(t => t.Id == toolId);
        var format = KnownFormats.FindById(formatId)!;
        var sample = await Corpus.GetAsync(format);
        Assert.SkipWhen(sample is null, $"El entorno no puede generar una muestra {format.DisplayName}.");

        // Copia aislada: la salida aparece junto a la entrada.
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "kumquat-tools", Guid.NewGuid().ToString("N"))).FullName;
        var input = Path.Combine(dir, "muestra" + format.DefaultExtension);
        File.Copy(sample!, input);
        try
        {
            var result = await Service.ApplyAsync(input, toolId, null, TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
            var output = result.Value!;
            Path.GetFileName(output).ShouldBe($"muestra ({tool.OutputSuffix}){tool.OutputFormat(format).DefaultExtension}");
            FormatSniffer.DetectFile(output).ShouldBe(tool.OutputFormat(format));
            File.Exists(input).ShouldBeTrue("El original nunca se toca.");
            await AssertEffectAsync(toolId, format, input, output);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static async Task AssertEffectAsync(string toolId, FileFormat format, string input, string output)
    {
        var ct = TestContext.Current.CancellationToken;
        switch (toolId)
        {
            case "image.compress" or "video.compress" when format != KnownFormats.Avi && format != KnownFormats.Wmv && format != KnownFormats.Tiff:
                new FileInfo(output).Length.ShouldBeLessThan(new FileInfo(input).Length, "Comprimir debe achicar el archivo.");
                break;
            case "image.rotate":
                var rotated = await MediaProbe.ProbeAsync(output, ct);
                (rotated.Width, rotated.Height).ShouldBe((Corpus.Height, Corpus.Width));
                break;
            case "image.half":
                var half = await MediaProbe.ProbeAsync(output, ct);
                (half.Width, half.Height).ShouldBe((Corpus.Width / 2, Corpus.Height / 2));
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
        }
    }
}

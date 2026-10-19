// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Collections.Concurrent;
using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;
using Kumquat.Infrastructure.Processes;

namespace Kumquat.Engines.Tests.Integration;

/// <summary>
/// Corpus sintético: todos los archivos de prueba se generan con fuentes lavfi de FFmpeg
/// (testsrc2, sine). Así el repo no carga binarios ni archivos con licencias de terceros.
/// </summary>
internal static class Corpus
{
    public const int Width = 320;
    public const int Height = 240;
    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(2);

    private static readonly string Root = Path.Combine(Path.GetTempPath(), "kumquat-corpus", Guid.NewGuid().ToString("N"));
    private static readonly ConcurrentDictionary<string, Lazy<Task<string?>>> Samples = new(StringComparer.Ordinal);

    private const string SvgSample =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"320\" height=\"240\" viewBox=\"0 0 320 240\">" +
        "<rect width=\"320\" height=\"240\" fill=\"#ff8c00\"/><circle cx=\"160\" cy=\"120\" r=\"80\" fill=\"#2e7d32\"/></svg>\n";

    /// <summary>Muestra del formato pedido, o <c>null</c> si el entorno no puede generarla.</summary>
    public static Task<string?> GetAsync(FileFormat format)
        => Samples.GetOrAdd(format.Id, _ => new Lazy<Task<string?>>(() => CreateAsync(format))).Value;

    private static async Task<string?> CreateAsync(FileFormat format)
    {
        Directory.CreateDirectory(Root);
        var path = Path.Combine(Root, "sample" + format.DefaultExtension);

        switch (format.Id)
        {
            case "wav":
                return await LavfiAsync(path, ["-f", "lavfi", "-i", $"sine=frequency=440:duration={Duration.TotalSeconds}", "-c:a", "pcm_s16le"]);
            case "mp4":
                return await LavfiAsync(path,
                [
                    "-f", "lavfi", "-i", $"testsrc2=size={Width}x{Height}:rate=25:duration={Duration.TotalSeconds}",
                    "-f", "lavfi", "-i", $"sine=frequency=440:duration={Duration.TotalSeconds}",
                    "-shortest", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac",
                ]);
            case "png":
                return await LavfiAsync(path, ["-f", "lavfi", "-i", $"testsrc2=size={Width}x{Height}", "-frames:v", "1", "-update", "1"]);
            case "svg":
                await File.WriteAllTextAsync(path, SvgSample);
                return path;
        }

        // El resto se deriva de las bases con los propios convertidores.
        var baseFormat = format.Category switch
        {
            FormatCategory.Audio => KnownFormats.Wav,
            FormatCategory.Video => KnownFormats.Mp4,
            _ when format == KnownFormats.Gif => KnownFormats.Mp4,
            FormatCategory.Image => KnownFormats.Png,
            _ => null,
        };
        if (baseFormat is null)
        {
            return null;
        }

        var source = await GetAsync(baseFormat);
        var edge = TestEngines.All.Where(c => c.IsAvailable).SelectMany(c => c.Edges).FirstOrDefault(e => e.From == baseFormat && e.To == format);
        if (source is null || edge is null)
        {
            return null;
        }

        var result = await TestEngines.ById(edge.ConverterId).ConvertAsync(new ConversionStep(source, path, edge), null, CancellationToken.None);
        return result.IsSuccess ? path : null;
    }

    private static async Task<string?> LavfiAsync(string output, IReadOnlyList<string> args)
    {
        if (TestEngines.FfmpegExe is not { } ffmpeg)
        {
            return null;
        }

        var outcome = await TestEngines.Runner.RunAsync(
            new ProcessSpec { FileName = ffmpeg, Arguments = ["-hide_banner", "-loglevel", "error", "-y", .. args, output], Timeout = TimeSpan.FromMinutes(2) },
            CancellationToken.None);
        return outcome.Succeeded ? output : throw new InvalidOperationException($"No se pudo generar {output}: {outcome.StandardErrorTail}");
    }
}

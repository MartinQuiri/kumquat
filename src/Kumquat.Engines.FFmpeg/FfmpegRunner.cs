// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Text;
using Kumquat.Core.Results;
using Kumquat.Infrastructure.Engines;
using Kumquat.Infrastructure.Processes;

namespace Kumquat.Engines.FFmpeg;

/// <summary>
/// Ejecuta <c>ffmpeg.exe</c> de forma segura (rutas <c>file:</c>, lista blanca de protocolos,
/// Job Object) con progreso real basado en la duración que informa <c>ffprobe</c>.
/// Lo comparten el convertidor y las herramientas de audio/video.
/// </summary>
public sealed class FfmpegRunner
{
    private static readonly TimeSpan RunTimeout = TimeSpan.FromHours(6);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(30);
    private const long MaxMemoryBytes = 4L * 1024 * 1024 * 1024;

    private readonly IProcessRunner _runner;
    private readonly string? _ffmpeg;
    private readonly string? _ffprobe;

    /// <summary>Crea el ejecutor localizando los binarios.</summary>
    public FfmpegRunner(IProcessRunner runner, EngineLocator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _ffmpeg = locator.Find("ffmpeg", Path.Combine("bin", "ffmpeg.exe"));
        _ffprobe = locator.Find("ffmpeg", Path.Combine("bin", "ffprobe.exe"));
    }

    /// <summary>Indica si FFmpeg está instalado.</summary>
    public bool IsAvailable => _ffmpeg is not null;

    /// <summary>Convierte <paramref name="input"/> en <paramref name="output"/> con los argumentos de salida dados.</summary>
    public async Task<Result> RunAsync(string input, IReadOnlyList<string> outputArguments, string output, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var duration = progress is null || _ffmpeg is null ? null : await ProbeDurationAsync(input, cancellationToken);
        return await ExecuteAsync(FfmpegArguments.Build(input, output, outputArguments), duration, progress, cancellationToken);
    }

    /// <summary>Ejecuta con varias entradas (unir); el progreso usa la duración total dada.</summary>
    public Task<Result> RunMultiAsync(IReadOnlyList<string> inputs, IReadOnlyList<string> outputArguments, string output, TimeSpan? totalDuration, IProgress<double>? progress, CancellationToken cancellationToken)
        => ExecuteAsync(FfmpegArguments.BuildMulti(inputs, output, outputArguments), totalDuration, progress, cancellationToken);

    /// <summary>Lee duración, resolución y pistas de un medio.</summary>
    public async Task<MediaInfo?> ProbeAsync(string input, CancellationToken cancellationToken)
    {
        if (_ffprobe is null)
        {
            return null;
        }

        var output = new StringBuilder();
        var outcome = await _runner.RunAsync(
            new ProcessSpec
            {
                FileName = _ffprobe,
                Arguments = FfmpegArguments.ProbeStreams(input),
                Timeout = ProbeTimeout,
                MaxMemoryBytes = MaxMemoryBytes,
                OnStandardOutputLine = line => output.AppendLine(line),
            },
            cancellationToken);
        return outcome.Succeeded ? MediaInfo.Parse(output.ToString()) : null;
    }

    /// <summary>Etiquetas del contenedor (título, artista...); las claves se comparan sin distinguir mayúsculas.</summary>
    public async Task<IReadOnlyDictionary<string, string>> ProbeTagsAsync(string input, CancellationToken cancellationToken)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (_ffprobe is null)
        {
            return tags;
        }

        var output = new StringBuilder();
        var outcome = await _runner.RunAsync(
            new ProcessSpec
            {
                FileName = _ffprobe,
                Arguments = FfmpegArguments.ProbeTags(input),
                Timeout = ProbeTimeout,
                MaxMemoryBytes = MaxMemoryBytes,
                OnStandardOutputLine = line => output.AppendLine(line),
            },
            cancellationToken);
        if (!outcome.Succeeded)
        {
            return tags;
        }

        try
        {
            // OGG/Opus guardan las etiquetas en la pista de audio; el resto, en el contenedor (que manda).
            using var doc = System.Text.Json.JsonDocument.Parse(output.ToString());
            if (doc.RootElement.TryGetProperty("streams", out var streams))
            {
                foreach (var stream in streams.EnumerateArray().Where(s => s.TryGetProperty("tags", out _)))
                {
                    foreach (var tag in stream.GetProperty("tags").EnumerateObject())
                    {
                        tags[tag.Name] = tag.Value.GetString() ?? string.Empty;
                    }
                }
            }

            if (doc.RootElement.TryGetProperty("format", out var format) && format.TryGetProperty("tags", out var values))
            {
                foreach (var tag in values.EnumerateObject())
                {
                    tags[tag.Name] = tag.Value.GetString() ?? string.Empty;
                }
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // Sin etiquetas legibles: se edita desde cero.
        }

        return tags;
    }

    private async Task<Result> ExecuteAsync(IReadOnlyList<string> arguments, TimeSpan? duration, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (_ffmpeg is null)
        {
            return new Error("engine-missing", "FFmpeg no está instalado.");
        }

        var parser = new FfmpegProgressParser(duration, p => progress?.Report(p));

        var outcome = await _runner.RunAsync(
            new ProcessSpec
            {
                FileName = _ffmpeg,
                Arguments = arguments,
                Timeout = RunTimeout,
                MaxMemoryBytes = MaxMemoryBytes,
                OnStandardOutputLine = parser.OnLine,
            },
            cancellationToken);

        if (outcome.TimedOut)
        {
            return new Error("engine-timeout", "FFmpeg superó el tiempo máximo.");
        }

        return outcome.Succeeded
            ? Result.Success
            : new Error("engine-failed", $"FFmpeg terminó con código {outcome.ExitCode}: {LastLines(outcome.StandardErrorTail, 3)}");
    }

    private static string LastLines(string text, int count)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" | ", lines.Skip(Math.Max(0, lines.Length - count)));
    }

    private async Task<TimeSpan?> ProbeDurationAsync(string input, CancellationToken cancellationToken)
    {
        if (_ffprobe is null)
        {
            return null;
        }

        var output = new StringBuilder();
        var outcome = await _runner.RunAsync(
            new ProcessSpec
            {
                FileName = _ffprobe,
                Arguments = FfmpegArguments.ProbeDuration(input),
                Timeout = ProbeTimeout,
                MaxMemoryBytes = MaxMemoryBytes,
                OnStandardOutputLine = line => output.AppendLine(line),
            },
            cancellationToken);
        return outcome.Succeeded ? FfmpegProgressParser.ParseDuration(output.ToString()) : null;
    }
}

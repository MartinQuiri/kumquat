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
        if (_ffmpeg is null)
        {
            return new Error("engine-missing", "FFmpeg no está instalado.");
        }

        var duration = progress is null ? null : await ProbeDurationAsync(input, cancellationToken);
        var parser = new FfmpegProgressParser(duration, p => progress?.Report(p));

        var outcome = await _runner.RunAsync(
            new ProcessSpec
            {
                FileName = _ffmpeg,
                Arguments = FfmpegArguments.Build(input, output, outputArguments),
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

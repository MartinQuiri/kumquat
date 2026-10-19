// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Text;
using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;
using Kumquat.Core.Results;
using Kumquat.Infrastructure.Engines;
using Kumquat.Infrastructure.Processes;

namespace Kumquat.Engines.FFmpeg;

/// <summary>Convertidor de audio y video basado en <c>ffmpeg.exe</c> fuera de proceso.</summary>
public sealed class FfmpegConverter : IConverter
{
    /// <summary>Identificador del convertidor.</summary>
    public const string ConverterId = "ffmpeg";

    private static readonly TimeSpan ConversionTimeout = TimeSpan.FromHours(6);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(30);
    private const long MaxMemoryBytes = 4L * 1024 * 1024 * 1024;

    private readonly IProcessRunner _runner;
    private readonly string? _ffmpeg;
    private readonly string? _ffprobe;

    /// <summary>Crea el convertidor.</summary>
    public FfmpegConverter(IProcessRunner runner, EngineLocator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _ffmpeg = locator.Find("ffmpeg", Path.Combine("bin", "ffmpeg.exe"));
        _ffprobe = locator.Find("ffmpeg", Path.Combine("bin", "ffprobe.exe"));
        Edges = BuildEdges();
    }

    /// <inheritdoc />
    public string Id => ConverterId;

    /// <inheritdoc />
    public bool IsAvailable => _ffmpeg is not null;

    /// <inheritdoc />
    public IReadOnlyCollection<ConversionEdge> Edges { get; }

    /// <inheritdoc />
    public async Task<Result> ConvertAsync(ConversionStep step, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (_ffmpeg is null)
        {
            return new Error("engine-missing", "FFmpeg no está instalado.");
        }

        var duration = progress is null ? null : await ProbeDurationAsync(step.InputPath, cancellationToken);
        var parser = new FfmpegProgressParser(duration, p => progress?.Report(p));

        var outcome = await _runner.RunAsync(
            new ProcessSpec
            {
                FileName = _ffmpeg,
                Arguments = FfmpegArguments.Build(step.InputPath, step.OutputPath, step.Edge.To),
                Timeout = ConversionTimeout,
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

    private static List<ConversionEdge> BuildEdges()
    {
        var edges = new List<ConversionEdge>();
        void Add(FileFormat from, FileFormat to)
        {
            if (from != to)
            {
                edges.Add(new ConversionEdge(from, to, ConverterId, FfmpegProfiles.Cost(from, to)));
            }
        }

        foreach (var from in FfmpegProfiles.Audio)
        {
            FfmpegProfiles.Audio.ToList().ForEach(to => Add(from, to));
        }

        foreach (var from in FfmpegProfiles.Video.Append(KnownFormats.Gif))
        {
            FfmpegProfiles.Video.ToList().ForEach(to => Add(from, to));
            FfmpegProfiles.Audio.Where(_ => from != KnownFormats.Gif).ToList().ForEach(to => Add(from, to));
            Add(from, KnownFormats.Gif);
        }

        foreach (var from in FfmpegProfiles.ImageIn)
        {
            FfmpegProfiles.ImageOut.ToList().ForEach(to => Add(from, to));
        }

        return edges;
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

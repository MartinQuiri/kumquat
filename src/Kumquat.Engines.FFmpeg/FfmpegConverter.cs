// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;
using Kumquat.Core.Results;

namespace Kumquat.Engines.FFmpeg;

/// <summary>Convertidor de audio y video basado en <c>ffmpeg.exe</c> fuera de proceso.</summary>
public sealed class FfmpegConverter : IConverter
{
    /// <summary>Identificador del convertidor.</summary>
    public const string ConverterId = "ffmpeg";

    private readonly FfmpegRunner _ffmpeg;

    /// <summary>Crea el convertidor.</summary>
    public FfmpegConverter(FfmpegRunner ffmpeg)
    {
        _ffmpeg = ffmpeg ?? throw new ArgumentNullException(nameof(ffmpeg));
        Edges = BuildEdges(ffmpeg);
    }

    /// <inheritdoc />
    public string Id => ConverterId;

    /// <inheritdoc />
    public bool IsAvailable => _ffmpeg.IsAvailable;

    /// <inheritdoc />
    public IReadOnlyCollection<ConversionEdge> Edges { get; }

    /// <inheritdoc />
    public Task<Result> ConvertAsync(ConversionStep step, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);
        var profile = FfmpegProfiles.For(step.Edge.To) ?? throw new ArgumentException($"Sin perfil FFmpeg para {step.Edge.To}.", nameof(step));
        return _ffmpeg.RunAsync(step.InputPath, profile, step.OutputPath, progress, cancellationToken);
    }

    private static List<ConversionEdge> BuildEdges(FfmpegRunner ffmpeg)
    {
        var edges = new List<ConversionEdge>();
        void Add(FileFormat from, FileFormat to)
        {
            if (from != to && ffmpeg.CanEncode(to))
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
}

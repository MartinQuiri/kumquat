// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Formats;
using Kumquat.Core.Results;
using Kumquat.Core.Tools;

namespace Kumquat.Engines.FFmpeg;

/// <summary>Herramienta de audio/video definida por sus argumentos de FFmpeg.</summary>
public sealed class FfmpegTool : ITool
{
    private static readonly FileFormat[] Mp4Family = [KnownFormats.Mp4, KnownFormats.Mov, KnownFormats.Mkv];

    private readonly FfmpegRunner _ffmpeg;
    private readonly FileFormat[] _appliesTo;
    private readonly Func<FileFormat, FileFormat> _outputFormat;
    private readonly Func<FileFormat, IReadOnlyList<string>> _arguments;

    private FfmpegTool(
        FfmpegRunner ffmpeg,
        string id,
        string label,
        string suffix,
        FileFormat[] appliesTo,
        Func<FileFormat, FileFormat> outputFormat,
        Func<FileFormat, IReadOnlyList<string>> arguments)
    {
        _ffmpeg = ffmpeg;
        Id = id;
        Label = label;
        OutputSuffix = suffix;
        _appliesTo = appliesTo;
        _outputFormat = outputFormat;
        _arguments = arguments;
    }

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public string Label { get; }

    /// <inheritdoc />
    public string OutputSuffix { get; }

    /// <inheritdoc />
    public bool IsAvailable => _ffmpeg.IsAvailable;

    /// <summary>Herramientas de audio y video, en el orden en que aparecen en la rueda.</summary>
    public static IReadOnlyList<ITool> CreateAll(FfmpegRunner ffmpeg)
    {
        ArgumentNullException.ThrowIfNull(ffmpeg);
        return
        [
            // Comprimir video: H.264 CRF 28 (o VP9 para WebM), máx. 1080p, audio 128 kbps.
            // AVI/WMV salen como MP4: sus códecs heredados no comprimen bien.
            new FfmpegTool(
                ffmpeg,
                "video.compress",
                "Comprimir",
                "comprimido",
                FfmpegProfiles.Video,
                input => input == KnownFormats.WebM || Mp4Family.Contains(input) ? input : KnownFormats.Mp4,
                input => input == KnownFormats.WebM
                    ? ["-map", "0:v:0?", "-map", "0:a?", "-sn", "-dn", "-vf", "scale='min(1920,iw)':-2", "-c:v", "libvpx-vp9", "-pix_fmt", "yuv420p", "-crf", "40", "-b:v", "0", "-row-mt", "1", "-deadline", "good", "-cpu-used", "5", "-c:a", "libopus", "-b:a", "96k"]
                    : ["-map", "0:v:0?", "-map", "0:a?", "-sn", "-dn", "-vf", "scale='min(1920,iw)':-2", "-c:v", "libx264", "-preset", "medium", "-crf", "28", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "128k", "-movflags", "+faststart"]),

            // Quitar audio: copia el video sin recodificar (instantáneo y sin pérdida).
            new FfmpegTool(
                ffmpeg,
                "video.mute",
                "Quitar audio",
                "sin audio",
                FfmpegProfiles.Video,
                input => input,
                _ => ["-map", "0:v", "-c", "copy", "-an", "-sn", "-dn"]),

            // Normalizar volumen al estándar de streaming (EBU R128, -16 LUFS). loudnorm trabaja
            // internamente a 192 kHz: se vuelve a 48 kHz (si no, WMA falla y WAV/FLAC salen 4x más pesados).
            new FfmpegTool(
                ffmpeg,
                "audio.normalize",
                "Normalizar",
                "normalizado",
                FfmpegProfiles.Audio,
                input => input,
                input => [.. FfmpegProfiles.For(input)!, "-af", "loudnorm=I=-16:TP=-1.5:LRA=11,aresample=48000"]),
        ];
    }

    /// <inheritdoc />
    public bool AppliesTo(FileFormat format) => _appliesTo.Contains(format);

    /// <inheritdoc />
    public FileFormat OutputFormat(FileFormat input) => _outputFormat(input);

    /// <inheritdoc />
    public Task<Result> ApplyAsync(ToolStep step, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);
        return _ffmpeg.RunAsync(step.InputPath, _arguments(step.InputFormat), step.OutputPath, progress, cancellationToken);
    }
}

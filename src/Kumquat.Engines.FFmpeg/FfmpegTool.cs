// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Formats;
using Kumquat.Core.Results;
using Kumquat.Core.Text;
using Kumquat.Core.Tools;

namespace Kumquat.Engines.FFmpeg;

/// <summary>Herramientas de audio y video con FFmpeg.</summary>
public sealed class FfmpegTool : ITool
{
    private static readonly FileFormat[] Mp4Family = [KnownFormats.Mp4, KnownFormats.Mov, KnownFormats.Mkv];

    private static readonly string[] H264 = ["-c:v", "libx264", "-preset", "medium", "-crf", "20", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart"];

    private readonly FfmpegRunner _ffmpeg;
    private readonly FileFormat[] _appliesTo;
    private readonly Func<FileFormat, FileFormat> _outputFormat;
    private readonly Func<ToolStep, IProgress<double>?, CancellationToken, Task<Result>> _apply;

    private FfmpegTool(
        FfmpegRunner ffmpeg,
        string id,
        string label,
        string suffix,
        FileFormat[] appliesTo,
        Func<FileFormat, FileFormat> outputFormat,
        Func<ToolStep, IProgress<double>?, CancellationToken, Task<Result>> apply,
        ToolMode mode = ToolMode.EachFile,
        int minInputs = 1)
    {
        _ffmpeg = ffmpeg;
        Id = id;
        Label = label;
        OutputSuffix = suffix;
        _appliesTo = appliesTo;
        _outputFormat = outputFormat;
        _apply = apply;
        Mode = mode;
        MinInputs = minInputs;
    }

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public string Label { get; }

    /// <inheritdoc />
    public string OutputSuffix { get; }

    /// <inheritdoc />
    public bool IsAvailable => _ffmpeg.IsAvailable;

    /// <inheritdoc />
    public ToolMode Mode { get; }

    /// <inheritdoc />
    public int MinInputs { get; }

    /// <summary>Herramientas de audio y video, en el orden en que aparecen en la rueda.</summary>
    public static IReadOnlyList<ITool> CreateAll(FfmpegRunner ffmpeg)
    {
        ArgumentNullException.ThrowIfNull(ffmpeg);
        return
        [
            Simple(ffmpeg, "video.compress", "Comprimir", "comprimido", FfmpegProfiles.Video, KeepContainerOrMp4, CompressArguments),
            Simple(ffmpeg, "video.mute", "Quitar audio", "sin audio", FfmpegProfiles.Video, f => f, _ => ["-map", "0:v", "-c", "copy", "-an", "-sn", "-dn"]),

            // Rotar 90° (horario): hay que recodificar; se mantiene el contenedor si es razonable.
            Simple(ffmpeg, "video.rotate", "Rotar 90°", "rotado", FfmpegProfiles.Video, KeepContainerOrMp4, f => ["-vf", "transpose=1", .. FfmpegProfiles.For(KeepContainerOrMp4(f))!]),

            // Fotograma representativo (filtro thumbnail: elige el más "típico" de los primeros 100).
            Simple(ffmpeg, "video.frame", "Fotograma", "fotograma", [.. FfmpegProfiles.Video, KnownFormats.Gif], _ => KnownFormats.Jpg, _ => ["-vf", "thumbnail", "-frames:v", "1", "-update", "1", "-q:v", "2"]),

            new FfmpegTool(ffmpeg, "video.merge", "Unir videos", "unido", FfmpegProfiles.Video, KeepContainerOrMp4, (step, progress, ct) => MergeVideosAsync(ffmpeg, step, progress, ct), ToolMode.Combine, minInputs: 2),

            // Normalizar al estándar de streaming (EBU R128, -16 LUFS). loudnorm trabaja internamente
            // a 192 kHz: se vuelve a 48 kHz (si no, WMA falla y WAV/FLAC salen 4x más pesados).
            Simple(ffmpeg, "audio.normalize", "Normalizar", "normalizado", FfmpegProfiles.Audio, f => f, f => [.. FfmpegProfiles.For(f)!, "-af", "loudnorm=I=-16:TP=-1.5:LRA=11,aresample=48000"]),

            new FfmpegTool(ffmpeg, "audio.merge", "Unir audios", "unido", FfmpegProfiles.Audio, f => f, (step, progress, ct) => MergeAudioAsync(ffmpeg, step, progress, ct), ToolMode.Combine, minInputs: 2),

            // Recortar: se recodifica para cortar exacto (copiar cortaría en el fotograma clave más cercano).
            new FfmpegTool(ffmpeg, "media.trim", "Recortar", "recortado", [.. FfmpegProfiles.Video, .. FfmpegProfiles.Audio], TrimOutput, (step, progress, ct) => TrimAsync(ffmpeg, step, progress, ct))
            {
                ParameterSource = (inputs, ct) => TrimParametersAsync(ffmpeg, inputs, ct),
                Validator = ValidateTrim,
            },

            // Metadatos: sin recodificar; los campos vacíos se borran.
            new FfmpegTool(ffmpeg, "media.metadata", "Metadatos", "metadatos", [.. FfmpegProfiles.Video, .. FfmpegProfiles.Audio], f => f, (step, progress, ct) => ffmpeg.RunAsync(step.InputPath, MediaMetadata.Arguments(step.InputFormat, step.Options), step.OutputPath, progress, ct))
            {
                ParameterSource = async (inputs, ct) => MediaMetadata.Parameters(await ffmpeg.ProbeTagsAsync(inputs[0].Path, ct), inputs[0].Format!),
                Validator = MediaMetadata.Validate,
            },
        ];
    }

    /// <inheritdoc />
    public bool AppliesTo(FileFormat format) => _appliesTo.Contains(format);

    /// <inheritdoc />
    public FileFormat? OutputFormat(FileFormat? input) => _outputFormat(input ?? throw new ArgumentNullException(nameof(input)));

    /// <summary>Origen de los parámetros a pedir (si la herramienta los necesita).</summary>
    private Func<IReadOnlyList<ToolInput>, CancellationToken, Task<IReadOnlyList<ToolParameter>>>? ParameterSource { get; init; }

    /// <summary>Validación de las opciones elegidas.</summary>
    private Func<IReadOnlyDictionary<string, string>, string?>? Validator { get; init; }

    /// <inheritdoc />
    public Task<IReadOnlyList<ToolParameter>> GetParametersAsync(IReadOnlyList<ToolInput> inputs, CancellationToken cancellationToken)
        => ParameterSource?.Invoke(inputs, cancellationToken) ?? Task.FromResult<IReadOnlyList<ToolParameter>>([]);

    /// <inheritdoc />
    public string? Validate(IReadOnlyDictionary<string, string> options) => Validator?.Invoke(options);

    /// <inheritdoc />
    public Task<Result> ApplyAsync(ToolStep step, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);
        return _apply(step, progress, cancellationToken);
    }

    private static FfmpegTool Simple(FfmpegRunner ffmpeg, string id, string label, string suffix, FileFormat[] appliesTo, Func<FileFormat, FileFormat> output, Func<FileFormat, IReadOnlyList<string>> arguments)
        => new(ffmpeg, id, label, suffix, appliesTo, output, (step, progress, ct) => ffmpeg.RunAsync(step.InputPath, arguments(step.InputFormat), step.OutputPath, progress, ct));

    private static FileFormat TrimOutput(FileFormat input) => input.Category == FormatCategory.Audio ? input : KeepContainerOrMp4(input);

    private static async Task<IReadOnlyList<ToolParameter>> TrimParametersAsync(FfmpegRunner ffmpeg, IReadOnlyList<ToolInput> inputs, CancellationToken cancellationToken)
    {
        var info = await ffmpeg.ProbeAsync(inputs[0].Path, cancellationToken);
        var duration = info?.Duration is { } d ? TimeText.Format(d) : string.Empty;
        return
        [
            new ToolParameter("start", "Desde", ToolParameterKind.Time, "0:00", "Minutos:segundos, p. ej. 0:15", Required: true),
            new ToolParameter("end", "Hasta", ToolParameterKind.Time, duration, duration.Length > 0 ? $"Duración total: {duration}" : null, Required: true),
        ];
    }

    private static string? ValidateTrim(IReadOnlyDictionary<string, string> options)
    {
        if (!TimeText.TryParse(options.GetValueOrDefault("start"), out var start))
        {
            return "El inicio no es un tiempo válido (usa minutos:segundos, p. ej. 1:05).";
        }

        if (!TimeText.TryParse(options.GetValueOrDefault("end"), out var end))
        {
            return "El final no es un tiempo válido (usa minutos:segundos, p. ej. 2:30).";
        }

        return end > start ? null : "El final tiene que ser después del inicio.";
    }

    private static Task<Result> TrimAsync(FfmpegRunner ffmpeg, ToolStep step, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        // Las opciones ya pasaron por ValidateTrim (ToolService valida antes de ejecutar).
        if (!TimeText.TryParse(step.Option("start"), out var start) || !TimeText.TryParse(step.Option("end"), out var end) || end <= start)
        {
            return Task.FromResult(Result.Failure(new Error("invalid-options", "Rango de tiempo inválido.")));
        }

        var profile = FfmpegProfiles.For(step.OutputFormat!)!;
        return ffmpeg.RunAsync(step.InputPath, ["-ss", TimeText.ToSeconds(start), "-t", TimeText.ToSeconds(end - start), .. profile], step.OutputPath, progress, cancellationToken);
    }

    /// <summary>AVI y WMV salen como MP4: sus códecs heredados no comprimen bien.</summary>
    private static FileFormat KeepContainerOrMp4(FileFormat input)
        => input == KnownFormats.WebM || Mp4Family.Contains(input) ? input : KnownFormats.Mp4;

    /// <summary>H.264 CRF 28 (o VP9 para WebM), máx. 1080p, audio 128 kbps.</summary>
    private static IReadOnlyList<string> CompressArguments(FileFormat input)
        => input == KnownFormats.WebM
            ? ["-map", "0:v:0?", "-map", "0:a?", "-sn", "-dn", "-vf", "scale='min(1920,iw)':-2", "-c:v", "libvpx-vp9", "-pix_fmt", "yuv420p", "-crf", "40", "-b:v", "0", "-row-mt", "1", "-deadline", "good", "-cpu-used", "5", "-c:a", "libopus", "-b:a", "96k"]
            : ["-map", "0:v:0?", "-map", "0:a?", "-sn", "-dn", "-vf", "scale='min(1920,iw)':-2", "-c:v", "libx264", "-preset", "medium", "-crf", "28", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "128k", "-movflags", "+faststart"];

    private static async Task<Result> MergeVideosAsync(FfmpegRunner ffmpeg, ToolStep step, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var clips = new List<MediaInfo>(step.Inputs.Count);
        foreach (var input in step.Inputs)
        {
            var info = await ffmpeg.ProbeAsync(input.Path, cancellationToken);
            if (info is null || !info.HasVideo)
            {
                return new Error("engine-failed", $"No se pudo leer el video '{Path.GetFileName(input.Path)}'.");
            }

            clips.Add(info);
        }

        // Los clips se escalan a la resolución del primero (en orden natural de nombre).
        var width = MergeGraph.Even(clips[0].Width);
        var height = MergeGraph.Even(clips[0].Height);
        var output = step.OutputFormat!;
        var codec = output == KnownFormats.WebM ? FfmpegProfiles.For(KnownFormats.WebM)!.SkipWhile(a => !string.Equals(a, "-c:v", StringComparison.Ordinal)).ToArray() : H264;
        var total = clips.TrueForAll(c => c.Duration is not null) ? TimeSpan.FromTicks(clips.Sum(c => c.Duration!.Value.Ticks)) : (TimeSpan?)null;
        return await ffmpeg.RunMultiAsync(
            [.. step.Inputs.Select(i => i.Path)],
            ["-filter_complex", MergeGraph.Video(clips, width, height), "-map", "[v]", "-map", "[a]", .. codec],
            step.OutputPath,
            total,
            progress,
            cancellationToken);
    }

    private static async Task<Result> MergeAudioAsync(FfmpegRunner ffmpeg, ToolStep step, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        TimeSpan? total = TimeSpan.Zero;
        foreach (var input in step.Inputs)
        {
            var info = await ffmpeg.ProbeAsync(input.Path, cancellationToken);
            total = info?.Duration is { } d && total is { } t ? t + d : null;
        }

        return await ffmpeg.RunMultiAsync(
            [.. step.Inputs.Select(i => i.Path)],
            ["-filter_complex", MergeGraph.Audio(step.Inputs.Count), "-map", "[a]", .. FfmpegProfiles.For(step.OutputFormat!)!.Where(a => a is not ("-vn" or "-sn" or "-dn"))],
            step.OutputPath,
            total,
            progress,
            cancellationToken);
    }
}

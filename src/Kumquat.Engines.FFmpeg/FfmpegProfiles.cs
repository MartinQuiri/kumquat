// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Collections.Frozen;
using System.Runtime.InteropServices;
using Kumquat.Core.Formats;

namespace Kumquat.Engines.FFmpeg;

/// <summary>
/// Perfiles de codificación por formato de salida. Los valores priorizan calidad
/// visual/auditiva con tamaños razonables y máxima compatibilidad de reproducción.
/// </summary>
internal static class FfmpegProfiles
{
    /// <summary>Formatos de audio que FFmpeg lee y escribe.</summary>
    public static readonly FileFormat[] Audio =
        [KnownFormats.Mp3, KnownFormats.M4a, KnownFormats.Wav, KnownFormats.Flac, KnownFormats.Ogg, KnownFormats.Opus, KnownFormats.Aiff, KnownFormats.Wma];

    /// <summary>Formatos de video que FFmpeg lee y escribe.</summary>
    public static readonly FileFormat[] Video =
        [KnownFormats.Mp4, KnownFormats.Mov, KnownFormats.Mkv, KnownFormats.WebM, KnownFormats.Avi, KnownFormats.Wmv];

    /// <summary>Imágenes que FFmpeg convierte mejor que libvips (BMP no lo escribe libvips).</summary>
    public static readonly FileFormat[] ImageIn = [KnownFormats.Png, KnownFormats.Jpg, KnownFormats.Bmp, KnownFormats.Tiff, KnownFormats.WebP];

    /// <summary>Imágenes que FFmpeg escribe en este motor.</summary>
    public static readonly FileFormat[] ImageOut = [KnownFormats.Bmp, KnownFormats.Png];

    /// <summary>
    /// VP9 si el FFmpeg empaquetado lo trae. El de ARM64 (BtbN) se compila con --disable-libvpx,
    /// así que ahí WebM va en AV1 (SVT-AV1): WebM lo admite y lo reproducen Chrome, Edge y Firefox.
    /// </summary>
    public static bool HasVp9 { get; } = RuntimeInformation.ProcessArchitecture != Architecture.Arm64;

    private static readonly FrozenSet<FileFormat> LosslessAudio = new[] { KnownFormats.Wav, KnownFormats.Flac, KnownFormats.Aiff }.ToFrozenSet();

    private static readonly FrozenDictionary<string, string[]> OutputArgs = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        // Video: H.264/AAC es lo que reproduce todo (Windows, iPhone, navegadores, TVs).
        ["mp4"] = ["-map", "0:v:0?", "-map", "0:a?", "-sn", "-dn", "-c:v", "libx264", "-preset", "medium", "-crf", "20", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart"],
        ["mov"] = ["-map", "0:v:0?", "-map", "0:a?", "-sn", "-dn", "-c:v", "libx264", "-preset", "medium", "-crf", "20", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart"],
        ["mkv"] = ["-map", "0:v:0?", "-map", "0:a?", "-sn", "-dn", "-c:v", "libx264", "-preset", "medium", "-crf", "20", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "192k"],
        ["webm"] = ["-map", "0:v:0?", "-map", "0:a?", "-sn", "-dn", .. WebMVideo(vp9Crf: "32", vp9CpuUsed: "4", av1Crf: "35"), "-c:a", "libopus", "-b:a", "128k"],
        ["avi"] = ["-map", "0:v:0?", "-map", "0:a?", "-sn", "-dn", "-c:v", "mpeg4", "-vtag", "xvid", "-pix_fmt", "yuv420p", "-q:v", "3", "-c:a", "libmp3lame", "-q:a", "2"],
        ["wmv"] = ["-map", "0:v:0?", "-map", "0:a?", "-sn", "-dn", "-c:v", "wmv2", "-pix_fmt", "yuv420p", "-q:v", "3", "-c:a", "wmav2", "-b:a", "192k"],

        // GIF con paleta propia por clip: mucho mejor que la paleta web por defecto.
        ["gif"] = ["-map", "0:v:0", "-an", "-vf", "fps=15,scale='min(720,iw)':-2:flags=lanczos,split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=5", "-loop", "0"],

        // Audio: se descarta cualquier pista de video (incluida la carátula, que rompe OGG/WAV).
        ["mp3"] = ["-vn", "-sn", "-dn", "-c:a", "libmp3lame", "-q:a", "2"],
        ["m4a"] = ["-vn", "-sn", "-dn", "-c:a", "aac", "-b:a", "256k", "-movflags", "+faststart"],
        ["wav"] = ["-vn", "-sn", "-dn", "-c:a", "pcm_s16le"],
        ["flac"] = ["-vn", "-sn", "-dn", "-c:a", "flac"],
        ["ogg"] = ["-vn", "-sn", "-dn", "-c:a", "libvorbis", "-q:a", "6"],
        ["opus"] = ["-vn", "-sn", "-dn", "-c:a", "libopus", "-b:a", "160k"],
        ["aiff"] = ["-vn", "-sn", "-dn", "-c:a", "pcm_s16be"],
        ["wma"] = ["-vn", "-sn", "-dn", "-c:a", "wmav2", "-b:a", "192k"],

        // Imagen fija: primer cuadro.
        ["bmp"] = ["-frames:v", "1", "-update", "1", "-c:v", "bmp"],
        ["png"] = ["-frames:v", "1", "-update", "1", "-c:v", "png"],
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Códec de video para WebM: VP9 o, si este FFmpeg no lo trae, AV1 con calidad equivalente.</summary>
    public static string[] WebMVideo(string vp9Crf, string vp9CpuUsed, string av1Crf) => HasVp9
        ? ["-c:v", "libvpx-vp9", "-pix_fmt", "yuv420p", "-crf", vp9Crf, "-b:v", "0", "-row-mt", "1", "-deadline", "good", "-cpu-used", vp9CpuUsed]
        : ["-c:v", "libsvtav1", "-pix_fmt", "yuv420p", "-crf", av1Crf, "-preset", "8"];

    /// <summary>Argumentos de salida para un formato, o <c>null</c> si no hay perfil.</summary>
    public static IReadOnlyList<string>? For(FileFormat target) => OutputArgs.GetValueOrDefault(target.Id);

    /// <summary>Codificadores que piden unos argumentos de salida (los valores de <c>-c:v</c>, <c>-c:a</c> y <c>-c</c>, salvo <c>copy</c>).</summary>
    public static IEnumerable<string> Encoders(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return arguments.Zip(arguments.Skip(1))
            .Where(pair => pair.First is "-c:v" or "-c:a" or "-c" && !string.Equals(pair.Second, "copy", StringComparison.Ordinal))
            .Select(pair => pair.Second);
    }

    /// <summary>Costo de la arista para el planificador.</summary>
    public static double Cost(FileFormat from, FileFormat to)
    {
        if (to == KnownFormats.Gif)
        {
            return 4; // Pérdida fuerte de color y fps.
        }

        if (to.Category == FormatCategory.Video)
        {
            return 3; // Recodificar video es lento y con pérdida.
        }

        if (to.Category == FormatCategory.Audio)
        {
            // Pasar de lossless a lossless no pierde nada; a lossy, sí.
            return LosslessAudio.Contains(to) ? 1 : (from.Category == FormatCategory.Video ? 1.5 : 2);
        }

        return 2; // Imágenes vía FFmpeg: libvips es preferible cuando existe la arista.
    }
}

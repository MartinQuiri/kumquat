// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Collections.Frozen;
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

    private static readonly FrozenSet<FileFormat> LosslessAudio = new[] { KnownFormats.Wav, KnownFormats.Flac, KnownFormats.Aiff }.ToFrozenSet();

    private static readonly FrozenDictionary<string, string[]> OutputArgs = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        // Video: H.264/AAC es lo que reproduce todo (Windows, iPhone, navegadores, TVs).
        ["mp4"] = ["-map", "0:v:0?", "-map", "0:a?", "-sn", "-dn", "-c:v", "libx264", "-preset", "medium", "-crf", "20", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart"],
        ["mov"] = ["-map", "0:v:0?", "-map", "0:a?", "-sn", "-dn", "-c:v", "libx264", "-preset", "medium", "-crf", "20", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart"],
        ["mkv"] = ["-map", "0:v:0?", "-map", "0:a?", "-sn", "-dn", "-c:v", "libx264", "-preset", "medium", "-crf", "20", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "192k"],
        ["webm"] = ["-map", "0:v:0?", "-map", "0:a?", "-sn", "-dn", "-c:v", "libvpx-vp9", "-pix_fmt", "yuv420p", "-crf", "32", "-b:v", "0", "-row-mt", "1", "-deadline", "good", "-cpu-used", "4", "-c:a", "libopus", "-b:a", "128k"],
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

    /// <summary>Argumentos de salida para un formato, o <c>null</c> si no hay perfil.</summary>
    public static IReadOnlyList<string>? For(FileFormat target) => OutputArgs.GetValueOrDefault(target.Id);

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

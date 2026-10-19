// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Globalization;
using Kumquat.Core.Formats;
using Kumquat.Core.Text;

namespace Kumquat.Engines.FFmpeg;

/// <summary>Construye la línea de comandos de FFmpeg de forma segura.</summary>
internal static class FfmpegArguments
{
    /// <summary>
    /// Argumentos para convertir <paramref name="input"/> en <paramref name="output"/>.
    /// Las rutas van con prefijo <c>file:</c>: así un archivo llamado <c>-y.mp4</c> o
    /// <c>concat:a|b</c> nunca se interpreta como opción ni como protocolo.
    /// </summary>
    public static IReadOnlyList<string> Build(string input, string output, FileFormat target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var profile = FfmpegProfiles.For(target) ?? throw new ArgumentException($"Sin perfil FFmpeg para {target}.", nameof(target));
        return Build(input, output, profile);
    }

    /// <summary>Igual que <see cref="Build(string, string, FileFormat)"/> con argumentos de salida explícitos.</summary>
    /// <param name="input">Archivo de entrada.</param>
    /// <param name="output">Archivo de salida.</param>
    /// <param name="profile">Argumentos de salida.</param>
    /// <param name="inputOptions">Opciones de entrada (antes de <c>-i</c>), p. ej. <c>-ss</c> para saltar rápido y exacto.</param>
    public static IReadOnlyList<string> Build(string input, string output, IReadOnlyList<string> profile, IReadOnlyList<string>? inputOptions = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(input);
        ArgumentException.ThrowIfNullOrEmpty(output);
        ArgumentNullException.ThrowIfNull(profile);

        List<string> args =
        [
            "-hide_banner", "-nostdin", "-nostats", "-loglevel", "error", "-y",
            "-protocol_whitelist", "file,pipe",
        ];
        args.AddRange(inputOptions ?? []);
        args.AddRange(["-i", ToFileUrl(input), "-map_metadata", "0"]);
        args.AddRange(profile);
        args.AddRange(["-progress", "pipe:1", ToFileUrl(output)]);
        return args;
    }

    /// <summary>Como <see cref="Build(string, string, IReadOnlyList{string}, IReadOnlyList{string})"/> con varias entradas (para unir).</summary>
    public static IReadOnlyList<string> BuildMulti(IReadOnlyList<string> inputs, string output, IReadOnlyList<string> profile)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentException.ThrowIfNullOrEmpty(output);
        ArgumentNullException.ThrowIfNull(profile);

        List<string> args = ["-hide_banner", "-nostdin", "-nostats", "-loglevel", "error", "-y"];
        foreach (var input in inputs)
        {
            args.AddRange(["-protocol_whitelist", "file,pipe", "-i", ToFileUrl(input)]);
        }

        args.AddRange(profile);
        args.AddRange(["-progress", "pipe:1", ToFileUrl(output)]);
        return args;
    }

    /// <summary>Argumentos de ffprobe para leer duración, resolución y pistas en JSON.</summary>
    public static IReadOnlyList<string> ProbeStreams(string input)
        =>
        [
            "-v", "error", "-protocol_whitelist", "file", "-show_entries", "stream=codec_type,width,height:format=duration",
            "-of", "json", ToFileUrl(input),
        ];

    /// <summary>Argumentos de ffprobe para leer las etiquetas (título, artista...) en JSON.</summary>
    public static IReadOnlyList<string> ProbeTags(string input)
        => ["-v", "error", "-protocol_whitelist", "file", "-show_entries", "format_tags:stream_tags", "-select_streams", "a:0", "-of", "json", ToFileUrl(input)];

    /// <summary>Argumentos de ffprobe para leer la duración.</summary>
    public static IReadOnlyList<string> ProbeDuration(string input)
        =>
        [
            "-v", "error", "-protocol_whitelist", "file", "-show_entries", "format=duration",
            "-of", "default=noprint_wrappers=1:nokey=1", ToFileUrl(input),
        ];

    /// <summary>Ruta absoluta con prefijo de protocolo <c>file:</c>.</summary>
    /// <summary>
    /// Video sin comprimir (BGRA) a stdout, en un recuadro fijo de <paramref name="width"/>×<paramref name="height"/>
    /// (se encaja con bandas negras: así el tamaño es conocido aunque el video venga rotado).
    /// </summary>
    /// <param name="input">Archivo.</param>
    /// <param name="from">Instante inicial (búsqueda exacta).</param>
    /// <param name="duration">Duración a leer, o <c>null</c> para un solo fotograma.</param>
    /// <param name="width">Ancho del recuadro (par).</param>
    /// <param name="height">Alto del recuadro (par).</param>
    /// <param name="fps">Fotogramas por segundo de salida.</param>
    public static IReadOnlyList<string> RawVideo(string input, TimeSpan from, TimeSpan? duration, int width, int height, int fps)
    {
        var size = string.Create(CultureInfo.InvariantCulture, $"{width}:{height}");
        var filter = (duration is null ? string.Empty : string.Create(CultureInfo.InvariantCulture, $"fps={fps},"))
            + $"scale={size}:force_original_aspect_ratio=decrease:flags=bicubic,pad={size}:-1:-1:color=black";
        List<string> args = ["-hide_banner", "-nostdin", "-nostats", "-loglevel", "error", "-ss", TimeText.ToSeconds(from)];
        if (duration is { } d)
        {
            args.AddRange(["-t", TimeText.ToSeconds(d)]);
        }

        args.AddRange(["-protocol_whitelist", "file,pipe", "-i", ToFileUrl(input), "-an", "-sn", "-vf", filter, "-pix_fmt", "bgra", "-f", "rawvideo"]);
        if (duration is null)
        {
            args.AddRange(["-frames:v", "1"]);
        }

        args.Add("pipe:1");
        return args;
    }

    /// <summary>Audio PCM de 16 bits estéreo a stdout, desde un instante.</summary>
    public static IReadOnlyList<string> RawAudio(string input, TimeSpan from, TimeSpan duration, int sampleRate)
        =>
        [
            "-hide_banner", "-nostdin", "-nostats", "-loglevel", "error",
            "-ss", TimeText.ToSeconds(from), "-t", TimeText.ToSeconds(duration),
            "-protocol_whitelist", "file,pipe", "-i", ToFileUrl(input),
            "-vn", "-sn", "-ac", "2", "-ar", sampleRate.ToString(CultureInfo.InvariantCulture), "-f", "s16le", "pipe:1",
        ];

    public static string ToFileUrl(string path) => "file:" + Path.GetFullPath(path);
}

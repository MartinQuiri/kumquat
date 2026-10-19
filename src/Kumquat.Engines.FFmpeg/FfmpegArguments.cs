// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Formats;

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
    public static IReadOnlyList<string> Build(string input, string output, IReadOnlyList<string> profile)
    {
        ArgumentException.ThrowIfNullOrEmpty(input);
        ArgumentException.ThrowIfNullOrEmpty(output);
        ArgumentNullException.ThrowIfNull(profile);

        List<string> args =
        [
            "-hide_banner", "-nostdin", "-nostats", "-loglevel", "error", "-y",
            "-protocol_whitelist", "file,pipe",
            "-i", ToFileUrl(input),
            "-map_metadata", "0",
        ];
        args.AddRange(profile);
        args.AddRange(["-progress", "pipe:1", ToFileUrl(output)]);
        return args;
    }

    /// <summary>Argumentos de ffprobe para leer la duración.</summary>
    public static IReadOnlyList<string> ProbeDuration(string input)
        =>
        [
            "-v", "error", "-protocol_whitelist", "file", "-show_entries", "format=duration",
            "-of", "default=noprint_wrappers=1:nokey=1", ToFileUrl(input),
        ];

    /// <summary>Ruta absoluta con prefijo de protocolo <c>file:</c>.</summary>
    public static string ToFileUrl(string path) => "file:" + Path.GetFullPath(path);
}

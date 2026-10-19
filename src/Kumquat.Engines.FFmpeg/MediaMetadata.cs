// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Text.RegularExpressions;
using Kumquat.Core.Formats;
using Kumquat.Core.Tools;

namespace Kumquat.Engines.FFmpeg;

/// <summary>
/// Metadatos de audio y video (etiquetas del contenedor). Se reescriben con <c>-c copy</c>:
/// el audio y el video no se recodifican, el archivo queda idéntico salvo las etiquetas.
/// </summary>
internal static partial class MediaMetadata
{
    /// <summary>Campos conocidos: clave de FFmpeg → etiqueta visible.</summary>
    public static readonly (string Key, string Label)[] Fields =
    [
        ("title", "Título"),
        ("artist", "Artista / autor"),
        ("album", "Álbum"),
        ("date", "Año o fecha"),
        ("genre", "Género"),
        ("comment", "Comentario"),
        ("copyright", "Copyright"),
    ];

    /// <summary>Etiquetas técnicas que no se ofrecen para editar (las pone el propio contenedor).</summary>
    private static readonly HashSet<string> Technical = new(StringComparer.OrdinalIgnoreCase)
    {
        "encoder", "major_brand", "minor_version", "compatible_brands", "creation_time", "handler_name", "vendor_id", "duration",
    };

    /// <summary>Contenedores MP4/QuickTime: solo guardan etiquetas propias con <c>use_metadata_tags</c>.</summary>
    private static readonly FileFormat[] IsoContainers = [KnownFormats.Mp4, KnownFormats.Mov, KnownFormats.M4a];

    /// <summary>OGG/Opus: las etiquetas (comentarios Vorbis) van en la pista de audio.</summary>
    private static readonly FileFormat[] StreamTagged = [KnownFormats.Ogg, KnownFormats.Opus];

    /// <summary>WAV/AVI (RIFF INFO): solo una lista fija de etiquetas; las personalizadas se descartan.</summary>
    private static readonly FileFormat[] FixedTagSet = [KnownFormats.Wav, KnownFormats.Avi];

    public static IReadOnlyList<ToolParameter> Parameters(IReadOnlyDictionary<string, string> current, FileFormat format)
    {
        var known = Fields.Select(f => new ToolParameter(f.Key, f.Label, ToolParameterKind.Text, current.GetValueOrDefault(f.Key, string.Empty)));
        var custom = current
            .Where(t => !Technical.Contains(t.Key) && !Fields.Any(f => string.Equals(f.Key, t.Key, StringComparison.OrdinalIgnoreCase)))
            .Select(t => new ToolParameter(ToolParameter.CustomPrefix + t.Key, t.Key, ToolParameterKind.Text, t.Value));
        return FixedTagSet.Contains(format)
            ? [.. known]
            : [.. known, .. custom, new ToolParameter("custom", "Campos personalizados", ToolParameterKind.CustomFields)];
    }

    public static string? Validate(IReadOnlyDictionary<string, string> options)
    {
        var bad = options.Keys.FirstOrDefault(k => k.StartsWith(ToolParameter.CustomPrefix, StringComparison.Ordinal) && !ValidKey().IsMatch(k[ToolParameter.CustomPrefix.Length..]));
        return bad is null ? null : $"El nombre de campo '{bad[ToolParameter.CustomPrefix.Length..]}' solo puede tener letras, números, guion o guion bajo.";
    }

    /// <summary>Argumentos de salida: copia todo y aplica las etiquetas (valor vacío = borrar la etiqueta).</summary>
    public static IReadOnlyList<string> Arguments(FileFormat format, IReadOnlyDictionary<string, string>? options)
    {
        List<string> args = ["-map", "0:v?", "-map", "0:a?", "-c", "copy", "-map_metadata", "0"];
        if (IsoContainers.Contains(format))
        {
            args.AddRange(["-movflags", "use_metadata_tags"]);
        }

        if (format == KnownFormats.Aiff)
        {
            args.AddRange(["-write_id3v2", "1"]);
        }

        var target = StreamTagged.Contains(format) ? "-metadata:s:a:0" : "-metadata";

        foreach (var (key, value) in options ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            var tag = key.StartsWith(ToolParameter.CustomPrefix, StringComparison.Ordinal) ? key[ToolParameter.CustomPrefix.Length..] : key;
            if (ValidKey().IsMatch(tag))
            {
                args.AddRange([target, $"{tag}={value}"]);
            }
        }

        return args;
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 200)]
    private static partial Regex ValidKey();
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Globalization;
using System.Text;

namespace Kumquat.Engines.FFmpeg;

/// <summary>
/// Construye el grafo de filtros para unir medios con el filtro <c>concat</c>. Los clips pueden
/// tener resoluciones distintas (se escalan al primero con bandas negras) y algunos pueden no
/// tener audio (se rellena con silencio): sin esto, <c>concat</c> falla o desincroniza.
/// </summary>
internal static class MergeGraph
{
    /// <summary>Grafo para unir videos; salidas <c>[v]</c> y <c>[a]</c>.</summary>
    /// <param name="clips">Información de cada clip, en orden.</param>
    /// <param name="width">Ancho de salida (par).</param>
    /// <param name="height">Alto de salida (par).</param>
    public static string Video(IReadOnlyList<MediaInfo> clips, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(clips);
        var graph = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;
        for (var i = 0; i < clips.Count; i++)
        {
            graph.Append(inv, $"[{i}:v]scale={width}:{height}:force_original_aspect_ratio=decrease,pad={width}:{height}:(ow-iw)/2:(oh-ih)/2,setsar=1,fps=30,format=yuv420p[v{i}];");
            graph.Append(clips[i].HasAudio
                ? string.Create(inv, $"[{i}:a]aresample=48000,aformat=channel_layouts=stereo[a{i}];")
                : string.Create(inv, $"anullsrc=r=48000:cl=stereo,atrim=duration={Seconds(clips[i])}[a{i}];"));
        }

        for (var i = 0; i < clips.Count; i++)
        {
            graph.Append(inv, $"[v{i}][a{i}]");
        }

        graph.Append(inv, $"concat=n={clips.Count}:v=1:a=1[v][a]");
        return graph.ToString();
    }

    /// <summary>Grafo para unir audios; salida <c>[a]</c>.</summary>
    public static string Audio(int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        var graph = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;
        for (var i = 0; i < count; i++)
        {
            graph.Append(inv, $"[{i}:a]aresample=48000,aformat=channel_layouts=stereo[a{i}];");
        }

        for (var i = 0; i < count; i++)
        {
            graph.Append(inv, $"[a{i}]");
        }

        graph.Append(inv, $"concat=n={count}:v=0:a=1[a]");
        return graph.ToString();
    }

    /// <summary>Redondea a par (H.264 4:2:0 exige dimensiones pares).</summary>
    public static int Even(int value) => Math.Max(2, value - (value % 2));

    private static string Seconds(MediaInfo clip)
        => (clip.Duration ?? TimeSpan.FromSeconds(1)).TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
}

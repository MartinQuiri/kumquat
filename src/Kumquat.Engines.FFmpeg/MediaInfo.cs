// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Globalization;
using System.Text.Json;

namespace Kumquat.Engines.FFmpeg;

/// <summary>Lo que Kumquat necesita saber de un medio para unir o recortar.</summary>
/// <param name="Duration">Duración, si se conoce.</param>
/// <param name="HasVideo">Tiene pista de video.</param>
/// <param name="HasAudio">Tiene pista de audio.</param>
/// <param name="Width">Ancho del video.</param>
/// <param name="Height">Alto del video.</param>
public sealed record MediaInfo(TimeSpan? Duration, bool HasVideo, bool HasAudio, int Width, int Height)
{
    /// <summary>Interpreta la salida JSON de <see cref="FfmpegArguments.ProbeStreams"/>.</summary>
    public static MediaInfo? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var streams = root.TryGetProperty("streams", out var s) ? s.EnumerateArray().ToList() : [];
            var video = streams.FirstOrDefault(x => x.TryGetProperty("codec_type", out var t) && string.Equals(t.GetString(), "video", StringComparison.Ordinal));
            var hasVideo = video.ValueKind == JsonValueKind.Object;
            var hasAudio = streams.Exists(x => x.TryGetProperty("codec_type", out var t) && string.Equals(t.GetString(), "audio", StringComparison.Ordinal));
            TimeSpan? duration = root.TryGetProperty("format", out var f) && f.TryGetProperty("duration", out var d)
                && double.TryParse(d.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
                ? TimeSpan.FromSeconds(seconds)
                : null;
            return new MediaInfo(
                duration,
                hasVideo,
                hasAudio,
                hasVideo && video.TryGetProperty("width", out var w) ? w.GetInt32() : 0,
                hasVideo && video.TryGetProperty("height", out var h) ? h.GetInt32() : 0);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

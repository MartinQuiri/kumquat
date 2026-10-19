// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Globalization;
using System.Text;
using System.Text.Json;
using Kumquat.Infrastructure.Processes;

namespace Kumquat.Engines.Tests.Integration;

/// <summary>Propiedades de un medio según ffprobe.</summary>
internal sealed record MediaInfo(bool HasVideo, bool HasAudio, int? Width, int? Height, double? DurationSeconds, string? VideoCodec, string? AudioCodec, int? SampleRate);

/// <summary>Lee propiedades de un medio con ffprobe, para afirmar sobre la salida sin comparar bytes.</summary>
internal static class MediaProbe
{
    public static async Task<MediaInfo> ProbeAsync(string path, CancellationToken cancellationToken)
    {
        var json = new StringBuilder();
        var outcome = await TestEngines.Runner.RunAsync(
            new ProcessSpec
            {
                FileName = TestEngines.FfprobeExe ?? throw new InvalidOperationException("ffprobe no está instalado."),
                Arguments = ["-v", "error", "-show_streams", "-show_format", "-of", "json", "file:" + path],
                Timeout = TimeSpan.FromSeconds(30),
                OnStandardOutputLine = l => json.AppendLine(l),
            },
            cancellationToken);
        outcome.Succeeded.ShouldBeTrue($"ffprobe falló con {path}: {outcome.StandardErrorTail}");

        using var doc = JsonDocument.Parse(json.ToString());
        var streams = doc.RootElement.GetProperty("streams").EnumerateArray().ToList();
        var video = streams.FirstOrDefault(s => s.GetProperty("codec_type").GetString() == "video");
        var audio = streams.FirstOrDefault(s => s.GetProperty("codec_type").GetString() == "audio");
        double? duration = doc.RootElement.TryGetProperty("format", out var format) && format.TryGetProperty("duration", out var d)
            ? double.Parse(d.GetString()!, CultureInfo.InvariantCulture)
            : null;

        return new MediaInfo(
            HasVideo: video.ValueKind != JsonValueKind.Undefined,
            HasAudio: audio.ValueKind != JsonValueKind.Undefined,
            Width: video.ValueKind != JsonValueKind.Undefined ? video.GetProperty("width").GetInt32() : null,
            Height: video.ValueKind != JsonValueKind.Undefined ? video.GetProperty("height").GetInt32() : null,
            DurationSeconds: duration,
            VideoCodec: video.ValueKind != JsonValueKind.Undefined ? video.GetProperty("codec_name").GetString() : null,
            AudioCodec: audio.ValueKind != JsonValueKind.Undefined ? audio.GetProperty("codec_name").GetString() : null,
            SampleRate: audio.ValueKind != JsonValueKind.Undefined && audio.TryGetProperty("sample_rate", out var rate) ? int.Parse(rate.GetString()!, CultureInfo.InvariantCulture) : null);
    }
}

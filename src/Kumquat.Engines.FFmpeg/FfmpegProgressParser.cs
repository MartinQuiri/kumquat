// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Globalization;

namespace Kumquat.Engines.FFmpeg;

/// <summary>
/// Interpreta la salida de <c>-progress pipe:1</c> (líneas <c>clave=valor</c>) y la
/// convierte en un avance 0..1 usando la duración total del medio.
/// </summary>
internal sealed class FfmpegProgressParser
{
    private readonly double _totalSeconds;
    private readonly Action<double> _report;
    private double _last = -1;

    public FfmpegProgressParser(TimeSpan? duration, Action<double> report)
    {
        _totalSeconds = duration is { TotalSeconds: > 0 } d ? d.TotalSeconds : 0;
        _report = report ?? throw new ArgumentNullException(nameof(report));
    }

    public void OnLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var eq = line.IndexOf('=', StringComparison.Ordinal);
        if (eq <= 0)
        {
            return;
        }

        var key = line.AsSpan(0, eq);
        var value = line.AsSpan(eq + 1).Trim();

        if (key.SequenceEqual("progress") && value.SequenceEqual("end"))
        {
            Emit(1.0);
            return;
        }

        // out_time_us y out_time_ms (este último también viene en microsegundos, por un bug histórico).
        if ((key.SequenceEqual("out_time_us") || key.SequenceEqual("out_time_ms"))
            && _totalSeconds > 0
            && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var micros)
            && micros >= 0)
        {
            Emit(Math.Min(0.999, micros / 1_000_000.0 / _totalSeconds));
        }
    }

    /// <summary>Interpreta la salida de <c>ffprobe -show_entries format=duration</c>.</summary>
    public static TimeSpan? ParseDuration(string? text)
    {
        return double.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0 && double.IsFinite(seconds)
            ? TimeSpan.FromSeconds(seconds)
            : null;
    }

    private void Emit(double value)
    {
        // Solo avances monótonos y de al menos 0,5 %: evita inundar la UI.
        if (value >= 1.0 || value - _last >= 0.005)
        {
            _last = value;
            _report(value);
        }
    }
}

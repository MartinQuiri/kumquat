// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Globalization;

namespace Kumquat.Core.Text;

/// <summary>
/// Instantes de un medio escritos como los escribe una persona: <c>83</c>, <c>1:23</c>,
/// <c>1:23.5</c>, <c>01:02:03</c>. Acepta coma o punto decimal.
/// </summary>
public static class TimeText
{
    /// <summary>Interpreta un instante; <c>false</c> si el texto no es válido o es negativo.</summary>
    public static bool TryParse(string? text, out TimeSpan value)
    {
        value = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Trim().Replace(',', '.').Split(':');
        if (parts.Length > 3 || parts.Any(p => p.Length == 0))
        {
            return false;
        }

        double seconds = 0;
        for (var i = 0; i < parts.Length; i++)
        {
            var isLast = i == parts.Length - 1;
            var style = isLast ? NumberStyles.AllowDecimalPoint : NumberStyles.None;
            if (!double.TryParse(parts[i], style, CultureInfo.InvariantCulture, out var number) || (!isLast && number >= 60 && i > 0) || (isLast && parts.Length > 1 && number >= 60))
            {
                return false;
            }

            seconds = (seconds * 60) + number;
        }

        if (!double.IsFinite(seconds) || seconds > TimeSpan.MaxValue.TotalSeconds / 2)
        {
            return false;
        }

        value = TimeSpan.FromSeconds(seconds);
        return true;
    }

    /// <summary>Formato legible y re-interpretable: <c>1:23.5</c> o <c>1:02:03</c>.</summary>
    public static string Format(TimeSpan value)
    {
        var text = value.TotalHours >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{(int)value.TotalHours}:{value.Minutes:00}:{value.Seconds:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{(int)value.TotalMinutes}:{value.Seconds:00}");
        var tenths = value.Milliseconds / 100;
        return tenths > 0 ? string.Create(CultureInfo.InvariantCulture, $"{text}.{tenths}") : text;
    }

    /// <summary>Segundos con punto decimal, para pasar a FFmpeg.</summary>
    public static string ToSeconds(TimeSpan value) => value.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
}

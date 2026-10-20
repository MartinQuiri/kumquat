// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Localization;

namespace Kumquat.Core.Text;

/// <summary>
/// Tiempo que falta, para leer de un vistazo: "casi listo", "quedan ~12 s", "quedan ~3 min".
/// Siempre se redondea hacia arriba: mejor que termine antes de lo dicho que después.
/// </summary>
public static class RemainingText
{
    /// <summary>Texto del tiempo restante; sin estimación, "calculando…".</summary>
    public static string Format(TimeSpan? remaining)
    {
        if (remaining is not { } left)
        {
            return Strings.ProgressCalculating;
        }

        if (left < TimeSpan.FromSeconds(5))
        {
            return Strings.ProgressAlmostDone;
        }

        if (left < TimeSpan.FromSeconds(60))
        {
            return Strings.ProgressSecondsLeft((int)Math.Ceiling(left.TotalSeconds));
        }

        var minutes = (int)Math.Ceiling(left.TotalMinutes);
        return minutes < 60 ? Strings.ProgressMinutesLeft(minutes) : Strings.ProgressHoursLeft(minutes / 60, minutes % 60);
    }
}

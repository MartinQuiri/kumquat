// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Interaction.Input;

/// <summary>
/// Detecta que Windows quitó los hooks de bajo nivel. Lo hace en silencio si un callback tarda
/// (equipo muy cargado, una pausa del recolector de basura) y no avisa: la rueda dejaría de
/// funcionar hasta reiniciar Kumquat. La señal es que Windows registró entrada (GetLastInputInfo)
/// que a los hooks nunca les llegó.
/// </summary>
internal static class HookWatchdog
{
    /// <summary>Cuánto puede llevar Windows viendo entrada que los hooks no vieron.</summary>
    public const int SilenceMs = 1500;

    /// <summary>Como mucho una reinstalación cada tanto (hay entrada que los hooks no ven, p. ej. hacia una ventana de administrador).</summary>
    public const int MinIntervalMs = 5000;

    /// <param name="lastSystemInput">Tick de la última entrada según Windows (<c>GetLastInputInfo</c>).</param>
    /// <param name="lastHookEvent">Tick del último evento que recibieron los hooks.</param>
    /// <param name="lastReinstall">Tick de la última reinstalación.</param>
    /// <param name="now">Tick actual.</param>
    /// <remarks>Los ticks son de 32 bits y dan la vuelta cada 49 días: se comparan por diferencia.</remarks>
    public static bool ShouldReinstall(uint lastSystemInput, uint lastHookEvent, uint lastReinstall, uint now)
        => unchecked((int)(lastSystemInput - lastHookEvent)) > SilenceMs
            && unchecked((int)(now - lastSystemInput)) >= 0
            && unchecked((int)(now - lastReinstall)) >= MinIntervalMs;
}

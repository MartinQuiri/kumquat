// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Interaction.Native;

namespace Kumquat.Interaction.Input;

/// <summary>Atajo global que abre la rueda sobre los archivos seleccionados en el Explorador.</summary>
/// <param name="Modifiers">Modificadores <c>MOD_*</c> de RegisterHotKey.</param>
/// <param name="VirtualKey">Tecla virtual.</param>
/// <param name="Display">Texto para el menú.</param>
public sealed record Hotkey(uint Modifiers, uint VirtualKey, string Display)
{
    /// <summary>
    /// Ctrl+Alt+K ("K" de Kumquat): libre en Windows y en las apps comunes. Con Alt+Ctrl no
    /// choca con atajos de texto y no se dispara al escribir.
    /// </summary>
    public static Hotkey Default { get; } = new(Win32.MOD_CONTROL | Win32.MOD_ALT, 0x4B, "Ctrl+Alt+K");
}

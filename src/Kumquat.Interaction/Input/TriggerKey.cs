// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Interaction.Input;

/// <summary>
/// Gesto que abre la rueda durante un arrastre. Shift es el default (como Tangerine);
/// la tecla no se intercepta: Explorer la sigue viendo, y Kumquat como destino responde
/// COPY de todos modos.
/// </summary>
public enum TriggerKey
{
    /// <summary>Cualquier Shift.</summary>
    Shift,

    /// <summary>Cualquier Ctrl.</summary>
    Control,

    /// <summary>Bloq Mayús.</summary>
    CapsLock,

    /// <summary>Clic derecho mientras se arrastra con el izquierdo.</summary>
    RightClick,
}

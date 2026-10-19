// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Interaction.Input;

/// <summary>Qué hacer tras procesar un evento de entrada.</summary>
public enum GestureSignal
{
    /// <summary>Nada.</summary>
    None,

    /// <summary>Se pidió la rueda durante un arrastre.</summary>
    Trigger,

    /// <summary>El usuario soltó el botón: termina el gesto.</summary>
    DragEnded,

    /// <summary>Escape con la rueda abierta.</summary>
    Cancel,

    /// <summary>Un arrastre candidato superó el umbral (solo diagnóstico).</summary>
    DragStarted,
}

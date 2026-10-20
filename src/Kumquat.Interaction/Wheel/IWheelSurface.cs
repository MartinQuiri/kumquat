// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Interaction.Native;

namespace Kumquat.Interaction.Wheel;

/// <summary>Lo que <see cref="WheelSession"/> necesita de la ventana de la rueda (y los tests imitan).</summary>
internal interface IWheelSurface
{
    /// <summary>Indica si la rueda está bajo el cursor (y no estacionada).</summary>
    bool IsShown { get; }

    /// <summary>Centro de la rueda en píxeles de pantalla.</summary>
    POINT Center { get; }

    /// <summary>Geometría actual para un estado.</summary>
    WheelLayout LayoutFor(WheelVisualState state);

    /// <summary>Muestra la rueda centrada en un punto.</summary>
    void ShowAt(POINT screenPoint, WheelVisualState state);

    /// <summary>Redibuja en la posición actual.</summary>
    void Render(WheelVisualState state);

    /// <summary>Saca la rueda de la vista.</summary>
    void Park();

    /// <summary>Inicia (o reinicia) un timer; al vencer se llama a <see cref="WheelSession.OnTimer"/>.</summary>
    void StartTimer(nuint id, uint milliseconds);

    /// <summary>Detiene un timer.</summary>
    void StopTimer(nuint id);

    /// <summary>Hace que el lector de pantalla diga un texto (UI Automation); no hace nada si no hay ninguno.</summary>
    void Announce(string text);
}

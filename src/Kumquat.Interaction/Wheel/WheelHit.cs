// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;

namespace Kumquat.Interaction.Wheel;

/// <summary>Resultado del hit-test.</summary>
/// <param name="Zone">Zona.</param>
/// <param name="SectorIndex">Índice del sector si <see cref="Zone"/> es <see cref="WheelZone.Sector"/>; si no, -1.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct WheelHit(WheelZone Zone, int SectorIndex)
{
    /// <summary>Fuera del disco.</summary>
    public static WheelHit Outside => new(WheelZone.Outside, -1);

    /// <summary>En el centro.</summary>
    public static WheelHit Center => new(WheelZone.Center, -1);
}

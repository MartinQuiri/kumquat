// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;

namespace Kumquat.Interaction.Wheel;

/// <summary>Resultado del hit-test.</summary>
/// <param name="Zone">Zona.</param>
/// <param name="Ring">Índice del anillo (0 = interior) si es un sector; si no, -1.</param>
/// <param name="Sector">Índice del sector dentro del anillo; si no, -1.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct WheelHit(WheelZone Zone, int Ring, int Sector)
{
    /// <summary>Fuera del disco.</summary>
    public static WheelHit Outside => new(WheelZone.Outside, -1, -1);

    /// <summary>En el centro.</summary>
    public static WheelHit Center => new(WheelZone.Center, -1, -1);

    /// <summary>Un sector concreto.</summary>
    public static WheelHit At(int ring, int sector) => new(WheelZone.Sector, ring, sector);
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Interaction.Wheel;

/// <summary>Zona de la rueda bajo un punto.</summary>
public enum WheelZone
{
    /// <summary>Fuera del disco: la rueda no acepta el drop.</summary>
    Outside,

    /// <summary>Centro: soltar aquí cancela.</summary>
    Center,

    /// <summary>Un sector.</summary>
    Sector,
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Interaction.Wheel;

/// <summary>Lo que la rueda muestra en un momento dado.</summary>
/// <param name="Items">Sectores.</param>
/// <param name="Highlighted">Sector resaltado, o -1.</param>
/// <param name="Message">Texto central opcional (p. ej. "Sin conversiones").</param>
internal sealed record WheelVisualState(IReadOnlyList<WheelItem> Items, int Highlighted, string? Message)
{
    /// <summary>Rueda recién aparecida, esperando saber qué se arrastra.</summary>
    public static WheelVisualState Waiting { get; } = new([], -1, null);
}

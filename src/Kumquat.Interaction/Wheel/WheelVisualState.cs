// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Interaction.Wheel;

/// <summary>Lo que la rueda muestra en un momento dado.</summary>
/// <param name="Rings">Anillos no vacíos, de adentro hacia afuera.</param>
/// <param name="Highlight">Sector resaltado (o <see cref="WheelHit.Center"/> si ninguno).</param>
/// <param name="Message">Texto opcional (p. ej. "Sin conversiones").</param>
internal sealed record WheelVisualState(IReadOnlyList<WheelRing> Rings, WheelHit Highlight, string? Message)
{
    /// <summary>Rueda recién aparecida, esperando saber qué se arrastra.</summary>
    public static WheelVisualState Waiting { get; } = new([], WheelHit.Center, null);

    /// <summary>Etiqueta bajo la rueda con el formato y el tamaño de lo arrastrado (p. ej. "PNG · 2,4 MB").</summary>
    public string? Caption { get; init; }

    /// <summary>Indica si hay un sector resaltado.</summary>
    public bool HasHighlight => Highlight.Zone == WheelZone.Sector;

    /// <summary>Sectores por anillo (para la geometría).</summary>
    public IReadOnlyList<int> SectorCounts => [.. Rings.Select(r => r.Items.Count)];

    /// <summary>Sector bajo un hit, o <c>null</c>.</summary>
    public WheelItem? ItemAt(WheelHit hit)
        => hit.Zone == WheelZone.Sector && hit.Ring < Rings.Count && hit.Sector < Rings[hit.Ring].Items.Count
            ? Rings[hit.Ring].Items[hit.Sector]
            : null;
}

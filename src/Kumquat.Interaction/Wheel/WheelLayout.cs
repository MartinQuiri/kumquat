// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;

namespace Kumquat.Interaction.Wheel;

/// <summary>
/// Geometría de la rueda en píxeles físicos. El sector 0 está centrado arriba (norte)
/// y los demás siguen en sentido horario: así los destinos más frecuentes caen en los
/// ejes, que son los más rápidos y precisos de alcanzar (Kurtenbach &amp; Buxton, 1993).
/// </summary>
/// <param name="SectorCount">Cantidad de sectores (1..8 recomendado).</param>
/// <param name="InnerRadius">Radio de la zona central de cancelar.</param>
/// <param name="OuterRadius">Radio exterior del disco.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct WheelLayout(int SectorCount, double InnerRadius, double OuterRadius)
{
    /// <summary>Máximo de sectores por anillo: más allá la tasa de error sube fuerte.</summary>
    public const int MaxSectors = 8;

    /// <summary>Ancho angular de cada sector, en radianes.</summary>
    public double SectorAngle => SectorCount > 0 ? 2 * Math.PI / SectorCount : 0;

    /// <summary>Clasifica un punto relativo al centro (x a la derecha, y hacia abajo).</summary>
    public WheelHit HitTest(double dx, double dy)
    {
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        if (distance > OuterRadius || double.IsNaN(distance))
        {
            return WheelHit.Outside;
        }

        if (distance < InnerRadius || SectorCount <= 0)
        {
            return WheelHit.Center;
        }

        // Ángulo medido desde el norte, en sentido horario, desplazado medio sector
        // para que el sector 0 quede centrado arriba.
        var angle = Math.Atan2(dx, -dy);
        var shifted = angle + (SectorAngle / 2);
        var normalized = ((shifted % (2 * Math.PI)) + (2 * Math.PI)) % (2 * Math.PI);
        var index = (int)(normalized / SectorAngle);
        return new WheelHit(WheelZone.Sector, Math.Min(index, SectorCount - 1));
    }

    /// <summary>Ángulo central del sector, en radianes desde el norte en sentido horario.</summary>
    public double CenterAngle(int sectorIndex) => sectorIndex * SectorAngle;

    /// <summary>Punto medio del sector (para la etiqueta), relativo al centro.</summary>
    public (double X, double Y) LabelPoint(int sectorIndex)
    {
        var angle = CenterAngle(sectorIndex);
        var r = (InnerRadius + OuterRadius) / 2;
        return (Math.Sin(angle) * r, -Math.Cos(angle) * r);
    }
}

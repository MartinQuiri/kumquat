// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Interaction.Wheel;

/// <summary>
/// Geometría de una rueda de anillos concéntricos, en píxeles físicos. En cada anillo el
/// sector 0 queda centrado arriba (norte) y el resto sigue en sentido horario: los destinos
/// más frecuentes caen en los ejes, que son los más rápidos y precisos de alcanzar
/// (Kurtenbach y Buxton, 1993).
/// </summary>
public sealed class WheelLayout
{
    /// <summary>Máximo de sectores por anillo: más allá la tasa de error sube fuerte.</summary>
    public const int MaxSectors = 8;

    private readonly int[] _sectorCounts;

    /// <summary>Crea la geometría.</summary>
    /// <param name="sectorCounts">Sectores por anillo, de adentro hacia afuera (sin anillos vacíos).</param>
    /// <param name="centerRadius">Radio de la zona central de cancelar.</param>
    /// <param name="ringThickness">Grosor de cada anillo.</param>
    public WheelLayout(IReadOnlyList<int> sectorCounts, double centerRadius, double ringThickness)
    {
        ArgumentNullException.ThrowIfNull(sectorCounts);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ringThickness);
        _sectorCounts = [.. sectorCounts];
        CenterRadius = centerRadius;
        RingThickness = ringThickness;
    }

    /// <summary>Cantidad de anillos.</summary>
    public int RingCount => _sectorCounts.Length;

    /// <summary>Radio de la zona central.</summary>
    public double CenterRadius { get; }

    /// <summary>Grosor de cada anillo.</summary>
    public double RingThickness { get; }

    /// <summary>Radio exterior del último anillo (o del centro si no hay anillos).</summary>
    public double OuterRadius => RingOuter(RingCount - 1);

    /// <summary>Sectores del anillo.</summary>
    public int SectorCount(int ring) => _sectorCounts[ring];

    /// <summary>Radio interior del anillo.</summary>
    public double RingInner(int ring) => CenterRadius + (ring * RingThickness);

    /// <summary>Radio exterior del anillo (con <c>ring = -1</c>, el del centro).</summary>
    public double RingOuter(int ring) => CenterRadius + ((ring + 1) * RingThickness);

    /// <summary>Ancho angular de cada sector del anillo, en radianes.</summary>
    public double SectorAngle(int ring) => _sectorCounts[ring] > 0 ? 2 * Math.PI / _sectorCounts[ring] : 0;

    /// <summary>Ángulo central del sector, en radianes desde el norte en sentido horario.</summary>
    public double CenterAngle(int ring, int sector) => sector * SectorAngle(ring);

    /// <summary>Clasifica un punto relativo al centro (x a la derecha, y hacia abajo).</summary>
    public WheelHit HitTest(double dx, double dy)
    {
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        if (double.IsNaN(distance) || distance > OuterRadius)
        {
            return WheelHit.Outside;
        }

        if (distance < CenterRadius || RingCount == 0)
        {
            return WheelHit.Center;
        }

        var ring = Math.Min((int)((distance - CenterRadius) / RingThickness), RingCount - 1);
        if (_sectorCounts[ring] == 0)
        {
            return WheelHit.Center;
        }

        var sectorAngle = SectorAngle(ring);

        // Ángulo desde el norte, horario, desplazado medio sector para centrar el sector 0 arriba.
        var shifted = Math.Atan2(dx, -dy) + (sectorAngle / 2);
        var normalized = ((shifted % (2 * Math.PI)) + (2 * Math.PI)) % (2 * Math.PI);
        var sector = Math.Min((int)(normalized / sectorAngle), _sectorCounts[ring] - 1);
        return WheelHit.At(ring, sector);
    }

    /// <summary>Punto medio del sector (para la etiqueta), relativo al centro.</summary>
    public (double X, double Y) LabelPoint(int ring, int sector)
    {
        var angle = CenterAngle(ring, sector);
        var r = (RingInner(ring) + RingOuter(ring)) / 2;
        return (Math.Sin(angle) * r, -Math.Cos(angle) * r);
    }
}

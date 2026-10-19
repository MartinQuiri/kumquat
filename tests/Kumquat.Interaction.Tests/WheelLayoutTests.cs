// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using CsCheck;
using Kumquat.Interaction.Wheel;

namespace Kumquat.Interaction.Tests;

public sealed class WheelLayoutTests
{
    // Centro 30, anillos de 50: interior 30-80, medio 80-130, exterior 130-180.
    private static readonly WheelLayout ThreeRings = new([8, 4, 3], centerRadius: 30, ringThickness: 50);

    [Theory]
    [InlineData(0, -55, 0, 0)]    // anillo interior, norte
    [InlineData(55, 0, 0, 2)]     // anillo interior, este
    [InlineData(-40, -40, 0, 7)]  // anillo interior, noroeste
    [InlineData(0, -105, 1, 0)]   // anillo medio, norte
    [InlineData(105, 0, 1, 1)]    // anillo medio (4 sectores), este
    [InlineData(0, 105, 1, 2)]    // anillo medio, sur
    [InlineData(0, -155, 2, 0)]   // anillo exterior (3 sectores), norte
    [InlineData(140, 60, 2, 1)]   // anillo exterior, sureste
    public void HitTest_AnilloYSector(double dx, double dy, int ring, int sector)
    {
        ThreeRings.HitTest(dx, dy).ShouldBe(WheelHit.At(ring, sector));
    }

    [Fact]
    public void HitTest_CentroYFuera()
    {
        ThreeRings.HitTest(0, 0).ShouldBe(WheelHit.Center);
        ThreeRings.HitTest(15, 15).ShouldBe(WheelHit.Center);
        ThreeRings.HitTest(200, 0).ShouldBe(WheelHit.Outside);
        ThreeRings.HitTest(double.NaN, 0).ShouldBe(WheelHit.Outside);
    }

    [Fact]
    public void SinAnillos_TodoLoInternoEsCentro()
    {
        var empty = new WheelLayout([], 30, 50);

        empty.HitTest(0, 0).ShouldBe(WheelHit.Center);
        empty.HitTest(0, -60).ShouldBe(WheelHit.Outside);
    }

    [Fact]
    public void Radios()
    {
        ThreeRings.RingInner(0).ShouldBe(30);
        ThreeRings.RingOuter(0).ShouldBe(80);
        ThreeRings.RingInner(2).ShouldBe(130);
        ThreeRings.OuterRadius.ShouldBe(180);
    }

    [Fact]
    public void Propiedad_LaEtiquetaDeCadaSectorCaeEnSuPropioSector()
    {
        Gen.Int[1, 8].Array[1, 3].Sample(counts =>
        {
            var layout = new WheelLayout(counts, 30, 50);
            for (var ring = 0; ring < counts.Length; ring++)
            {
                for (var sector = 0; sector < counts[ring]; sector++)
                {
                    var (x, y) = layout.LabelPoint(ring, sector);
                    layout.HitTest(x, y).ShouldBe(WheelHit.At(ring, sector));
                }
            }
        });
    }

    [Fact]
    public void Propiedad_TodoPuntoDeLosAnillosCaeEnUnSectorValido()
    {
        Gen.Select(Gen.Int[1, 8].Array[1, 3], Gen.Double[0, 2 * Math.PI], Gen.Double[0.001, 0.999]).Sample((counts, angle, t) =>
        {
            var layout = new WheelLayout(counts, 30, 50);
            var radius = 30 + (t * 50 * counts.Length);
            var hit = layout.HitTest(Math.Sin(angle) * radius, -Math.Cos(angle) * radius);

            hit.Zone.ShouldBe(WheelZone.Sector);
            hit.Ring.ShouldBeInRange(0, counts.Length - 1);
            hit.Sector.ShouldBeInRange(0, counts[hit.Ring] - 1);
        });
    }
}

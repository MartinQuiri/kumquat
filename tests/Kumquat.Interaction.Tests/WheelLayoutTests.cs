// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using CsCheck;
using Kumquat.Interaction.Wheel;

namespace Kumquat.Interaction.Tests;

public sealed class WheelLayoutTests
{
    private static readonly WheelLayout Eight = new(8, 38, 140);

    [Theory]
    [InlineData(0, -90, 0)]   // norte
    [InlineData(64, -64, 1)]  // noreste
    [InlineData(90, 0, 2)]    // este
    [InlineData(0, 90, 4)]    // sur
    [InlineData(-90, 0, 6)]   // oeste
    [InlineData(-64, -64, 7)] // noroeste
    public void HitTest_SectoresEnSentidoHorarioDesdeElNorte(double dx, double dy, int expected)
    {
        Eight.HitTest(dx, dy).ShouldBe(new WheelHit(WheelZone.Sector, expected));
    }

    [Fact]
    public void HitTest_CentroYFuera()
    {
        Eight.HitTest(0, 0).ShouldBe(WheelHit.Center);
        Eight.HitTest(20, 20).ShouldBe(WheelHit.Center);
        Eight.HitTest(200, 0).ShouldBe(WheelHit.Outside);
        Eight.HitTest(double.NaN, 0).ShouldBe(WheelHit.Outside);
    }

    [Fact]
    public void HitTest_SinSectores_TodoElDiscoEsCentro()
    {
        new WheelLayout(0, 38, 140).HitTest(0, -100).ShouldBe(WheelHit.Center);
    }

    [Fact]
    public void Propiedad_LaEtiquetaDeCadaSectorCaeDentroDeSuPropioSector()
    {
        Gen.Int[1, 12].Sample(count =>
        {
            var layout = new WheelLayout(count, 38, 140);
            for (var i = 0; i < count; i++)
            {
                var (x, y) = layout.LabelPoint(i);
                layout.HitTest(x, y).ShouldBe(new WheelHit(WheelZone.Sector, i));
            }
        });
    }

    [Fact]
    public void Propiedad_TodoPuntoDelAnilloCaeEnUnSectorValido()
    {
        Gen.Select(Gen.Int[1, 12], Gen.Double[0, 2 * Math.PI], Gen.Double[38.001, 140]).Sample((count, angle, radius) =>
        {
            var hit = new WheelLayout(count, 38, 140).HitTest(Math.Sin(angle) * radius, -Math.Cos(angle) * radius);
            hit.Zone.ShouldBe(WheelZone.Sector);
            hit.SectorIndex.ShouldBeInRange(0, count - 1);
        });
    }
}

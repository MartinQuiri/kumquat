// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Design;

namespace Kumquat.Interaction.Tests;

public sealed class SvgPathTests
{
    [Fact]
    public void Parse_TodosLosIconos_SinErrorYDentroDelViewBox()
    {
        foreach (var name in Icons.Names)
        {
            var figures = Icons.Figures(name);
            figures.ShouldNotBeEmpty(name);
            foreach (var point in figures.SelectMany(f => f.Segments.Select(s => s.End).Prepend(f.Start)))
            {
                point.X.ShouldBeInRange(-1, Icons.ViewBox + 1, name);
                point.Y.ShouldBeInRange(-1, Icons.ViewBox + 1, name);
            }
        }
    }

    [Fact]
    public void Parse_ComandosRelativosYNumerosPegados()
    {
        // "-.5-.5" son dos números; las coordenadas extra tras "m" son "l" implícitos.
        var figure = SvgPath.Parse("m10,10 5,0 h-.5-.5 v2z").ShouldHaveSingleItem();
        figure.Start.ShouldBe(new PathPoint(10, 10));
        figure.Closed.ShouldBeTrue();
        figure.Segments.Select(s => s.End).ShouldBe([new PathPoint(15, 10), new PathPoint(14.5, 10), new PathPoint(14, 10), new PathPoint(14, 12)]);
    }

    [Fact]
    public void Parse_CuartoDeCirculo_PasaPorElPuntoMedioDelArco()
    {
        // Arco de radio 10 desde (10,0) hasta (0,10), centro (0,0), horario en coordenadas de pantalla.
        var segment = SvgPath.Parse("M10,0 A10,10,0,0,1,0,10").ShouldHaveSingleItem().Segments.ShouldHaveSingleItem();
        segment.IsCubic.ShouldBeTrue();
        segment.End.X.ShouldBe(0, 1e-9);
        segment.End.Y.ShouldBe(10, 1e-9);

        // Punto de la Bézier en t = 0,5: debe caer sobre el círculo (error de la aproximación < 0,03 %).
        var mid = Bezier(new PathPoint(10, 0), segment, 0.5);
        Math.Sqrt((mid.X * mid.X) + (mid.Y * mid.Y)).ShouldBe(10, 0.003);
        mid.X.ShouldBeGreaterThan(0);
        mid.Y.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Parse_ArcoGrande_SeDivideEnTramosDeHasta90Grados()
    {
        // Semicírculo: dos tramos.
        var segments = SvgPath.Parse("M0,0 a5,5,0,1,0,10,0").ShouldHaveSingleItem().Segments;
        segments.Count.ShouldBe(2);
        segments[^1].End.ShouldBe(new PathPoint(10, 0));
    }

    [Fact]
    public void Parse_RadioDemasiadoChico_SeAgranda()
    {
        // Radio 1 no alcanza para unir puntos a distancia 10: se escala a 5 (SVG F.6.6).
        var segments = SvgPath.Parse("M0,0 A1,1,0,0,1,10,0").ShouldHaveSingleItem().Segments;
        var mid = Bezier(new PathPoint(0, 0), segments[0], 1);
        Math.Sqrt(((mid.X - 5) * (mid.X - 5)) + (mid.Y * mid.Y)).ShouldBe(5, 1e-6);
    }

    [Fact]
    public void Parse_CuadraticaSuave_ReflejaElControl()
    {
        var segments = SvgPath.Parse("M0,0 Q5,10 10,0 T20,0").ShouldHaveSingleItem().Segments;
        segments.Count.ShouldBe(2);

        // El control reflejado es (15,-10): la curva baja en el segundo tramo.
        Bezier(new PathPoint(10, 0), segments[1], 0.5).Y.ShouldBe(-5, 1e-9);
    }

    [Theory]
    [InlineData("10,10")]
    [InlineData("M10")]
    [InlineData("M0,0 X5,5")]
    [InlineData("M0,0 A5,5,0,2,0,10,0")]
    public void Parse_Invalido_Lanza(string data) => Should.Throw<FormatException>(() => SvgPath.Parse(data));

    private static PathPoint Bezier(PathPoint p0, PathSegment s, double t)
    {
        var u = 1 - t;
        double At(double a, double b, double c, double d) => (u * u * u * a) + (3 * u * u * t * b) + (3 * u * t * t * c) + (t * t * t * d);
        return new PathPoint(At(p0.X, s.Control1.X, s.Control2.X, s.End.X), At(p0.Y, s.Control1.Y, s.Control2.Y, s.End.Y));
    }
}

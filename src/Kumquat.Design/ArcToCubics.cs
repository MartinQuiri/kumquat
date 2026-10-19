// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Design;

/// <summary>
/// Convierte un arco elíptico de SVG a Béziers cúbicas (W3C SVG 1.1, apéndice F.6: paso de
/// parametrización por extremos a parametrización por centro, y un tramo cúbico cada ≤ 90°).
/// </summary>
internal static class ArcToCubics
{
    public static IEnumerable<PathSegment> Convert(PathPoint from, double rx, double ry, double rotationDegrees, bool largeArc, bool sweep, PathPoint to)
    {
        rx = Math.Abs(rx);
        ry = Math.Abs(ry);
        if (rx == 0 || ry == 0 || (from.X == to.X && from.Y == to.Y))
        {
            // Radio nulo: el arco es una recta (F.6.2). Extremos iguales: no se dibuja nada.
            if (from.X != to.X || from.Y != to.Y)
            {
                yield return PathSegment.Line(to);
            }

            yield break;
        }

        var arc = CenterParameterization(from, rx, ry, rotationDegrees * Math.PI / 180, largeArc, sweep, to);
        foreach (var segment in Cubics(arc, to))
        {
            yield return segment;
        }
    }

    private static IEnumerable<PathSegment> Cubics(CenterArc arc, PathPoint to)
    {
        var (cx, cy, rx, ry, cos, sin, theta1, delta) = arc;
        var pieces = Math.Max(1, (int)Math.Ceiling(Math.Abs(delta) / (Math.PI / 2) - 1e-9));
        var step = delta / pieces;

        // Longitud de las tangentes de una cúbica que aproxima un arco de ángulo "step".
        var k = 4.0 / 3 * Math.Tan(step / 4);
        var theta = theta1;
        for (var i = 0; i < pieces; i++)
        {
            var a = theta;
            var b = theta + step;
            var p0 = (X: Math.Cos(a), Y: Math.Sin(a));
            var p3 = (X: Math.Cos(b), Y: Math.Sin(b));
            var c1 = Map(p0.X - (k * p0.Y), p0.Y + (k * p0.X));
            var c2 = Map(p3.X + (k * p3.Y), p3.Y - (k * p3.X));
            var end = i == pieces - 1 ? to : Map(p3.X, p3.Y);
            yield return PathSegment.Cubic(c1, c2, end);
            theta = b;
        }

        PathPoint Map(double ux, double uy)
            => new((cos * rx * ux) - (sin * ry * uy) + cx, (sin * rx * ux) + (cos * ry * uy) + cy);
    }

    private static CenterArc CenterParameterization(PathPoint from, double rx, double ry, double phi, bool largeArc, bool sweep, PathPoint to)
    {
        var cos = Math.Cos(phi);
        var sin = Math.Sin(phi);

        // F.6.5.1: punto medio en el sistema rotado.
        var dx = (from.X - to.X) / 2;
        var dy = (from.Y - to.Y) / 2;
        var x1 = (cos * dx) + (sin * dy);
        var y1 = (-sin * dx) + (cos * dy);

        // F.6.6: agrandar los radios si no alcanzan.
        var lambda = ((x1 * x1) / (rx * rx)) + ((y1 * y1) / (ry * ry));
        if (lambda > 1)
        {
            var root = Math.Sqrt(lambda);
            rx *= root;
            ry *= root;
        }

        // F.6.5.2: centro en el sistema rotado.
        var numerator = (rx * rx * ry * ry) - (rx * rx * y1 * y1) - (ry * ry * x1 * x1);
        var denominator = (rx * rx * y1 * y1) + (ry * ry * x1 * x1);
        var factor = Math.Sqrt(Math.Max(0, numerator / denominator)) * (largeArc == sweep ? -1 : 1);
        var cx1 = factor * rx * y1 / ry;
        var cy1 = factor * -ry * x1 / rx;

        // F.6.5.3: centro real.
        var cx = (cos * cx1) - (sin * cy1) + ((from.X + to.X) / 2);
        var cy = (sin * cx1) + (cos * cy1) + ((from.Y + to.Y) / 2);

        // F.6.5.5-6: ángulo inicial y barrido.
        var theta1 = Angle(1, 0, (x1 - cx1) / rx, (y1 - cy1) / ry);
        var delta = Angle((x1 - cx1) / rx, (y1 - cy1) / ry, (-x1 - cx1) / rx, (-y1 - cy1) / ry);
        if (!sweep && delta > 0)
        {
            delta -= 2 * Math.PI;
        }
        else if (sweep && delta < 0)
        {
            delta += 2 * Math.PI;
        }

        return new CenterArc(cx, cy, rx, ry, cos, sin, theta1, delta);
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    private readonly record struct CenterArc(double Cx, double Cy, double Rx, double Ry, double Cos, double Sin, double Theta1, double Delta);

    private static double Angle(double ux, double uy, double vx, double vy)
        => Math.Atan2((ux * vy) - (uy * vx), (ux * vx) + (uy * vy));
}

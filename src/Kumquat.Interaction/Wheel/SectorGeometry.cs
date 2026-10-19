// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Drawing.Drawing2D;

namespace Kumquat.Interaction.Wheel;

/// <summary>
/// Sector de anillo con esquinas redondeadas y separación de ancho parejo con sus vecinos
/// (los bordes son paralelos al radio, desplazados media separación). Cada esquina es un arco
/// de radio <c>corner</c> tangente al borde recto y a la circunferencia.
/// </summary>
internal static class SectorGeometry
{
    /// <summary>Construye el trazado.</summary>
    /// <param name="c">Centro de la rueda (x = y).</param>
    /// <param name="inner">Radio interior del sector.</param>
    /// <param name="outer">Radio exterior del sector.</param>
    /// <param name="startDegrees">Ángulo del borde inicial (GDI+: desde el este, horario).</param>
    /// <param name="sweepDegrees">Ancho angular, de borde a borde, antes de la separación.</param>
    /// <param name="gap">Separación entre sectores vecinos.</param>
    /// <param name="corner">Radio de las esquinas.</param>
    public static GraphicsPath Rounded(float c, float inner, float outer, float startDegrees, float sweepDegrees, float gap, float corner)
    {
        var path = new GraphicsPath();
        try
        {
            if (sweepDegrees >= 360)
            {
                path.AddEllipse(c - outer, c - outer, 2 * outer, 2 * outer);
                path.AddEllipse(c - inner, c - inner, 2 * inner, 2 * inner);
                return path;
            }

            var a0 = startDegrees * Math.PI / 180;
            var a1 = (startDegrees + sweepDegrees) * Math.PI / 180;
            var h = corner + (gap / 2);
            var r = corner;

            // Centros de las esquinas: a distancia "h" del radio del borde y a "r" de la circunferencia.
            var dOuter = outer - r;
            var dInner = inner + r;
            var mid = (a0 + a1) / 2;
            var outerStart = Math.Min(a0 + Math.Asin(Math.Min(1, h / dOuter)), mid);
            var outerEnd = Math.Max(a1 - Math.Asin(Math.Min(1, h / dOuter)), mid);
            var innerStart = Math.Min(a0 + Math.Asin(Math.Min(1, h / dInner)), mid);
            var innerEnd = Math.Max(a1 - Math.Asin(Math.Min(1, h / dInner)), mid);

            // Recorrido horario: esquina exterior inicial, arco exterior, esquina exterior final,
            // borde final hacia adentro, esquina interior final, arco interior (al revés), esquina interior inicial.
            Corner(path, c, dOuter, outerStart, r, Deg(a0) - 90, Deg(outerStart));
            path.AddArc(c - outer, c - outer, 2 * outer, 2 * outer, Deg(outerStart), Deg(outerEnd - outerStart));
            Corner(path, c, dOuter, outerEnd, r, Deg(outerEnd), Deg(a1) + 90);
            Corner(path, c, dInner, innerEnd, r, Deg(a1) + 90, Deg(innerEnd) + 180);
            path.AddArc(c - inner, c - inner, 2 * inner, 2 * inner, Deg(innerEnd), -Deg(innerEnd - innerStart));
            Corner(path, c, dInner, innerStart, r, Deg(innerStart) + 180, Deg(a0) + 270);
            path.CloseFigure();
            return path;
        }
        catch
        {
            path.Dispose();
            throw;
        }
    }

    private static void Corner(GraphicsPath path, float c, double distance, double angle, float r, double fromDegrees, double toDegrees)
    {
        var x = c + (float)(Math.Cos(angle) * distance);
        var y = c + (float)(Math.Sin(angle) * distance);
        var sweep = toDegrees - fromDegrees;
        sweep = ((sweep % 360) + 360) % 360;
        path.AddArc(x - r, y - r, 2 * r, 2 * r, (float)fromDegrees, (float)sweep);
    }

    private static float Deg(double radians) => (float)(radians * 180 / Math.PI);
}

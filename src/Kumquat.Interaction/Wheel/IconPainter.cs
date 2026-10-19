// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Drawing;
using System.Drawing.Drawing2D;
using Kumquat.Design;

namespace Kumquat.Interaction.Wheel;

/// <summary>Dibuja los íconos vectoriales de <see cref="Icons"/> con GDI+.</summary>
internal static class IconPainter
{
    /// <summary>Trazado del ícono escalado al cuadrado indicado.</summary>
    public static GraphicsPath PathFor(string icon, RectangleF box)
    {
        var scale = Math.Min(box.Width, box.Height) / Icons.ViewBox;
        var left = box.X + ((box.Width - (Icons.ViewBox * scale)) / 2);
        var top = box.Y + ((box.Height - (Icons.ViewBox * scale)) / 2);
        PointF Map(PathPoint p) => new((float)(left + (p.X * scale)), (float)(top + (p.Y * scale)));

        // Phosphor usa la regla no-cero de SVG (los huecos van en sentido contrario).
        var path = new GraphicsPath(FillMode.Winding);
        foreach (var figure in Icons.Figures(icon))
        {
            path.StartFigure();
            var current = Map(figure.Start);
            foreach (var segment in figure.Segments)
            {
                var end = Map(segment.End);
                if (segment.IsCubic)
                {
                    path.AddBezier(current, Map(segment.Control1), Map(segment.Control2), end);
                }
                else
                {
                    path.AddLine(current, end);
                }

                current = end;
            }

            if (figure.Closed)
            {
                path.CloseFigure();
            }
        }

        return path;
    }

    /// <summary>Rellena el ícono en el cuadrado indicado.</summary>
    public static void Fill(Graphics g, string icon, RectangleF box, Color color)
    {
        using var path = PathFor(icon, box);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }
}

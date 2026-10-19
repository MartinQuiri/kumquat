// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Media;
using Kumquat.Design;

namespace Kumquat.Dialogs.Look;

/// <summary>Íconos de <see cref="Icons"/> como geometría WPF (congelada y en caché).</summary>
internal static class IconGeometry
{
    private static readonly ConcurrentDictionary<string, Geometry> Cache = new(StringComparer.Ordinal);

    public static Geometry For(string name) => Cache.GetOrAdd(name, Build);

    private static StreamGeometry Build(string name)
    {
        // Misma regla de relleno que SVG (no-cero): los huecos de Phosphor van en sentido contrario.
        var geometry = new StreamGeometry { FillRule = FillRule.Nonzero };
        using (var context = geometry.Open())
        {
            foreach (var figure in Icons.Figures(name))
            {
                context.BeginFigure(P(figure.Start), isFilled: true, isClosed: figure.Closed);
                foreach (var segment in figure.Segments)
                {
                    if (segment.IsCubic)
                    {
                        context.BezierTo(P(segment.Control1), P(segment.Control2), P(segment.End), isStroked: true, isSmoothJoin: true);
                    }
                    else
                    {
                        context.LineTo(P(segment.End), isStroked: true, isSmoothJoin: true);
                    }
                }
            }
        }

        geometry.Freeze();
        return geometry;
    }

    private static Point P(PathPoint point) => new(point.X, point.Y);
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Kumquat.Interaction.Wheel;

/// <summary>Dibuja la rueda con GDI+ sobre un lienzo ARGB premultiplicado.</summary>
internal static class WheelRenderer
{
    /// <summary>Radio exterior en DIP (píxeles a 96 DPI).</summary>
    public const float OuterRadiusDip = 140;

    /// <summary>Radio de la zona central en DIP.</summary>
    public const float InnerRadiusDip = 38;

    /// <summary>Margen alrededor del disco en DIP.</summary>
    public const float MarginDip = 6;

    /// <summary>Lado del lienzo en píxeles para una escala dada.</summary>
    public static int CanvasSize(float scale) => (int)Math.Ceiling(2 * (OuterRadiusDip + MarginDip) * scale);

    /// <summary>Geometría en píxeles para una escala.</summary>
    public static WheelLayout Layout(int sectors, float scale) => new(sectors, InnerRadiusDip * scale, OuterRadiusDip * scale);

    public static void Render(Graphics g, int size, float scale, WheelVisualState state, WheelTheme theme)
    {
        ArgumentNullException.ThrowIfNull(g);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(theme);

        g.CompositingMode = CompositingMode.SourceCopy;
        g.Clear(Color.Transparent);
        g.CompositingMode = CompositingMode.SourceOver;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        var c = size / 2f;
        var layout = Layout(state.Items.Count, scale);
        var outer = (float)layout.OuterRadius;
        var inner = (float)layout.InnerRadius;

        using (var background = new SolidBrush(theme.Background))
        {
            g.FillEllipse(background, c - outer, c - outer, 2 * outer, 2 * outer);
        }

        if (state.Items.Count > 0)
        {
            DrawSectors(g, c, layout, state, theme, scale);
        }

        DrawCenter(g, c, inner, scale, theme, state.Message);

        using var rim = new Pen(Color.FromArgb(160, theme.Accent), 1.5f * scale);
        g.DrawEllipse(rim, c - outer, c - outer, 2 * outer, 2 * outer);
    }

    private static void DrawSectors(Graphics g, float c, WheelLayout layout, WheelVisualState state, WheelTheme theme, float scale)
    {
        var outer = (float)layout.OuterRadius;
        var inner = (float)layout.InnerRadius;
        var sweep = 360f / state.Items.Count;

        if (state.Highlighted >= 0 && state.Highlighted < state.Items.Count)
        {
            // GDI+ mide los ángulos desde el este en sentido horario; la rueda, desde el norte.
            var start = -90f - (sweep / 2) + (state.Highlighted * sweep);
            using var path = new GraphicsPath();
            path.AddArc(c - outer, c - outer, 2 * outer, 2 * outer, start, sweep);
            path.AddArc(c - inner, c - inner, 2 * inner, 2 * inner, start + sweep, -sweep);
            path.CloseFigure();
            using var accent = new SolidBrush(theme.Accent);
            g.FillPath(accent, path);
        }

        if (state.Items.Count > 1)
        {
            using var separator = new Pen(theme.Separator, 1f * scale);
            for (var i = 0; i < state.Items.Count; i++)
            {
                var angle = (layout.CenterAngle(i) - (layout.SectorAngle / 2)) - (Math.PI / 2);
                g.DrawLine(
                    separator,
                    c + (float)(Math.Cos(angle) * inner),
                    c + (float)(Math.Sin(angle) * inner),
                    c + (float)(Math.Cos(angle) * outer),
                    c + (float)(Math.Sin(angle) * outer));
            }
        }

        using var font = new Font("Segoe UI Semibold", 13f * scale, FontStyle.Regular, GraphicsUnit.Pixel);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var text = new SolidBrush(theme.Text);
        using var accentText = new SolidBrush(theme.AccentText);
        for (var i = 0; i < state.Items.Count; i++)
        {
            var (x, y) = layout.LabelPoint(i);
            g.DrawString(state.Items[i].Label, font, i == state.Highlighted ? accentText : text, c + (float)x, c + (float)y, format);
        }
    }

    private static void DrawCenter(Graphics g, float c, float inner, float scale, WheelTheme theme, string? message)
    {
        using var ring = new Pen(theme.Separator, 1f * scale);
        g.DrawEllipse(ring, c - inner, c - inner, 2 * inner, 2 * inner);

        // "×" de cancelar.
        var arm = inner * 0.28f;
        using var cross = new Pen(theme.Muted, 2f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(cross, c - arm, c - arm, c + arm, c + arm);
        g.DrawLine(cross, c - arm, c + arm, c + arm, c - arm);

        if (message is not null)
        {
            using var font = new Font("Segoe UI", 12f * scale, FontStyle.Regular, GraphicsUnit.Pixel);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var brush = new SolidBrush(theme.Text);
            g.DrawString(message, font, brush, new RectangleF(c - (3 * inner), c + inner + (6 * scale), 6 * inner, 40 * scale), format);
        }
    }
}

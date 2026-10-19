// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Kumquat.Interaction.Wheel;

/// <summary>Dibuja la rueda de anillos con GDI+ sobre un lienzo ARGB premultiplicado.</summary>
internal static class WheelRenderer
{
    /// <summary>Radio de la zona central (cancelar), en DIP (píxeles a 96 DPI).</summary>
    public const float CenterRadiusDip = 30;

    /// <summary>Grosor de cada anillo, en DIP.</summary>
    public const float RingThicknessDip = 50;

    /// <summary>Margen alrededor del disco, en DIP.</summary>
    public const float MarginDip = 6;

    /// <summary>Anillos para los que se dimensiona el lienzo (el máximo: convertir, herramientas, acciones).</summary>
    public const int MaxRings = 3;

    /// <summary>Lado del lienzo en píxeles. Fijo para el máximo de anillos: la ventana no cambia de tamaño al llenarse.</summary>
    public static int CanvasSize(float scale) => (int)Math.Ceiling(2 * (CenterRadiusDip + (MaxRings * RingThicknessDip) + MarginDip) * scale);

    /// <summary>Geometría en píxeles para un estado y una escala.</summary>
    public static WheelLayout Layout(WheelVisualState state, float scale)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new WheelLayout(state.SectorCounts, CenterRadiusDip * scale, RingThicknessDip * scale);
    }

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
        var layout = Layout(state, scale);

        // Disco de fondo: hasta el último anillo (o un anillo "fantasma" mientras se espera DragEnter).
        var discRadius = (float)(layout.RingCount > 0 ? layout.OuterRadius : layout.RingOuter(0));
        using (var background = new SolidBrush(theme.Background))
        {
            g.FillEllipse(background, c - discRadius, c - discRadius, 2 * discRadius, 2 * discRadius);
        }

        for (var ring = 0; ring < layout.RingCount; ring++)
        {
            DrawRing(g, c, layout, ring, state, theme, scale);
        }

        DrawCenter(g, c, (float)layout.CenterRadius, scale, theme);

        if (state.Message is not null)
        {
            DrawMessage(g, c, discRadius, scale, theme, state.Message);
        }

        using var rim = new Pen(Color.FromArgb(160, theme.Accent), 1.5f * scale);
        g.DrawEllipse(rim, c - discRadius, c - discRadius, 2 * discRadius, 2 * discRadius);
    }

    private static void DrawRing(Graphics g, float c, WheelLayout layout, int ring, WheelVisualState state, WheelTheme theme, float scale)
    {
        var kind = state.Rings[ring].Kind;
        var items = state.Rings[ring].Items;
        var inner = (float)layout.RingInner(ring);
        var outer = (float)layout.RingOuter(ring);
        var sweep = 360f / items.Count;

        // Tinte suave del color del anillo: identifica convertir / herramientas / acciones de un vistazo.
        using (var tint = new SolidBrush(WheelTheme.TintFor(kind)))
        using (var band = new GraphicsPath())
        {
            band.AddEllipse(c - outer, c - outer, 2 * outer, 2 * outer);
            band.AddEllipse(c - inner, c - inner, 2 * inner, 2 * inner);
            g.FillPath(tint, band);
        }

        if (state.Highlight.Zone == WheelZone.Sector && state.Highlight.Ring == ring)
        {
            // GDI+ mide los ángulos desde el este en sentido horario; la rueda, desde el norte.
            var start = -90f - (sweep / 2) + (state.Highlight.Sector * sweep);
            using var path = new GraphicsPath();
            if (items.Count == 1)
            {
                path.AddEllipse(c - outer, c - outer, 2 * outer, 2 * outer);
                path.AddEllipse(c - inner, c - inner, 2 * inner, 2 * inner);
            }
            else
            {
                path.AddArc(c - outer, c - outer, 2 * outer, 2 * outer, start, sweep);
                path.AddArc(c - inner, c - inner, 2 * inner, 2 * inner, start + sweep, -sweep);
                path.CloseFigure();
            }

            using var accent = new SolidBrush(WheelTheme.AccentFor(kind));
            g.FillPath(accent, path);
        }

        using var separator = new Pen(theme.Separator, 1f * scale);
        g.DrawEllipse(separator, c - outer, c - outer, 2 * outer, 2 * outer);
        if (items.Count > 1)
        {
            for (var i = 0; i < items.Count; i++)
            {
                var angle = layout.CenterAngle(ring, i) - (layout.SectorAngle(ring) / 2) - (Math.PI / 2);
                g.DrawLine(
                    separator,
                    c + (float)(Math.Cos(angle) * inner),
                    c + (float)(Math.Sin(angle) * inner),
                    c + (float)(Math.Cos(angle) * outer),
                    c + (float)(Math.Sin(angle) * outer));
            }
        }

        DrawLabels(g, c, layout, ring, state, theme, scale);
    }

    private static void DrawLabels(Graphics g, float c, WheelLayout layout, int ring, WheelVisualState state, WheelTheme theme, float scale)
    {
        var items = state.Rings[ring].Items;
        var thickness = (float)layout.RingThickness;

        using var font = new Font("Segoe UI Semibold", (ring == 0 ? 12.5f : 11f) * scale, FontStyle.Regular, GraphicsUnit.Pixel);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
        using var text = new SolidBrush(theme.Text);
        using var accentText = new SolidBrush(theme.AccentText);
        for (var i = 0; i < items.Count; i++)
        {
            var (x, y) = layout.LabelPoint(ring, i);
            var highlighted = state.Highlight.Zone == WheelZone.Sector && state.Highlight.Ring == ring && state.Highlight.Sector == i;
            var width = (float)LabelWidth(layout, ring, x, y);
            var height = thickness * 0.92f;
            var box = new RectangleF(c + (float)x - (width / 2), c + (float)y - (height / 2), width, height);
            using var fitted = FitFont(g, font, items[i].Label, width);
            g.DrawString(items[i].Label, fitted, highlighted ? accentText : text, box, format);
        }
    }

    /// <summary>
    /// Ancho horizontal disponible para una etiqueta centrada en (x, y): lo que deja el anillo
    /// sobre esa horizontal (a los costados es solo su grosor; arriba y abajo, mucho más) y la
    /// cuerda del sector (para no invadir a los vecinos). El texto se ajusta en hasta dos líneas.
    /// </summary>
    internal static double LabelWidth(WheelLayout layout, int ring, double x, double y)
    {
        var inner = layout.RingInner(ring);
        var outer = layout.RingOuter(ring);
        var ay = Math.Abs(y);
        var ax = Math.Abs(x);
        var outerX = Math.Sqrt(Math.Max(0, (outer * outer) - (ay * ay)));
        var annulusHalf = ay < inner
            ? Math.Min(ax - Math.Sqrt((inner * inner) - (ay * ay)), outerX - ax)
            : outerX - ax;

        var count = layout.SectorCount(ring);
        var mid = (inner + outer) / 2;
        var chordHalf = count == 1 ? double.MaxValue : mid * Math.Sin(Math.PI / count) * 0.92;

        var half = Math.Max(Math.Min(annulusHalf, chordHalf), layout.RingThickness * 0.45);
        return 2 * half * 0.95;
    }

    /// <summary>Achica la fuente (hasta 60 %) si la palabra más larga no cabe: nunca cortar una palabra a la mitad.</summary>
    private static Font FitFont(Graphics g, Font font, string label, float width)
    {
        var longest = label.Split(' ', StringSplitOptions.RemoveEmptyEntries).Max(word => g.MeasureString(word, font).Width);
        var ratio = longest > width ? Math.Max(0.6f, width / longest) : 1f;
        return new Font(font.FontFamily, font.Size * ratio, font.Style, GraphicsUnit.Pixel);
    }

    private static void DrawCenter(Graphics g, float c, float radius, float scale, WheelTheme theme)
    {
        using var ring = new Pen(theme.Separator, 1f * scale);
        g.DrawEllipse(ring, c - radius, c - radius, 2 * radius, 2 * radius);

        // "×" de cancelar.
        var arm = radius * 0.3f;
        using var cross = new Pen(theme.Muted, 2f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(cross, c - arm, c - arm, c + arm, c + arm);
        g.DrawLine(cross, c - arm, c + arm, c + arm, c - arm);
    }

    private static void DrawMessage(Graphics g, float c, float discRadius, float scale, WheelTheme theme, string message)
    {
        using var font = new Font("Segoe UI", 12f * scale, FontStyle.Regular, GraphicsUnit.Pixel);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var brush = new SolidBrush(theme.Text);
        g.DrawString(message, font, brush, new RectangleF(c - discRadius, c + (discRadius * 0.35f), 2 * discRadius, discRadius * 0.5f), format);
    }
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Kumquat.Design;

namespace Kumquat.Interaction.Wheel;

/// <summary>
/// Dibuja la rueda con GDI+ sobre un lienzo ARGB premultiplicado, en estilo "vidrio": disco
/// translúcido con borde de luz, sectores separados con esquinas redondeadas, íconos de color
/// por familia y el logo en el centro.
/// </summary>
internal static class WheelRenderer
{
    /// <summary>Radio de la zona central (cancelar, con el logo), en DIP (píxeles a 96 DPI).</summary>
    public const float CenterRadiusDip = 34;

    /// <summary>Grosor de cada anillo, en DIP.</summary>
    public const float RingThicknessDip = 56;

    /// <summary>Margen alrededor del disco (para la sombra), en DIP.</summary>
    public const float MarginDip = 14;

    /// <summary>Anillos para los que se dimensiona el lienzo (el máximo: convertir, herramientas, acciones).</summary>
    public const int MaxRings = 3;

    /// <summary>Separación entre sectores y entre anillos, en DIP.</summary>
    private const float GapDip = 3;

    /// <summary>Radio de las esquinas de cada sector, en DIP.</summary>
    private const float CornerDip = 7;

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
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        var c = size / 2f;
        var layout = Layout(state, scale);

        // Disco: hasta el último anillo (o un anillo "fantasma" mientras se espera DragEnter).
        var discRadius = (float)(layout.RingCount > 0 ? layout.OuterRadius : layout.RingOuter(0));
        DrawGlass(g, c, discRadius, scale, theme);

        for (var ring = 0; ring < layout.RingCount; ring++)
        {
            DrawRing(g, c, layout, ring, state, theme, scale);
        }

        DrawCenter(g, c, (float)layout.CenterRadius, scale, theme);

        if (state.Message is not null)
        {
            DrawMessage(g, c, discRadius, scale, theme, state.Message);
        }
    }

    /// <summary>Sombra difusa, cuerpo translúcido en degradado y bordes de luz y de sombra.</summary>
    private static void DrawGlass(Graphics g, float c, float r, float scale, WheelTheme theme)
    {
        var blur = (MarginDip - 2) * scale;
        using (var shadowShape = new GraphicsPath())
        {
            var sr = r + blur;
            shadowShape.AddEllipse(c - sr, c - sr + (3 * scale), 2 * sr, 2 * sr);
            using var shadow = new PathGradientBrush(shadowShape)
            {
                CenterColor = theme.Shadow,
                SurroundColors = [Color.FromArgb(0, theme.Shadow)],
                FocusScales = new PointF((r - blur) / sr, (r - blur) / sr),
            };
            g.FillPath(shadow, shadowShape);
        }

        var disc = new RectangleF(c - r, c - r, 2 * r, 2 * r);
        using (var body = new LinearGradientBrush(disc, theme.GlassTop, theme.GlassBottom, LinearGradientMode.Vertical))
        {
            g.FillEllipse(body, disc);
        }

        // Borde de luz: fuerte arriba, se desvanece hacia abajo (como el canto de un vidrio).
        var rim = RectangleF.Inflate(disc, -0.75f * scale, -0.75f * scale);
        using (var lightBrush = new LinearGradientBrush(disc, theme.RimLight, Color.FromArgb(theme.IsDark ? 10 : 120, theme.RimLight), LinearGradientMode.Vertical))
        using (var light = new Pen(lightBrush, 1.2f * scale))
        {
            g.DrawEllipse(light, rim);
        }

        using var dark = new Pen(theme.RimDark, 1f * scale);
        g.DrawEllipse(dark, RectangleF.Inflate(disc, 0.5f * scale, 0.5f * scale));
    }

    private static void DrawRing(Graphics g, float c, WheelLayout layout, int ring, WheelVisualState state, WheelTheme theme, float scale)
    {
        var items = state.Rings[ring].Items;
        var gap = GapDip * scale;
        var corner = CornerDip * scale;
        var inner = (float)layout.RingInner(ring) + (gap / 2);
        var outer = (float)layout.RingOuter(ring) - (gap / 2);
        var sweep = 360f / items.Count;

        using var rest = new SolidBrush(theme.Sector);
        for (var i = 0; i < items.Count; i++)
        {
            // CA2000 es un falso positivo del análisis de flujo dentro del bucle: el using libera en toda ruta.
#pragma warning disable CA2000
            // GDI+ mide los ángulos desde el este en sentido horario; la rueda, desde el norte.
            var start = -90f - (sweep / 2) + (i * sweep);
            using (var sector = SectorGeometry.Rounded(c, inner, outer, start, sweep, gap, corner))
            {
                DrawSector(g, sector, IsHighlighted(state, ring, i) ? items[i] : null, rest, scale);
            }
#pragma warning restore CA2000
        }

        DrawLabels(g, c, layout, ring, state, theme, scale);
    }

    /// <summary>Rellena un sector: en reposo, vidrio tenue; resaltado, el color de su ítem en degradado.</summary>
    private static void DrawSector(Graphics g, GraphicsPath sector, WheelItem? highlighted, Brush rest, float scale)
    {
        if (highlighted is null)
        {
            g.FillPath(rest, sector);
            return;
        }

        var accent = WheelTheme.AccentFor(highlighted);
        var bounds = sector.GetBounds();
        using var fill = new LinearGradientBrush(RectangleF.Inflate(bounds, 1, 1), Lighten(accent, 0.22), accent, LinearGradientMode.Vertical);
        g.FillPath(fill, sector);
        using var shine = new Pen(Color.FromArgb(70, 255, 255, 255), 1f * scale);
        g.DrawPath(shine, sector);
    }

    private static bool IsHighlighted(WheelVisualState state, int ring, int sector)
        => state.Highlight.Zone == WheelZone.Sector && state.Highlight.Ring == ring && state.Highlight.Sector == sector;

    private static void DrawLabels(Graphics g, float c, WheelLayout layout, int ring, WheelVisualState state, WheelTheme theme, float scale)
    {
        var items = state.Rings[ring].Items;
        var thickness = (float)layout.RingThickness;
        var withIcons = state.Rings[ring].Kind != WheelRingKind.Convert;
        var family = withIcons ? WheelFonts.Medium : WheelFonts.SemiBold;
        using var font = new Font(family, (withIcons ? 10.5f : 13.5f) * scale, FontStyle.Regular, GraphicsUnit.Pixel);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
        for (var i = 0; i < items.Count; i++)
        {
            var (x, y) = layout.LabelPoint(ring, i);
            var width = (float)LabelWidth(layout, ring, x, y);
            var center = new PointF(c + (float)x, c + (float)y);
            // CA2000 es un falso positivo del análisis de flujo dentro del bucle: el using libera en toda ruta.
#pragma warning disable CA2000
            using (var fitted = FitFont(g, font, items[i].Label, width))
            {
                DrawLabel(g, items[i], withIcons, IsHighlighted(state, ring, i), center, width, thickness, fitted, format, theme, scale);
            }
#pragma warning restore CA2000
        }
    }

    private static void DrawLabel(Graphics g, WheelItem item, bool withIcon, bool highlighted, PointF center, float width, float thickness, Font font, StringFormat format, WheelTheme theme, float scale)
    {
        using var text = new SolidBrush(highlighted ? Color.White : theme.Text);
        var icon = withIcon ? Icons.ForItem(item.Id) : null;
        if (icon is null)
        {
            var box = new RectangleF(center.X - (width / 2), center.Y - (thickness * 0.45f), width, thickness * 0.9f);
            g.DrawString(item.Label, font, text, box, format);
            return;
        }

        // Ícono arriba y texto debajo, centrados juntos en el sector.
        var iconSize = 19 * scale;
        var textSize = g.MeasureString(item.Label, font, new SizeF(width, thickness), format);
        var textHeight = Math.Min(textSize.Height, thickness - iconSize - (6 * scale));
        var top = center.Y - ((iconSize + scale + textHeight) / 2);
        var iconColor = highlighted ? Color.White : WheelTheme.AccentFor(item);
        IconPainter.Fill(g, icon, new RectangleF(center.X - (iconSize / 2), top, iconSize, iconSize), iconColor);
        g.DrawString(item.Label, font, text, new RectangleF(center.X - (width / 2), top + iconSize + scale, width, textHeight), format);
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
        var chordHalf = count == 1 ? double.MaxValue : mid * Math.Sin(Math.PI / count) * 0.88;

        var half = Math.Max(Math.Min(annulusHalf, chordHalf), layout.RingThickness * 0.45);
        return 2 * half * 0.92;
    }

    /// <summary>Achica la fuente (hasta 60 %) si la palabra más larga no cabe: nunca cortar una palabra a la mitad.</summary>
    private static Font FitFont(Graphics g, Font font, string label, float width)
    {
        var longest = label.Split(' ', StringSplitOptions.RemoveEmptyEntries).Max(word => g.MeasureString(word, font).Width);
        var ratio = longest > width ? Math.Max(0.6f, width / longest) : 1f;
        var family = font.FontFamily;
        var size = font.Size * ratio;
        return new Font(family, size, FontStyle.Regular, GraphicsUnit.Pixel);
    }

    /// <summary>Centro: botón de vidrio con el logo (soltar aquí cancela).</summary>
    private static void DrawCenter(Graphics g, float c, float radius, float scale, WheelTheme theme)
    {
        var r = radius - (GapDip * scale / 2);
        var disc = new RectangleF(c - r, c - r, 2 * r, 2 * r);
        using (var fill = new SolidBrush(theme.CenterFill))
        {
            g.FillEllipse(fill, disc);
        }

        using (var edge = new LinearGradientBrush(disc, Color.FromArgb(theme.IsDark ? 70 : 255, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), LinearGradientMode.Vertical))
        using (var pen = new Pen(edge, 1f * scale))
        {
            g.DrawEllipse(pen, disc);
        }

        var side = (int)Math.Round(r * 1.5f);
        var logo = WheelLogo.At(side);
        if (logo is not null)
        {
            g.DrawImage(logo, c - (side / 2f), c - (side / 2f) + (1 * scale), side, side);
            return;
        }

        // Sin el archivo del logo: "×" de cancelar.
        var arm = r * 0.3f;
        using var cross = new Pen(theme.Muted, 2f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(cross, c - arm, c - arm, c + arm, c + arm);
        g.DrawLine(cross, c - arm, c + arm, c + arm, c - arm);
    }

    private static void DrawMessage(Graphics g, float c, float discRadius, float scale, WheelTheme theme, string message)
    {
        using var font = new Font(WheelFonts.Medium, 12f * scale, FontStyle.Regular, GraphicsUnit.Pixel);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var brush = new SolidBrush(theme.Muted);
        g.DrawString(message, font, brush, new RectangleF(c - discRadius, c + (discRadius * 0.35f), 2 * discRadius, discRadius * 0.5f), format);
    }

    private static Color Lighten(Color color, double amount)
    {
        return Color.FromArgb(color.A, Mix(color.R), Mix(color.G), Mix(color.B));

        int Mix(int channel) => (int)Math.Round(channel + ((255 - channel) * amount));
    }
}

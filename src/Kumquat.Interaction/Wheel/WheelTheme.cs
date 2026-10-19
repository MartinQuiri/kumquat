// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Drawing;
using Kumquat.Design;

namespace Kumquat.Interaction.Wheel;

/// <summary>
/// Colores de la rueda "de vidrio" (estilo visionOS); siguen el tema claro/oscuro de Windows.
/// Una ventana en capas no puede desenfocar lo que tiene detrás, así que el vidrio se simula
/// con un degradado translúcido, un borde de luz arriba y una sombra suave.
/// </summary>
/// <param name="GlassTop">Vidrio, arriba.</param>
/// <param name="GlassBottom">Vidrio, abajo.</param>
/// <param name="Sector">Relleno de cada sector en reposo.</param>
/// <param name="CenterFill">Relleno del centro.</param>
/// <param name="RimLight">Borde de luz (arriba).</param>
/// <param name="RimDark">Contorno exterior (abajo).</param>
/// <param name="Text">Texto normal.</param>
/// <param name="Muted">Texto secundario.</param>
/// <param name="Shadow">Sombra bajo el disco.</param>
/// <param name="IsDark">Indica si es el tema oscuro.</param>
internal sealed record WheelTheme(
    Color GlassTop,
    Color GlassBottom,
    Color Sector,
    Color CenterFill,
    Color RimLight,
    Color RimDark,
    Color Text,
    Color Muted,
    Color Shadow,
    bool IsDark)
{
    /// <summary>Naranja kumquat.</summary>
    public static readonly Color Kumquat = ToColor(Palette.Kumquat);

    public static WheelTheme Dark { get; } = new(
        GlassTop: Color.FromArgb(232, 66, 66, 74),
        GlassBottom: Color.FromArgb(232, 34, 34, 40),
        Sector: Color.FromArgb(20, 255, 255, 255),
        CenterFill: Color.FromArgb(34, 255, 255, 255),
        RimLight: Color.FromArgb(110, 255, 255, 255),
        RimDark: Color.FromArgb(90, 0, 0, 0),
        Text: Color.FromArgb(255, 245, 245, 247),
        Muted: Color.FromArgb(255, 152, 152, 160),
        Shadow: Color.FromArgb(110, 0, 0, 0),
        IsDark: true);

    public static WheelTheme Light { get; } = new(
        GlassTop: Color.FromArgb(236, 255, 255, 255),
        GlassBottom: Color.FromArgb(236, 238, 238, 243),
        Sector: Color.FromArgb(14, 0, 0, 0),
        CenterFill: Color.FromArgb(200, 255, 255, 255),
        RimLight: Color.FromArgb(255, 255, 255, 255),
        RimDark: Color.FromArgb(46, 0, 0, 0),
        Text: Color.FromArgb(255, 29, 29, 31),
        Muted: Color.FromArgb(255, 110, 110, 115),
        Shadow: Color.FromArgb(70, 0, 0, 0),
        IsDark: false);

    /// <summary>Color de resaltado de cada anillo: distingue de un vistazo convertir, herramientas y acciones.</summary>
    public static Color AccentFor(WheelRingKind ring) => ToColor(ring switch
    {
        WheelRingKind.Tools => Palette.Tools,
        WheelRingKind.Actions => Palette.Actions,
        _ => Palette.Kumquat,
    });

    /// <summary>Color de un sector: el de la familia de la herramienta (PDF rojo, imagen verde...) o el del anillo.</summary>
    public static Color AccentFor(WheelItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Ring == WheelRingKind.Tools ? ToColor(Palette.ForItem(item.Id)) : AccentFor(item.Ring);
    }

    /// <summary>Tema según la preferencia de apps de Windows.</summary>
    public static WheelTheme FromSystem() => SystemTheme.AppsUseDark() ? Dark : Light;

    public static Color ToColor(Rgb rgb, int alpha = 255) => Color.FromArgb(alpha, rgb.R, rgb.G, rgb.B);
}

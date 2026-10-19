// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Drawing;
using Microsoft.Win32;

namespace Kumquat.Interaction.Wheel;

/// <summary>Colores de la rueda; siguen el tema claro/oscuro de Windows.</summary>
/// <param name="Background">Fondo del disco.</param>
/// <param name="Separator">Líneas entre sectores.</param>
/// <param name="Text">Texto normal.</param>
/// <param name="Accent">Sector resaltado.</param>
/// <param name="AccentText">Texto sobre el acento.</param>
/// <param name="Muted">Texto secundario.</param>
internal sealed record WheelTheme(Color Background, Color Separator, Color Text, Color Accent, Color AccentText, Color Muted)
{
    /// <summary>Naranja kumquat.</summary>
    public static readonly Color Kumquat = Color.FromArgb(255, 242, 140, 40);

    public static WheelTheme Dark { get; } = new(
        Color.FromArgb(238, 32, 32, 36), Color.FromArgb(70, 255, 255, 255), Color.FromArgb(255, 240, 240, 240), Kumquat, Color.FromArgb(255, 25, 20, 15), Color.FromArgb(255, 160, 160, 165));

    public static WheelTheme Light { get; } = new(
        Color.FromArgb(242, 250, 250, 250), Color.FromArgb(50, 0, 0, 0), Color.FromArgb(255, 28, 28, 30), Kumquat, Color.FromArgb(255, 25, 20, 15), Color.FromArgb(255, 100, 100, 105));

    /// <summary>Color de resaltado de cada anillo: distingue de un vistazo convertir, herramientas y acciones.</summary>
    public static Color AccentFor(WheelRingKind ring) => ring switch
    {
        WheelRingKind.Tools => Color.FromArgb(255, 38, 166, 154),
        WheelRingKind.Actions => Color.FromArgb(255, 142, 111, 216),
        _ => Kumquat,
    };

    /// <summary>Tinte de fondo del anillo (muy tenue, para distinguirlos sin recargar).</summary>
    public static Color TintFor(WheelRingKind ring) => Color.FromArgb(ring == WheelRingKind.Convert ? 0 : 26, AccentFor(ring));

    /// <summary>Tema según la preferencia de apps de Windows.</summary>
    public static WheelTheme FromSystem()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int light && light != 0 ? Light : Dark;
    }
}

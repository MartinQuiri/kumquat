// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Windows;
using Kumquat.Design;
using Kumquat.Dialogs.Look;

// WPF0001: ThemeMode es experimental en WPF (igual que en Kumquat.Dialogs).
#pragma warning disable WPF0001

namespace Kumquat.Interaction.Tests;

public sealed class DialogLookTests
{
    [Fact]
    public void AltoContraste_ColoresDelSistemaYTemaDeWindows()
    {
        var look = new DialogLook(dark: true, Palette.Pdf, highContrast: true);

        look.ThemeMode.ShouldBe(ThemeMode.System);
        look.AccentBrush.ShouldBe(SystemColors.HighlightBrush);
        look.OnAccent.ShouldBe(SystemColors.HighlightTextBrush);
        look.Text.ShouldBe(SystemColors.WindowTextBrush);
        look.Accent.ShouldBe(SystemColors.HighlightColor, "ni el color de la familia");
        look.WindowBackground(System.Windows.Media.Brushes.Transparent).ShouldBe(SystemColors.WindowBrush);
    }

    [Fact]
    public void Normal_ColorDeLaFamiliaYTemaPropio()
    {
        var look = new DialogLook(dark: false, Palette.Pdf, highContrast: false);

        look.ThemeMode.ShouldBe(ThemeMode.Light);
        look.Accent.ShouldBe(DialogLook.ToColor(Palette.Pdf));
        look.OnAccent.ShouldBe(System.Windows.Media.Brushes.White);
    }
}

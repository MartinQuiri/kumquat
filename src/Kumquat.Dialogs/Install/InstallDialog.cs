// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;
using Kumquat.Core.Localization;
using Kumquat.Design;
using Kumquat.Dialogs.Look;

namespace Kumquat.Dialogs.Install;

/// <summary>
/// Pide permiso para instalar un programa gratuito con winget. Dice qué es, de dónde sale y qué
/// pasa después; sin el "Instalar" explícito no se instala nada.
/// </summary>
public sealed class InstallDialog : Window
{
    private const double CaptionHeight = 44;

    private readonly DialogLook _look;

    private InstallDialog(InstallRequest request, bool dark)
    {
        _look = new DialogLook(dark, Palette.Actions);
        Title = Strings.InstallWindowTitle(request.Name);
        ThemeMode = dark ? ThemeMode.Dark : ThemeMode.Light;
        FontFamily = DialogLook.Font;
        Resources["ContentControlThemeFontFamily"] = DialogLook.Font;
        Foreground = _look.Text;
        Width = 480;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;

        var panel = new StackPanel { Margin = new Thickness(24, CaptionHeight, 24, 22) };
        panel.Children.Add(Header(request.Name));
        Body = Strings.InstallBody(request.Name, request.Purpose);
        panel.Children.Add(Paragraph(Body, _look.Text, 14));
        panel.Children.Add(Platter(
            Strings.InstallDetails(request.Publisher, request.License)));
        panel.Children.Add(Buttons());
        Content = new Grid { Background = Tint(), Children = { panel } };

        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = CaptionHeight - 8, GlassFrameThickness = new Thickness(-1), UseAeroCaptionButtons = true });
        SourceInitialized += (_, _) => WindowBackdrop.TryApply(this, dark);
    }

    /// <summary>Texto principal (pruebas).</summary>
    internal string Body { get; }

    /// <summary>Muestra la ventana (modal, hilo STA de UI). <c>true</c> si se eligió "Instalar".</summary>
    public static bool Ask(InstallRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new InstallDialog(request, SystemTheme.AppsUseDark()).ShowDialog() == true;
    }

    /// <summary>Crea la ventana sin mostrarla (pruebas).</summary>
    internal static InstallDialog CreateForTests(InstallRequest request, bool dark = true) => new(request, dark);

    private static TextBlock Paragraph(string text, Brush foreground, double size) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        FontSize = size,
        Foreground = foreground,
        Margin = new Thickness(0, 0, 0, 12),
    };

    private LinearGradientBrush Tint() => new(
        _look.IsDark ? Color.FromArgb(0x30, 0x2A, 0x2A, 0x33) : Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF),
        _look.IsDark ? Color.FromArgb(0x50, 0x12, 0x12, 0x16) : Color.FromArgb(0x30, 0xF2, 0xF2, 0xF7),
        90);

    private Grid Header(string name)
    {
        var header = new Grid { Margin = new Thickness(0, 4, 0, 16) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.Children.Add(_look.IconTile("install", 48));
        var title = new TextBlock { Text = Strings.InstallItem(name), FontSize = 22, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
        Grid.SetColumn(title, 1);
        header.Children.Add(title);
        return header;
    }

    private Border Platter(string text) => new()
    {
        CornerRadius = new CornerRadius(14),
        Background = _look.Platter,
        BorderBrush = _look.PlatterBorder,
        BorderThickness = new Thickness(1),
        Padding = new Thickness(16, 12, 16, 0),
        Margin = new Thickness(0, 0, 0, 16),
        Child = Paragraph(text, _look.Muted, 12.5),
    };

    private Grid Buttons()
    {
        var buttons = new Grid();
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
        var cancel = DialogLook.Pill(Strings.ButtonNotNow, _look.Subtle, _look.Text);
        cancel.IsCancel = true;
        cancel.Height = 44;
        var install = DialogLook.Pill(Strings.ButtonInstall, _look.AccentGradient, Brushes.White);
        install.IsDefault = true;
        install.Height = 44;
        install.FontSize = 15;
        AutomationProperties.SetName(install, Strings.ButtonInstall);
        install.Click += (_, _) => DialogResult = true;
        Grid.SetColumn(install, 2);
        buttons.Children.Add(cancel);
        buttons.Children.Add(install);
        return buttons;
    }
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using Kumquat.Design;

namespace Kumquat.Dialogs.Look;

/// <summary>
/// Estilo "vidrio" de las ventanas (inspirado en visionOS): tipografía Inter, superficies
/// translúcidas sobre el material acrílico de Windows, botones en píldora y el color propio
/// de cada familia de herramientas.
/// </summary>
internal sealed class DialogLook
{
    private static readonly Lazy<FontFamily> InterFamily = new(LoadInter);
    private static readonly Lazy<Style> PillStyle = new(() => (Style)XamlReader.Parse(PillStyleXaml));

    public DialogLook(bool dark, Rgb accent)
    {
        IsDark = dark;
        Accent = ToColor(accent);
        AccentLight = ToColor(accent.Mix(new Rgb(255, 255, 255), 0.22));
        Text = Brush(dark ? Color.FromRgb(0xF5, 0xF5, 0xF7) : Color.FromRgb(0x1D, 0x1D, 0x1F));
        Muted = Brush(dark ? Color.FromArgb(0xA6, 0xEB, 0xEB, 0xF5) : Color.FromArgb(0x99, 0x3C, 0x3C, 0x43));
        Platter = Brush(dark ? Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x8C, 0xFF, 0xFF, 0xFF));
        PlatterBorder = Brush(dark ? Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x14, 0x00, 0x00, 0x00));
        Subtle = Brush(dark ? Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x0F, 0x00, 0x00, 0x00));
        AccentBrush = Brush(Accent);
        AccentGradient = new LinearGradientBrush(AccentLight, Accent, 90);
        AccentGradient.Freeze();
    }

    /// <summary>Tipografía de la interfaz (Inter si está junto al ejecutable; si no, Segoe UI).</summary>
    public static FontFamily Font => InterFamily.Value;

    public bool IsDark { get; }

    public Color Accent { get; }

    public Color AccentLight { get; }

    public Brush AccentBrush { get; }

    public LinearGradientBrush AccentGradient { get; }

    public Brush Text { get; }

    public Brush Muted { get; }

    /// <summary>Superficie translúcida (las "bandejas" de visionOS) sobre el fondo acrílico.</summary>
    public Brush Platter { get; }

    public Brush PlatterBorder { get; }

    /// <summary>Relleno tenue para botones secundarios y zonas vacías.</summary>
    public Brush Subtle { get; }

    /// <summary>Botón en píldora.</summary>
    public static Button Pill(object content, Brush background, Brush foreground)
    {
        var button = new Button { Content = content, Background = background, Foreground = foreground, Style = PillStyle.Value };
        return button;
    }

    /// <summary>Ícono vectorial de <see cref="Icons"/> en un cuadrado del lado dado.</summary>
    public static FrameworkElement Icon(string name, double size, Brush fill)
    {
        var path = new System.Windows.Shapes.Path { Data = IconGeometry.For(name), Fill = fill };
        var canvas = new Canvas { Width = Icons.ViewBox, Height = Icons.ViewBox, Children = { path } };
        return new Viewbox { Width = size, Height = size, Child = canvas, IsHitTestVisible = false };
    }

    /// <summary>Cuadrado redondeado de color con el ícono en blanco (como los íconos de app de Apple).</summary>
    public FrameworkElement IconTile(string? icon, double size)
    {
        var tile = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size * 0.28),
            Background = AccentGradient,
            BorderBrush = Brush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 14, ShadowDepth = 3, Opacity = 0.28, Color = Accent, Direction = 270 },
        };
        if (icon is not null)
        {
            tile.Child = Icon(icon, size * 0.56, Brushes.White);
        }

        return tile;
    }

    public static SolidColorBrush Brush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public static Color ToColor(Rgb rgb) => Color.FromRgb(rgb.R, rgb.G, rgb.B);

    private static FontFamily LoadInter()
    {
        // WPF agrupa los TTF estáticos de la carpeta por familia tipográfica: FontWeight elige el archivo.
        if (DesignAssets.FontFiles.Count == 0)
        {
            return new FontFamily("Segoe UI Variable Text, Segoe UI");
        }

        var folder = new Uri(DesignAssets.FontsDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
        return new FontFamily(folder, "./#" + DesignAssets.FontFamily + ", Segoe UI");
    }

    private const string PillStyleXaml = """
        <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
               xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Button">
          <Setter Property="Cursor" Value="Hand"/>
          <Setter Property="Height" Value="40"/>
          <Setter Property="Padding" Value="22,0"/>
          <Setter Property="FontSize" Value="14"/>
          <Setter Property="FontWeight" Value="SemiBold"/>
          <Setter Property="BorderThickness" Value="0"/>
          <Setter Property="BorderBrush" Value="#80FFFFFF"/>
          <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
          <Setter Property="Template">
            <Setter.Value>
              <ControlTemplate TargetType="Button">
                <Border x:Name="Bg" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                        BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="20" Padding="{TemplateBinding Padding}" SnapsToDevicePixels="True">
                  <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                </Border>
                <ControlTemplate.Triggers>
                  <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Bg" Property="Opacity" Value="0.86"/></Trigger>
                  <Trigger Property="IsPressed" Value="True"><Setter TargetName="Bg" Property="Opacity" Value="0.72"/></Trigger>
                  <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Bg" Property="BorderThickness" Value="2"/></Trigger>
                  <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.45"/></Trigger>
                </ControlTemplate.Triggers>
              </ControlTemplate>
            </Setter.Value>
          </Setter>
        </Style>
        """;
}

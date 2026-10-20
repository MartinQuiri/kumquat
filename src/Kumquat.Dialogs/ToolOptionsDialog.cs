// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using Kumquat.Core.Localization;
using Kumquat.Core.Tools;
using Kumquat.Design;
using Kumquat.Dialogs.Look;

namespace Kumquat.Dialogs;

/// <summary>
/// Ventana de opciones de una herramienta, construida a partir de sus parámetros: vidrio
/// acrílico, encabezado con el ícono de la herramienta y, si corresponde, una vista previa
/// interactiva (páginas para elegir, tramo de audio o video). Valida en vivo con la propia
/// herramienta: el error se muestra sin cerrar la ventana.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "Una ventana no es IDisposable: lo descartable se libera en Closed.")]
public sealed partial class ToolOptionsDialog : Window
{
    private const double CaptionHeight = 44;

    private readonly ToolDialogRequest _request;
    private readonly IReadOnlyList<ToolParameter> _parameters;
    private readonly DialogLook _look;
    private readonly Dictionary<string, Func<string>> _readers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextBox> _boxes = new(StringComparer.Ordinal);
    private readonly StackPanel _customRows = new();
    private readonly TextBlock _error;
    private readonly Border? _previewHost;

    private ToolOptionsDialog(ToolDialogRequest request, bool dark)
    {
        _request = request;
        _parameters = request.Parameters;
        _look = new DialogLook(dark, Palette.ForItem(request.ToolId));
        Title = Strings.ToolWindowTitle(request.Title);
        ThemeMode = _look.ThemeMode;
        FontFamily = DialogLook.Font;
        Resources["ContentControlThemeFontFamily"] = DialogLook.Font;
        Foreground = _look.Text;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = true;
        TrySetIcon();

        _error = new TextBlock { Foreground = DialogLook.Brush(Color.FromRgb(0xFF, 0x45, 0x3A)), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10), Visibility = Visibility.Collapsed };
        var sidebar = BuildSidebar();
        if (request.Preview is null)
        {
            Width = 440;
            SizeToContent = SizeToContent.Height;
            MaxHeight = SystemParameters.WorkArea.Height * 0.9;
            ResizeMode = ResizeMode.NoResize;
            sidebar.Margin = new Thickness(24, CaptionHeight, 24, 22);
            Content = Root(sidebar);
        }
        else
        {
            Width = Math.Min(1040, SystemParameters.WorkArea.Width * 0.92);
            Height = Math.Min(690, SystemParameters.WorkArea.Height * 0.92);
            MinWidth = 820;
            MinHeight = 520;
            ResizeMode = ResizeMode.CanResize;
            _previewHost = new Border { CornerRadius = new CornerRadius(18), Background = _look.Platter, BorderBrush = _look.PlatterBorder, BorderThickness = new Thickness(1), Margin = new Thickness(16, CaptionHeight, 8, 16) };
            sidebar.Margin = new Thickness(16, CaptionHeight, 24, 22);
            Content = Root(WithPreview(_previewHost, sidebar));
        }

        // Contenido hasta arriba (la franja superior sigue sirviendo para arrastrar) y botón cerrar del sistema.
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = CaptionHeight - 8, GlassFrameThickness = new Thickness(-1), UseAeroCaptionButtons = true, ResizeBorderThickness = SystemParameters.WindowResizeBorderThickness });
        SourceInitialized += (_, _) => WindowBackdrop.TryApply(this, dark);
        Loaded += (_, _) => OnLoaded();
        Closed += (_, _) => OnClosed();
    }

    /// <summary>Opciones elegidas (null si se canceló).</summary>
    public IReadOnlyDictionary<string, string>? Result { get; private set; }

    /// <summary>
    /// Muestra la ventana (bloquea el hilo de UI, pero bombea mensajes). Debe llamarse en el hilo STA.
    /// Devuelve todos los valores; los de texto solo si cambiaron respecto del valor inicial (así,
    /// al editar metadatos de varios archivos, solo se tocan los campos que el usuario modificó).
    /// </summary>
    public static IReadOnlyDictionary<string, string>? Ask(ToolDialogRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var dialog = new ToolOptionsDialog(request, SystemTheme.AppsUseDark());
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    /// <summary>Crea la ventana sin mostrarla (pruebas).</summary>
    internal static ToolOptionsDialog CreateForTests(IReadOnlyList<ToolParameter> parameters)
        => new(new ToolDialogRequest("test", "Prueba", "archivo", parameters, _ => null), dark: true);

    /// <summary>Lee los valores actuales de la ventana (útil para pruebas).</summary>
    internal Dictionary<string, string> Collect()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var parameter in _parameters.Where(p => p.Kind != ToolParameterKind.CustomFields))
        {
            var value = _readers[parameter.Key]();
            var changed = !string.Equals(value, parameter.DefaultValue ?? string.Empty, StringComparison.Ordinal);
            if (parameter.Kind != ToolParameterKind.Text || changed)
            {
                values[parameter.Key] = value;
            }
        }

        foreach (var row in _customRows.Children.OfType<Grid>())
        {
            var name = ((TextBox)row.Children[0]).Text.Trim();
            var value = ((TextBox)row.Children[1]).Text;
            if (name.Length > 0)
            {
                values[ToolParameter.CustomPrefix + name] = value;
            }
        }

        return values;
    }

    private static Grid WithPreview(Border preview, FrameworkElement sidebar)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
        grid.Children.Add(preview);
        Grid.SetColumn(sidebar, 1);
        grid.Children.Add(sidebar);
        return grid;
    }

    /// <summary>Capa raíz: un velo tenue sobre el acrílico (da cuerpo al vidrio) y el nombre de la app arriba.</summary>
    private Grid Root(UIElement content)
    {
        var root = new Grid
        {
            Background = new LinearGradientBrush(
                _look.IsDark ? Color.FromArgb(0x30, 0x2A, 0x2A, 0x33) : Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF),
                _look.IsDark ? Color.FromArgb(0x50, 0x12, 0x12, 0x16) : Color.FromArgb(0x30, 0xF2, 0xF2, 0xF7),
                90),
        };
        var caption = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(16, 12, 0, 0), VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left, IsHitTestVisible = false };
        if (File.Exists(DesignAssets.LogoPng))
        {
            var logo = new BitmapImage();
            logo.BeginInit();
            logo.CacheOption = BitmapCacheOption.OnLoad;
            logo.DecodePixelWidth = 64;
            logo.UriSource = new Uri(DesignAssets.LogoPng);
            logo.EndInit();
            caption.Children.Add(new Image { Source = logo, Width = 18, Height = 18, Margin = new Thickness(0, 0, 8, 0) });
        }

        caption.Children.Add(new TextBlock { Text = "Kumquat", Foreground = _look.Muted, FontSize = 12.5, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center });
        root.Children.Add(content);
        root.Children.Add(caption);
        return root;
    }

    private void TrySetIcon()
    {
        try
        {
            if (File.Exists(DesignAssets.LogoIco))
            {
                Icon = BitmapFrame.Create(new Uri(DesignAssets.LogoIco));
            }
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException)
        {
            // Sin ícono: queda el genérico.
        }
    }

    private void TryAccept()
    {
        var values = Collect();

        // La validación ve todos los valores (incluidos los no modificados) para chequear coherencia.
        var full = new Dictionary<string, string>(values, StringComparer.Ordinal);
        foreach (var parameter in _parameters.Where(p => p.Kind != ToolParameterKind.CustomFields && !full.ContainsKey(p.Key)))
        {
            full[parameter.Key] = parameter.DefaultValue ?? string.Empty;
        }

        var missing = _parameters.FirstOrDefault(p => p.Required && string.IsNullOrWhiteSpace(full.GetValueOrDefault(p.Key)));
        var error = missing is not null ? Strings.ValidateRequired(missing.Label) : _request.Validate(full);
        if (error is not null)
        {
            _error.Text = error;
            _error.Visibility = Visibility.Visible;
            Announcer.Say(_error, error);
            return;
        }

        Result = values;
        DialogResult = true;
    }
}

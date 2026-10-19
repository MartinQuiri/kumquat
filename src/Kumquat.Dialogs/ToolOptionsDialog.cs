// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Kumquat.Core.Tools;

namespace Kumquat.Dialogs;

/// <summary>
/// Ventana de opciones de una herramienta, construida a partir de sus parámetros. Valida en
/// vivo con la propia herramienta: el error se muestra sin cerrar la ventana.
/// </summary>
public sealed partial class ToolOptionsDialog : Window
{
    private readonly IReadOnlyList<ToolParameter> _parameters;
    private readonly Func<IReadOnlyDictionary<string, string>, string?> _validate;
    private readonly Dictionary<string, Func<string>> _readers = new(StringComparer.Ordinal);
    private readonly StackPanel _customRows = new();
    private readonly TextBlock _error;

    private ToolOptionsDialog(string title, string subtitle, IReadOnlyList<ToolParameter> parameters, Func<IReadOnlyDictionary<string, string>, string?> validate)
    {
        _parameters = parameters;
        _validate = validate;
        Title = $"Kumquat · {title}";
        ThemeMode = ThemeMode.System;
        Width = 460;
        SizeToContent = SizeToContent.Height;
        MaxHeight = SystemParameters.WorkArea.Height * 0.9;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = true;

        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 20, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = subtitle, Opacity = 0.7, Margin = new Thickness(0, 2, 0, 16), TextTrimming = TextTrimming.CharacterEllipsis });
        if (parameters.Any(p => p.Kind == ToolParameterKind.Text))
        {
            panel.Children.Add(new TextBlock { Text = "Deja un campo vacío para borrarlo.", Opacity = 0.7, Margin = new Thickness(0, 0, 0, 12), FontSize = 12 });
        }

        foreach (var parameter in parameters)
        {
            panel.Children.Add(BuildField(parameter));
        }

        _error = new TextBlock { Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
        panel.Children.Add(_error);
        panel.Children.Add(BuildButtons());

        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Loaded += (_, _) => BringToFront();
    }

    /// <summary>Opciones elegidas (null si se canceló).</summary>
    public IReadOnlyDictionary<string, string>? Result { get; private set; }

    /// <summary>
    /// Muestra la ventana (bloquea el hilo de UI, pero bombea mensajes). Debe llamarse en el hilo STA.
    /// Devuelve todos los valores; los de texto solo si cambiaron respecto del valor inicial (así,
    /// al editar metadatos de varios archivos, solo se tocan los campos que el usuario modificó).
    /// </summary>
    public static IReadOnlyDictionary<string, string>? Ask(string title, string subtitle, IReadOnlyList<ToolParameter> parameters, Func<IReadOnlyDictionary<string, string>, string?> validate)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(validate);
        var dialog = new ToolOptionsDialog(title, subtitle, parameters, validate);
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    /// <summary>Crea la ventana sin mostrarla (pruebas).</summary>
    internal static ToolOptionsDialog CreateForTests(IReadOnlyList<ToolParameter> parameters) => new("Prueba", "archivo", parameters, _ => null);

    /// <summary>Renderiza la ventana fuera de pantalla a un PNG (vista previa para la documentación).</summary>
    internal static void RenderPreview(string title, string subtitle, IReadOnlyList<ToolParameter> parameters, string pngPath, bool dark)
    {
        var dialog = new ToolOptionsDialog(title, subtitle, parameters, _ => null)
        {
            ThemeMode = dark ? ThemeMode.Dark : ThemeMode.Light,
            Left = -20000,
            Top = -20000,
            ShowActivated = false,
        };
        dialog.Show();
        dialog.UpdateLayout();
        var content = (FrameworkElement)dialog.Content;
        var scale = 2.0;
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)(content.ActualWidth * scale), (int)(content.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            // Con el tema Fluent el fondo real es el material Mica del sistema (transparente aquí): se simula.
            var background = new SolidColorBrush(dark ? Color.FromRgb(0x20, 0x20, 0x20) : Color.FromRgb(0xF3, 0xF3, 0xF3));
            context.DrawRectangle(background, null, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
            context.DrawRectangle(new VisualBrush(content), null, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
        }

        bitmap.Render(visual);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using (var file = File.Create(pngPath))
        {
            encoder.Save(file);
        }

        dialog.Close();
    }

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

    private StackPanel BuildField(ToolParameter parameter)
    {
        if (parameter.Kind == ToolParameterKind.CustomFields)
        {
            var section = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            section.Children.Add(new TextBlock { Text = parameter.Label, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
            section.Children.Add(_customRows);
            var add = new Button { Content = "+ Agregar campo", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 0) };
            add.Click += (_, _) => AddCustomRow();
            section.Children.Add(add);
            return section;
        }

        var field = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        var label = new Label { Content = parameter.Label + (parameter.Required ? " *" : string.Empty), Padding = new Thickness(0, 0, 0, 4) };
        field.Children.Add(label);
        Control input;
        if (parameter.Kind == ToolParameterKind.Password)
        {
            var box = new PasswordBox { Password = parameter.DefaultValue ?? string.Empty };
            _readers[parameter.Key] = () => box.Password;
            input = box;
        }
        else
        {
            var box = new TextBox { Text = parameter.DefaultValue ?? string.Empty };
            _readers[parameter.Key] = () => box.Text;
            input = box;
        }

        label.Target = input;
        AutomationProperties.SetName(input, parameter.Label);
        field.Children.Add(input);
        if (parameter.Hint is not null)
        {
            field.Children.Add(new TextBlock { Text = parameter.Hint, Opacity = 0.7, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
        }

        return field;
    }

    private void AddCustomRow()
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.6, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var name = new TextBox { Margin = new Thickness(0, 0, 6, 0) };
        var value = new TextBox { Margin = new Thickness(0, 0, 6, 0) };
        AutomationProperties.SetName(name, "Nombre del campo");
        AutomationProperties.SetName(value, "Valor del campo");
        var remove = new Button { Content = "✕", ToolTip = "Quitar" };
        remove.Click += (_, _) => _customRows.Children.Remove(row);
        Grid.SetColumn(value, 1);
        Grid.SetColumn(remove, 2);
        row.Children.Add(name);
        row.Children.Add(value);
        row.Children.Add(remove);
        _customRows.Children.Add(row);
        name.Focus();
    }

    private StackPanel BuildButtons()
    {
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var cancel = new Button { Content = "Cancelar", IsCancel = true, MinWidth = 96, Margin = new Thickness(0, 0, 8, 0) };
        var apply = new Button { Content = "Aplicar", IsDefault = true, MinWidth = 96 };
        apply.SetResourceReference(StyleProperty, "AccentButtonStyle");
        apply.Click += (_, _) => TryAccept();
        buttons.Children.Add(cancel);
        buttons.Children.Add(apply);
        return buttons;
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
        var error = missing is not null ? $"Completa «{missing.Label}»." : _validate(full);
        if (error is not null)
        {
            _error.Text = error;
            _error.Visibility = Visibility.Visible;
            return;
        }

        Result = values;
        DialogResult = true;
    }

    /// <summary>
    /// Tras un drop el que está al frente es el Explorador: sin esto la ventana se abriría detrás.
    /// </summary>
    private void BringToFront()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var current = GetCurrentThreadId();
        var foreground = GetWindowThreadProcessId(GetForegroundWindow(), 0);
        var attached = foreground != current && AttachThreadInput(current, foreground, attach: true);
        Activate();
        SetForegroundWindow(handle);
        if (attached)
        {
            AttachThreadInput(current, foreground, attach: false);
        }

        MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
    }

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint hwnd, nint processId);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetCurrentThreadId();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachThreadInput(uint attachFrom, uint attachTo, [MarshalAs(UnmanagedType.Bool)] bool attach);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint hwnd);
}

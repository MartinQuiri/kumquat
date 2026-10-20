// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using Kumquat.Core.Text;
using Kumquat.Design;
using Kumquat.Dialogs.Look;

namespace Kumquat.Dialogs.Search;

/// <summary>
/// Buscador de acciones (como Spotlight): se abre con Ctrl+Alt+K o desde el clic derecho, se
/// escribe para filtrar, ↑↓ eligen, Enter aplica y Esc o un clic afuera cierran. Todo con teclado.
/// </summary>
public sealed partial class CommandPalette : Window
{
    private const double PaletteWidth = 620;

    /// <summary>Cuadro de texto sin marco propio: el marco es la "píldora" de vidrio que lo rodea.</summary>
    private const string BareTextBoxXaml = """
        <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="TextBox">
          <Setter Property="Background" Value="Transparent"/>
          <Setter Property="BorderThickness" Value="0"/>
          <Setter Property="Template">
            <Setter.Value>
              <ControlTemplate TargetType="TextBox">
                <ScrollViewer x:Name="PART_ContentHost" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                              Padding="{TemplateBinding Padding}" VerticalAlignment="Center" Focusable="False"
                              HorizontalScrollBarVisibility="Hidden" VerticalScrollBarVisibility="Hidden"/>
              </ControlTemplate>
            </Setter.Value>
          </Setter>
        </Style>
        """;

    /// <summary>Filas con esquinas redondeadas; la elegida, con el color de acento translúcido.</summary>
    private const string RowStyleXaml = """
        <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
               xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="ListBoxItem">
          <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
          <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
          <Setter Property="Template">
            <Setter.Value>
              <ControlTemplate TargetType="ListBoxItem">
                <Border x:Name="Bg" CornerRadius="10" Background="Transparent" Padding="{TemplateBinding Padding}" Margin="0,1">
                  <ContentPresenter HorizontalAlignment="Stretch" VerticalAlignment="Center"/>
                </Border>
                <ControlTemplate.Triggers>
                  <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Bg" Property="Background" Value="{DynamicResource KumquatHover}"/></Trigger>
                  <Trigger Property="IsSelected" Value="True"><Setter TargetName="Bg" Property="Background" Value="{DynamicResource KumquatSelected}"/></Trigger>
                </ControlTemplate.Triggers>
              </ControlTemplate>
            </Setter.Value>
          </Setter>
        </Style>
        """;

    private readonly PaletteRequest _request;
    private readonly DialogLook _look;
    private readonly TextBox _query;
    private readonly ListBox _list;
    private readonly TextBlock _empty;
    private bool _closing;

    private CommandPalette(PaletteRequest request, bool dark)
    {
        _request = request;
        _look = new DialogLook(dark, Palette.Kumquat);
        Title = "Kumquat · Buscar acción";
        ThemeMode = dark ? ThemeMode.Dark : ThemeMode.Light;
        FontFamily = DialogLook.Font;
        Resources["ContentControlThemeFontFamily"] = DialogLook.Font;
        Foreground = _look.Text;
        Width = PaletteWidth;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.Manual;

        Resources["KumquatHover"] = _look.Subtle;
        Resources["KumquatSelected"] = DialogLook.Brush(Color.FromArgb(0x40, _look.Accent.R, _look.Accent.G, _look.Accent.B));
        _query = new TextBox { FontSize = 18, Padding = new Thickness(10, 10, 12, 10), Style = (Style)System.Windows.Markup.XamlReader.Parse(BareTextBoxXaml), Foreground = _look.Text, CaretBrush = _look.Text };
        AutomationProperties.SetName(_query, "Buscar acción");
        _query.TextChanged += (_, _) => Refresh();
        _list = new ListBox
        {
            MaxHeight = 360,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Margin = new Thickness(0, 8, 0, 0),
            ItemContainerStyle = (Style)System.Windows.Markup.XamlReader.Parse(RowStyleXaml),
            Focusable = false,
        };
        AutomationProperties.SetName(_list, "Acciones");
        ScrollViewer.SetHorizontalScrollBarVisibility(_list, ScrollBarVisibility.Disabled);
        _list.MouseLeftButtonUp += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(_list, source) is ListBoxItem)
            {
                Accept();
            }
        };
        _empty = new TextBlock { Text = "Nada coincide. Prueba con otra palabra (p. ej. pdf, comprimir, ruta).", Foreground = _look.Muted, Margin = new Thickness(14, 14, 14, 10), Visibility = Visibility.Collapsed, TextWrapping = TextWrapping.Wrap };

        Content = Build();
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 0, GlassFrameThickness = new Thickness(-1), UseAeroCaptionButtons = false, ResizeBorderThickness = new Thickness(0) });
        SourceInitialized += (_, _) => WindowBackdrop.TryApply(this, dark);
        Loaded += (_, _) => OnLoaded();
        Deactivated += (_, _) => Cancel();
        PreviewKeyDown += OnKey;
        Refresh();
    }

    /// <summary>Id elegido, o <c>null</c> si se canceló.</summary>
    public string? Chosen { get; private set; }

    /// <summary>Muestra el buscador (modal, en el hilo STA de UI) y devuelve el id elegido o <c>null</c>.</summary>
    public static string? Ask(PaletteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var palette = new CommandPalette(request, SystemTheme.AppsUseDark());
        palette.ShowDialog();
        return palette.Chosen;
    }

    /// <summary>Crea el buscador sin mostrarlo (pruebas).</summary>
    internal static CommandPalette CreateForTests(PaletteRequest request) => new(request, dark: true);

    /// <summary>Texto de búsqueda (pruebas).</summary>
    internal string Query
    {
        get => _query.Text;
        set => _query.Text = value;
    }

    /// <summary>Títulos visibles, en orden (pruebas).</summary>
    internal IReadOnlyList<string> VisibleTitles => [.. _list.Items.Cast<ListBoxItem>().Select(i => ((PaletteItem)i.Tag).Title)];

    /// <summary>Título resaltado (pruebas).</summary>
    internal string? SelectedTitle => (_list.SelectedItem as ListBoxItem)?.Tag is PaletteItem item ? item.Title : null;

    /// <summary>Simula una tecla (pruebas).</summary>
    internal void PressForTests(Key key) => Handle(key);

    /// <summary>Renderiza el buscador con una búsqueda a un PNG (documentación); el acrílico se simula.</summary>
    internal static void RenderPreview(PaletteRequest request, string query, string pngPath, bool dark)
    {
        var palette = new CommandPalette(request, dark) { Left = -20000, Top = -20000, ShowActivated = false };
        palette._closing = true; // fuera de pantalla nunca se activa: que Deactivated no lo cierre
        palette.Show();
        palette.Query = query;
        palette.UpdateLayout();
        var content = (FrameworkElement)palette.Content;
        var size = new Size(content.ActualWidth, content.ActualHeight);
        const double scale = 2.0;
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)(size.Width * scale), (int)(size.Height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            var backdrop = new LinearGradientBrush(
                dark ? Color.FromRgb(0x2B, 0x2A, 0x38) : Color.FromRgb(0xF4, 0xEE, 0xF2),
                dark ? Color.FromRgb(0x1E, 0x24, 0x2E) : Color.FromRgb(0xE9, 0xF0, 0xF6),
                45);
            context.DrawRoundedRectangle(backdrop, null, new Rect(size), 12, 12);
            context.DrawRectangle(new VisualBrush(content), null, new Rect(size));
        }

        bitmap.Render(visual);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using (var file = File.Create(pngPath))
        {
            encoder.Save(file);
        }

        palette.Close();
    }

    private Border Build()
    {
        var panel = new StackPanel { Margin = new Thickness(14, 14, 14, 10) };
        panel.Children.Add(Header());
        var search = new Border { CornerRadius = new CornerRadius(12), Background = _look.Subtle, Margin = new Thickness(0, 12, 0, 0), Child = SearchRow() };
        panel.Children.Add(search);
        panel.Children.Add(_list);
        panel.Children.Add(_empty);
        panel.Children.Add(new TextBlock { Text = "↑ ↓ elegir   ·   Enter aplicar   ·   Esc cerrar", Foreground = _look.Muted, FontSize = 11.5, Margin = new Thickness(4, 10, 0, 0) });
        var tint = new LinearGradientBrush(
            _look.IsDark ? Color.FromArgb(0x40, 0x2A, 0x2A, 0x33) : Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF),
            _look.IsDark ? Color.FromArgb(0x60, 0x12, 0x12, 0x16) : Color.FromArgb(0x40, 0xF2, 0xF2, 0xF7),
            90);
        return new Border { Background = tint, Child = panel };
    }

    private Grid Header()
    {
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.Children.Add(_look.IconTile(_request.Icon, 36));
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        text.Children.Add(new TextBlock { Text = _request.Title, FontSize = 15, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        if (_request.Subtitle is not null)
        {
            text.Children.Add(new TextBlock { Text = _request.Subtitle, FontSize = 12.5, Foreground = _look.Muted });
        }

        Grid.SetColumn(text, 1);
        header.Children.Add(text);
        return header;
    }

    private Grid SearchRow()
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var icon = DialogLook.Icon("search", 18, _look.Muted);
        icon.Margin = new Thickness(12, 0, 0, 0);
        icon.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(icon);
        _query.Background = Brushes.Transparent;
        Grid.SetColumn(_query, 1);
        row.Children.Add(_query);
        return row;
    }

    private ListBoxItem Row(PaletteItem item)
    {
        var row = new Grid { Margin = new Thickness(2, 3, 2, 3) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var icon = item.Icon is null ? new Border { Width = 18 } : DialogLook.Icon(item.Icon, 18, DialogLook.Brush(DialogLook.ToColor(item.Color)));
        icon.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(icon);
        var title = new TextBlock { Text = item.Title, FontSize = 14, Margin = new Thickness(12, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        Grid.SetColumn(title, 1);
        row.Children.Add(title);
        var group = new TextBlock { Text = item.Group, FontSize = 12, Foreground = _look.Muted, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(group, 2);
        row.Children.Add(group);
        var container = new ListBoxItem { Content = row, Tag = item, Padding = new Thickness(10, 7, 12, 7), Focusable = false };
        AutomationProperties.SetName(container, $"{item.Title}, {item.Group}");
        return container;
    }

    private void Refresh()
    {
        var matches = CommandSearch.Filter(_request.Items, _query.Text, i => i.Title, i => i.Keywords);
        _list.Items.Clear();
        foreach (var item in matches)
        {
            _list.Items.Add(Row(item));
        }

        _list.SelectedIndex = matches.Count > 0 ? 0 : -1;
        _list.Visibility = matches.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _empty.Visibility = matches.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (Handle(e.Key))
        {
            e.Handled = true;
        }
    }

    /// <summary>Las teclas de navegación se manejan aquí: el foco queda siempre en el cuadro de búsqueda.</summary>
    private bool Handle(Key key)
    {
        var count = _list.Items.Count;
        switch (key)
        {
            case Key.Escape:
                Cancel();
                return true;
            case Key.Enter:
                Accept();
                return true;
            case Key.Down when count > 0:
                Move((_list.SelectedIndex + 1) % count);
                return true;
            case Key.Up when count > 0:
                Move((_list.SelectedIndex - 1 + count) % count);
                return true;
            case Key.PageDown when count > 0:
                Move(Math.Min(count - 1, _list.SelectedIndex + 6));
                return true;
            case Key.PageUp when count > 0:
                Move(Math.Max(0, _list.SelectedIndex - 6));
                return true;
            default:
                return false;
        }
    }

    private void Move(int index)
    {
        _list.SelectedIndex = index;
        _list.ScrollIntoView(_list.SelectedItem);
    }

    private void Accept()
    {
        if (_closing || (_list.SelectedItem as ListBoxItem)?.Tag is not PaletteItem item)
        {
            return;
        }

        Chosen = item.Id;
        _closing = true;
        if (IsVisible)
        {
            Close();
        }
    }

    private void Cancel()
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        if (IsVisible)
        {
            Close();
        }
    }

    /// <summary>Arriba al centro del monitor donde está el cursor, como Spotlight; foco en el cuadro.</summary>
    private void OnLoaded()
    {
        GetCursorPos(out var cursor);
        var source = PresentationSource.FromVisual(this);
        var toDip = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var monitor = MonitorFromPoint(cursor, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (GetMonitorInfoW(monitor, ref info))
        {
            var topLeft = toDip.Transform(new Point(info.Work.Left, info.Work.Top));
            var bottomRight = toDip.Transform(new Point(info.Work.Right, info.Work.Bottom));
            Left = topLeft.X + ((bottomRight.X - topLeft.X - ActualWidth) / 2);
            Top = topLeft.Y + ((bottomRight.Y - topLeft.Y) * 0.2);
        }

        BringToFront();
        _query.Focus();
        Keyboard.Focus(_query);
    }

    /// <summary>
    /// Al frente, con el foco: el buscador es para escribir. Windows solo deja tomar el foco a quien
    /// produjo la última entrada (o recibió el permiso); si con eso no alcanza, se pulsa Alt —el
    /// recurso habitual de los lanzadores tipo Spotlight— y se vuelve a pedir.
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

        if (GetForegroundWindow() != handle)
        {
            PressAlt();
            SetForegroundWindow(handle);
            Activate();
        }
    }

    private static unsafe void PressAlt()
    {
        const ushort vkMenu = 0x12;
        const uint keyUp = 0x2;
        var inputs = stackalloc KeyboardInput[2];
        inputs[0] = new KeyboardInput { Type = 1, Vk = vkMenu };
        inputs[1] = new KeyboardInput { Type = 1, Vk = vkMenu, Flags = keyUp };
        _ = SendInput(2, inputs, sizeof(KeyboardInput));
    }

    /// <summary>INPUT de teclado (40 bytes en 64 bits: tipo, relleno, KEYBDINPUT y la unión completa).</summary>
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct KeyboardInput
    {
        [FieldOffset(0)]
        public uint Type;

        [FieldOffset(8)]
        public ushort Vk;

        [FieldOffset(10)]
        public ushort Scan;

        [FieldOffset(12)]
        public uint Flags;
    }

    [LibraryImport("user32.dll")]
    private static unsafe partial uint SendInput(uint count, KeyboardInput* inputs, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out NativePoint point);

    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromPoint(NativePoint point, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfoW(nint monitor, ref MonitorInfo info);

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

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using Kumquat.Core.Formats;
using Kumquat.Core.Localization;
using Kumquat.Core.Shelf;
using Kumquat.Design;
using Kumquat.Dialogs.Look;

namespace Kumquat.Dialogs.Shelf;

/// <summary>
/// Bandeja flotante ("Flotar"): una pila de archivos siempre visible para arrastrarlos después a
/// otra carpeta, un correo o una página donde subirlos. La pila arrastra todos; un clic la abre en
/// una lista donde cada archivo se arrastra solo. Se le pueden soltar más archivos encima. Nada se
/// copia ni se mueve al dejarlos aquí, y lo que se mueve a otra carpeta sale de la bandeja.
/// </summary>
public sealed partial class ShelfWindow : Window
{
    private const double CollapsedWidth = 150;
    private const double ExpandedWidth = 250;
    private const double CardSize = 70;
    private const int ThumbnailPixels = 160;
    private const double ScreenMargin = 8;

    private static readonly Color BadgeRed = Color.FromRgb(0xFF, 0x3B, 0x30);

    private readonly DialogLook _look;
    private readonly ShelfContents _contents = new();
    private readonly Dictionary<string, Thumbnail?> _thumbnails = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _loading = new(StringComparer.OrdinalIgnoreCase);
    private readonly Border _root;
    private readonly Grid _stack = new() { Width = 116, Height = 104, Background = Brushes.Transparent, Cursor = Cursors.Hand, Focusable = true };
    private readonly TextBlock _summary;
    private readonly Button _collapse;
    private readonly StackPanel _rows = new();
    private readonly ScrollViewer _list;
    private readonly DispatcherTimer _prune;
    private readonly bool _atCursor;
    private readonly bool _shellThumbnails;
    private Point? _pressedAt;
    private bool _draggingOut;
    private bool _expanded;

    /// <summary>Abre una bandeja nueva con estos archivos, junto al puntero.</summary>
    public static ShelfWindow Open(IReadOnlyList<string> paths)
    {
        var shelf = new ShelfWindow(paths, SystemTheme.AppsUseDark(), atCursor: true, shellThumbnails: true);
        shelf.Show();
        return shelf;
    }

    internal ShelfWindow(IEnumerable<string> paths, bool dark, bool atCursor, bool shellThumbnails)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _atCursor = atCursor;
        _shellThumbnails = shellThumbnails;
        _look = new DialogLook(dark, Palette.Kumquat);
        Title = Strings.ShelfName;
        ThemeMode = _look.ThemeMode;
        FontFamily = DialogLook.Font;
        Resources["ContentControlThemeFontFamily"] = DialogLook.Font;
        Foreground = _look.Text;
        Width = CollapsedWidth;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None;
        WindowStartupLocation = WindowStartupLocation.Manual;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        AllowDrop = true;
        AutomationProperties.SetName(this, Strings.ShelfName);

        _summary = new TextBlock { FontSize = 12, Foreground = _look.Muted, HorizontalAlignment = HorizontalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(8, 4, 8, 0) };
        AutomationProperties.SetLiveSetting(_summary, AutomationLiveSetting.Polite);
        _collapse = CollapseButton();
        _list = new ScrollViewer { Content = _rows, MaxHeight = 360, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
        WireStack();

        var panel = new StackPanel { Margin = new Thickness(8, 6, 8, 10) };
        panel.Children.Add(TopBar());
        panel.Children.Add(_stack);
        panel.Children.Add(_summary);
        panel.Children.Add(_list);
        _root = new Border { Background = _look.WindowBackground(Tint()), BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(2), Child = panel };
        Content = _root;
        WireWindow(dark);

        // Lo que se movió o borró por fuera sale de la bandeja; vacía, se cierra.
        _prune = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (_, _) => Prune(), Dispatcher);
        Closed += (_, _) => _prune.Stop();

        _contents.Add(paths);
        Refresh();
        _prune.Start();
    }

    /// <summary>
    /// Bandeja sin ubicar junto al puntero ni pedirle miniaturas al Explorador (pruebas y vistas
    /// previas): así no dependen de las extensiones del Explorador instaladas en cada equipo.
    /// </summary>
    internal static ShelfWindow CreateForTests(IEnumerable<string> paths, bool dark = true) => new(paths, dark, atCursor: false, shellThumbnails: false);

    /// <summary>"N archivos" bajo la pila (pruebas).</summary>
    internal string SummaryForTests => _summary.Text;

    /// <summary>Nombres de las filas de la lista (pruebas).</summary>
    internal IReadOnlyList<string> RowNamesForTests => [.. _rows.Children.OfType<FrameworkElement>().Select(AutomationProperties.GetName)];

    /// <summary>Si la lista está abierta (pruebas).</summary>
    internal bool IsExpandedForTests => _expanded;

    /// <summary>Si la bandeja se cerró sola (pruebas).</summary>
    internal bool IsClosedForTests { get; private set; }

    /// <summary>
    /// Pone una miniatura sin pedírsela al Explorador (pruebas y vistas previas: así no dependen de
    /// las extensiones del Explorador instaladas en cada equipo).
    /// </summary>
    internal void SetThumbnailForTests(string path, BitmapSource image, bool isIcon)
    {
        _thumbnails[Path.GetFullPath(path)] = new Thumbnail(image, isIcon);
        Refresh();
    }

    /// <summary>Lo mismo que soltar archivos encima (pruebas).</summary>
    internal void AddForTests(IEnumerable<string> paths) => AddPaths(paths);

    /// <summary>Lo mismo que el temporizador (pruebas).</summary>
    internal void PruneForTests() => Prune();

    /// <summary>Lo mismo que un clic en la pila (pruebas).</summary>
    internal void ToggleForTests() => SetExpanded(!_expanded);

    /// <summary>Renderiza la bandeja a un PNG (documentación).</summary>
    internal static void RenderPreview(ShelfWindow shelf, string pngPath, bool dark) => WindowPreview.Render(shelf, pngPath, dark);

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        IsClosedForTests = true;
        base.OnClosed(e);
    }

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static partial nint GetWindowLongPtr(nint hwnd, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static partial nint SetWindowLongPtr(nint hwnd, int index, nint value);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out NativePoint point);

    private Button CollapseButton()
    {
        var collapse = DialogLook.Pill(string.Empty, Brushes.Transparent, _look.Text);
        collapse.FontSize = 12;
        collapse.FontWeight = FontWeights.SemiBold;
        collapse.Height = 24;
        collapse.Padding = new Thickness(8, 0, 8, 0);
        collapse.ToolTip = Strings.ShelfCollapse;
        collapse.Visibility = Visibility.Collapsed;
        collapse.HorizontalAlignment = HorizontalAlignment.Center;
        collapse.Click += (_, _) => SetExpanded(false);
        return collapse;
    }

    /// <summary>La X para cerrar (los archivos no se tocan) y, con la lista abierta, el botón para ocultarla.</summary>
    private Grid TopBar()
    {
        var close = DialogLook.Pill(DialogLook.Icon("close", 9, _look.Muted), Brushes.Transparent, _look.Muted);
        close.Width = 22;
        close.Height = 22;
        close.Padding = new Thickness(0);
        close.HorizontalAlignment = HorizontalAlignment.Left;
        close.ToolTip = Strings.ShelfClose;
        AutomationProperties.SetName(close, Strings.ShelfClose);
        close.Click += (_, _) => Close();
        return new Grid { Height = 24, Children = { close, _collapse } };
    }

    /// <summary>La pila: arrastrarla saca todos los archivos; un clic (o Enter) abre la lista.</summary>
    private void WireStack()
    {
        AutomationProperties.SetHelpText(_stack, Strings.ShelfStackHelp);
        _stack.ToolTip = Strings.ShelfStackHelp;
        _stack.HorizontalAlignment = HorizontalAlignment.Center;
        _stack.Margin = new Thickness(0, 2, 0, 0);
        _stack.PreviewMouseLeftButtonDown += (_, e) => Press(e);
        _stack.MouseMove += (_, e) => DragIfMoved(e, _contents.Items);
        _stack.MouseLeftButtonUp += (_, e) =>
        {
            if (_pressedAt is not null)
            {
                _pressedAt = null;
                SetExpanded(!_expanded);
            }

            e.Handled = true;
        };
        _stack.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Space)
            {
                SetExpanded(!_expanded);
                e.Handled = true;
            }
        };
    }

    private void WireWindow(bool dark)
    {
        // Arrastrar el fondo mueve la bandeja; la pila y las filas arrastran archivos.
        MouseLeftButtonDown += (_, e) =>
        {
            if (!e.Handled && e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        };
        KeyDown += OnKeyDown;
        DragEnter += OnDragOver;
        DragOver += OnDragOver;
        DragLeave += (_, _) => _root.BorderBrush = Brushes.Transparent;
        Drop += OnDrop;

        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 0, GlassFrameThickness = new Thickness(-1), ResizeBorderThickness = new Thickness(0), UseAeroCaptionButtons = false });
        SourceInitialized += (_, _) =>
        {
            ToolWindow();
            WindowBackdrop.TryApply(this, dark);
        };
        Loaded += (_, _) => PlaceAtCursor();
    }

    /// <summary>Fuera de la barra de tareas y de Alt+Tab, como el panel de progreso.</summary>
    private void ToolWindow()
    {
        const int GwlExStyle = -20;
        const nint WsExToolWindow = 0x00000080;
        var handle = new WindowInteropHelper(this).Handle;
        _ = SetWindowLongPtr(handle, GwlExStyle, GetWindowLongPtr(handle, GwlExStyle) | WsExToolWindow);
    }

    /// <summary>Centrada en el puntero (donde se soltó en la rueda), dentro de la pantalla.</summary>
    private void PlaceAtCursor()
    {
        if (!_atCursor || !GetCursorPos(out var cursor) || PresentationSource.FromVisual(this)?.CompositionTarget is not { } target)
        {
            return;
        }

        var at = target.TransformFromDevice.Transform(new Point(cursor.X, cursor.Y));
        Left = Math.Clamp(at.X - (ActualWidth / 2), SystemParameters.VirtualScreenLeft + ScreenMargin, SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - ActualWidth - ScreenMargin);
        Top = Math.Clamp(at.Y - (ActualHeight / 2), SystemParameters.VirtualScreenTop + ScreenMargin, SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - ActualHeight - ScreenMargin);
    }

    private LinearGradientBrush Tint() => new(
        _look.IsDark ? Color.FromArgb(0x40, 0x2A, 0x2A, 0x33) : Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF),
        _look.IsDark ? Color.FromArgb(0x60, 0x12, 0x12, 0x16) : Color.FromArgb(0x40, 0xF2, 0xF2, 0xF7),
        90);

    private void SetExpanded(bool expanded)
    {
        _expanded = expanded;
        Width = expanded ? ExpandedWidth : CollapsedWidth;
        _list.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        _collapse.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        _summary.Visibility = expanded ? Visibility.Collapsed : Visibility.Visible;
        Refresh();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _expanded)
        {
            SetExpanded(false);
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && Keyboard.FocusedElement is FrameworkElement { Tag: string path })
        {
            _contents.Remove(path);
            AfterChange();
            e.Handled = true;
        }
    }

    // ---------- Soltar archivos encima ----------

    private void OnDragOver(object sender, DragEventArgs e)
    {
        var accepts = !_draggingOut && e.Data.GetDataPresent(DataFormats.FileDrop);
        // Solo se guarda la referencia: "vincular" si el origen lo permite (el cursor lo dice), si no "copiar".
        e.Effects = !accepts ? DragDropEffects.None
            : e.AllowedEffects.HasFlag(DragDropEffects.Link) ? DragDropEffects.Link
            : e.AllowedEffects & DragDropEffects.Copy;
        _root.BorderBrush = accepts ? _look.AccentBrush : Brushes.Transparent;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        _root.BorderBrush = Brushes.Transparent;
        if (!_draggingOut && e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            AddPaths(paths);
        }

        e.Handled = true;
    }

    private void AddPaths(IEnumerable<string> paths)
    {
        if (_contents.Add(paths) > 0)
        {
            Refresh();
        }
    }

    // ---------- Arrastrar archivos hacia afuera ----------

    private void Press(MouseButtonEventArgs e)
    {
        _pressedAt = e.GetPosition(this);
        e.Handled = true; // que no mueva la ventana
    }

    private void DragIfMoved(MouseEventArgs e, IReadOnlyList<string> paths)
    {
        if (_pressedAt is not { } start || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var now = e.GetPosition(this);
        if (Math.Abs(now.X - start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(now.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _pressedAt = null;
        DragOut([.. paths]);
    }

    /// <summary>
    /// Arrastra los archivos como desde el Explorador: el destino decide si copia, mueve o adjunta.
    /// Después se mira el disco, porque al mover dentro de un mismo disco el Explorador no siempre
    /// avisa al origen.
    /// </summary>
    private void DragOut(string[] paths)
    {
        if (paths.Length == 0)
        {
            return;
        }

        _draggingOut = true;
        try
        {
            DragDrop.DoDragDrop(this, new DataObject(DataFormats.FileDrop, paths), DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);
        }
        finally
        {
            _draggingOut = false;
        }

        Prune();
    }

    private void Prune()
    {
        if (_contents.RemoveMissing().Count > 0)
        {
            AfterChange();
        }
    }

    private void AfterChange()
    {
        if (_contents.Count == 0)
        {
            Close();
            return;
        }

        Refresh();
    }

    // ---------- Dibujo ----------

    private void Refresh()
    {
        _summary.Text = _contents.Count == 1 ? Path.GetFileName(_contents.Items[0]) : _contents.Summary;
        _collapse.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children =
            {
                new System.Windows.Shapes.Path { Data = Geometry.Parse("M0,5 L4.5,0.5 L9,5"), Stroke = _look.Text, StrokeThickness = 1.6, Width = 10, Height = 6, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0) },
                new TextBlock { Text = _contents.Summary },
            },
        };
        AutomationProperties.SetName(_collapse, Strings.ShelfCollapse);
        AutomationProperties.SetName(_stack, _contents.Summary);
        DrawStack();
        if (_expanded)
        {
            DrawRows();
        }
    }

    /// <summary>Hasta tres tarjetas un poco giradas, el clip y el número de archivos.</summary>
    private void DrawStack()
    {
        _stack.Children.Clear();
        var top = _contents.Items.TakeLast(3).ToList();
        double[] angles = top.Count switch { 1 => [0], 2 => [-7, 3], _ => [-10, 7, -1] };
        double[] offsets = top.Count switch { 1 => [0], 2 => [-6, 4], _ => [-10, 9, 0] };
        for (var i = 0; i < top.Count; i++)
        {
            var card = Card(top[i], CardSize);
            card.RenderTransformOrigin = new Point(0.5, 0.5);
            card.RenderTransform = new TransformGroup { Children = { new RotateTransform(angles[i]), new TranslateTransform(offsets[i], 0) } };
            _stack.Children.Add(card);
        }

        var clip = DialogLook.Icon("paperclip", 34, _look.Muted);
        clip.HorizontalAlignment = HorizontalAlignment.Right;
        clip.VerticalAlignment = VerticalAlignment.Top;
        clip.Margin = new Thickness(0, -4, 10, 0);
        clip.RenderTransformOrigin = new Point(0.5, 0.5);
        clip.RenderTransform = new RotateTransform(28);
        clip.IsHitTestVisible = false;
        _stack.Children.Add(clip);

        if (_contents.Count > 1)
        {
            var count = new TextBlock { Text = _contents.Count.ToString(System.Globalization.CultureInfo.CurrentCulture), Foreground = Brushes.White, FontSize = 12, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var badge = new Border
            {
                MinWidth = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(6, 0, 6, 0),
                Background = _look.IsHighContrast ? SystemColors.HighlightBrush : DialogLook.Brush(BadgeRed),
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 6, 4),
                Child = count,
                IsHitTestVisible = false,
            };
            _stack.Children.Add(badge);
        }
    }

    private void DrawRows()
    {
        _rows.Children.Clear();
        foreach (var path in _contents.Items)
        {
            _rows.Children.Add(Row(path));
        }
    }

    private Grid Row(string path)
    {
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
        var label = new TextBlock { Text = name, FontSize = 12.5, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 4, 0) };
        var remove = DialogLook.Pill(DialogLook.Icon("close", 8, _look.Muted), Brushes.Transparent, _look.Muted);
        remove.Width = 22;
        remove.Height = 22;
        remove.Padding = new Thickness(0);
        remove.Focusable = false;
        remove.ToolTip = Strings.ShelfRemove(name);
        AutomationProperties.SetName(remove, Strings.ShelfRemove(name));
        remove.Click += (_, e) =>
        {
            _contents.Remove(path);
            AfterChange();
            e.Handled = true;
        };

        var row = new Grid { Height = 46, Margin = new Thickness(0, 2, 0, 2), Background = Brushes.Transparent, Cursor = Cursors.Hand, Focusable = true, Tag = path, ToolTip = Strings.ShelfItemHelp };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(Card(path, 40));
        Grid.SetColumn(label, 1);
        row.Children.Add(label);
        Grid.SetColumn(remove, 2);
        row.Children.Add(remove);
        AutomationProperties.SetName(row, name);
        AutomationProperties.SetHelpText(row, Strings.ShelfItemHelp);

        row.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2)
            {
                OpenFile(path);
                e.Handled = true;
                return;
            }

            if (e.OriginalSource is not DependencyObject source || !IsInside(source, remove))
            {
                Press(e);
                _ = row.Focus();
            }
        };
        row.MouseMove += (_, e) => DragIfMoved(e, [path]);
        row.MouseLeftButtonUp += (_, _) => _pressedAt = null;
        row.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                OpenFile(path);
                e.Handled = true;
            }
        };
        return row;
    }

    private static bool IsInside(DependencyObject element, DependencyObject container)
    {
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, container))
            {
                return true;
            }
        }

        return false;
    }

    private static void OpenFile(string path)
    {
        try
        {
            using var _ = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Sin programa asociado: no es un error de Kumquat.
        }
    }

    /// <summary>Tarjeta con la miniatura (foto recortada a la tarjeta) o el ícono (entero).</summary>
    private Border Card(string path, double size)
    {
        var card = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size * 0.14),
            Background = _look.IsHighContrast ? SystemColors.WindowBrush : Brushes.White,
            BorderBrush = _look.PlatterBorder,
            BorderThickness = new Thickness(1),
            Effect = _look.IsHighContrast ? null : new DropShadowEffect { BlurRadius = 10, ShadowDepth = 2, Opacity = 0.28, Color = Colors.Black },
        };
        if (_thumbnails.TryGetValue(path, out var thumbnail) && thumbnail is not null)
        {
            Fill(card, thumbnail);
        }
        else
        {
            card.Child = Fallback(path, size);
            LoadThumbnail(path);
        }

        return card;
    }

    /// <summary>Una foto o un PDF llenan la tarjeta (recortados); un ícono va entero, con margen.</summary>
    private static void Fill(Border card, Thumbnail thumbnail)
    {
        if (thumbnail.IsIcon)
        {
            card.Padding = new Thickness(card.Width * 0.14);
            card.Child = new Image { Source = thumbnail.Image, Stretch = Stretch.Uniform };
            return;
        }

        card.Child = null;
        card.Background = new ImageBrush(thumbnail.Image) { Stretch = Stretch.UniformToFill };
    }

    private FrameworkElement Fallback(string path, double size)
    {
        var icon = Directory.Exists(path) ? "show-in-folder"
            : KnownFormats.FindByExtension(Path.GetExtension(path)) is { } format ? Icons.ForCategory(format.Category)
            : "document";
        var tile = DialogLook.Icon(icon, size * 0.45, _look.Muted);
        tile.HorizontalAlignment = HorizontalAlignment.Center;
        tile.VerticalAlignment = VerticalAlignment.Center;
        return tile;
    }

    /// <summary>La miniatura se pide fuera del hilo de la interfaz (un video puede tardar) y se redibuja al llegar.</summary>
    private void LoadThumbnail(string path)
    {
        if (!_shellThumbnails || _thumbnails.ContainsKey(path) || !_loading.Add(path))
        {
            return;
        }

        _ = LoadAsync(path)
            .ContinueWith(
                task => Dispatcher.InvokeAsync(() =>
                {
                    _loading.Remove(path);
                    _thumbnails[path] = task.IsCompletedSuccessfully ? task.Result : null;
                    if (!IsClosedForTests && task.IsCompletedSuccessfully && task.Result is not null)
                    {
                        Refresh();
                    }
                }),
                TaskScheduler.Default);
    }

    private static async Task<Thumbnail?> LoadAsync(string path)
        => await ShellThumbnails.GetAsync(path, ThumbnailPixels, iconOnly: false) is { } content
            ? new Thumbnail(content, IsIcon: false)
            : await ShellThumbnails.GetAsync(path, ThumbnailPixels, iconOnly: true) is { } icon ? new Thumbnail(icon, IsIcon: true) : null;

    /// <summary>Imagen de un archivo y si es el ícono de su tipo (no su contenido).</summary>
    private sealed record Thumbnail(BitmapSource Image, bool IsIcon);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}

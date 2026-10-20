// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using Kumquat.Core.Jobs;
using Kumquat.Core.Localization;
using Kumquat.Core.Text;
using Kumquat.Design;
using Kumquat.Dialogs.Look;

namespace Kumquat.Dialogs.Progress;

/// <summary>
/// Panel chico abajo a la derecha (sobre la barra de tareas) con lo que Kumquat está haciendo:
/// cada archivo con su barra, el porcentaje y cuánto falta. Nunca toma el foco ni aparece en la
/// barra de tareas; qué mostrar y cuándo lo decide <see cref="ProgressBoard"/>.
/// </summary>
public sealed partial class ProgressPanel : Window
{
    private const double PanelWidth = 360;
    private const double ScreenMargin = 12;

    private readonly DialogLook _look;
    private readonly Action<Guid> _cancel;
    private readonly TextBlock _header;
    private readonly StackPanel _rows = new();
    private readonly TextBlock _more;
    private readonly Dictionary<Guid, Row> _byJob = [];
    private bool _inCorner = true;

    /// <param name="cancel">Cancela un trabajo.</param>
    /// <param name="dismiss">"Ocultar" (hasta que termine lo actual).</param>
    public ProgressPanel(Action<Guid> cancel, Action dismiss)
        : this(cancel, dismiss, SystemTheme.AppsUseDark())
    {
    }

    internal ProgressPanel(Action<Guid> cancel, Action dismiss, bool dark)
    {
        ArgumentNullException.ThrowIfNull(dismiss);
        _cancel = cancel ?? throw new ArgumentNullException(nameof(cancel));
        _look = new DialogLook(dark, Palette.Kumquat);
        Title = Strings.ProgressPanelName;
        ThemeMode = _look.ThemeMode;
        FontFamily = DialogLook.Font;
        Resources["ContentControlThemeFontFamily"] = DialogLook.Font;
        Foreground = _look.Text;
        Width = PanelWidth;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        AutomationProperties.SetName(this, Strings.ProgressPanelName);

        _header = new TextBlock { FontSize = 12.5, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        AutomationProperties.SetLiveSetting(_header, AutomationLiveSetting.Polite);
        _more = new TextBlock { FontSize = 12, Foreground = _look.Muted, Margin = new Thickness(2, 6, 0, 0), Visibility = Visibility.Collapsed };

        var panel = new StackPanel { Margin = new Thickness(14, 10, 10, 12) };
        panel.Children.Add(Header(dismiss));
        panel.Children.Add(_rows);
        panel.Children.Add(_more);
        Content = new Border { Background = _look.WindowBackground(Tint()), Child = panel };

        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 0, GlassFrameThickness = new Thickness(-1), ResizeBorderThickness = new Thickness(0), UseAeroCaptionButtons = false });
        SourceInitialized += (_, _) =>
        {
            NoActivate();
            WindowBackdrop.TryApply(this, dark);
        };
        SizeChanged += (_, _) => PlaceInCorner();
    }

    /// <summary>Crea el panel fuera de pantalla, sin ubicarlo en la esquina (pruebas y vistas previas).</summary>
    internal static ProgressPanel CreateForTests(bool dark = true) => new(_ => { }, () => { }, dark) { _inCorner = false };

    /// <summary>Renderiza el panel a un PNG (documentación).</summary>
    internal static void RenderPreview(ProgressPanel panel, string pngPath, bool dark) => WindowPreview.Render(panel, pngPath, dark);

    /// <summary>Textos de las filas visibles (pruebas): "archivo | detalle".</summary>
    internal IReadOnlyList<string> RowTextsForTests => [.. _byJob.Values.Select(r => $"{r.Name.Text} | {r.Detail.Text}")];

    /// <summary>Encabezado (pruebas).</summary>
    internal string HeaderForTests => _header.Text;

    /// <summary>"y N más" (pruebas): vacío si no se muestra.</summary>
    internal string MoreForTests => _more.Visibility == Visibility.Visible ? _more.Text : string.Empty;

    /// <summary>Muestra, actualiza o esconde el panel según la vista.</summary>
    public void Present(ProgressView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (!view.Visible)
        {
            if (IsVisible)
            {
                Hide();
            }

            return;
        }

        Update(view);
        if (!IsVisible)
        {
            Show();
        }
    }

    /// <summary>Actualiza las filas sin rehacer las que siguen (no parpadea ni pierde el puntero).</summary>
    internal void Update(ProgressView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        _header.Text = view.Queued > 0 ? Strings.ProgressRunningQueued(view.Running, view.Queued) : Strings.ProgressRunning(view.Running);
        foreach (var gone in _byJob.Keys.Except(view.Rows.Select(r => r.Id)).ToList())
        {
            _rows.Children.Remove(_byJob[gone].Root);
            _byJob.Remove(gone);
        }

        for (var i = 0; i < view.Rows.Count; i++)
        {
            var data = view.Rows[i];
            if (!_byJob.TryGetValue(data.Id, out var row))
            {
                row = NewRow(data);
                _byJob[data.Id] = row;
            }

            // Mismo orden que la vista: los que pasan de la cola a "en curso" suben.
            if (_rows.Children.IndexOf(row.Root) != i)
            {
                _rows.Children.Remove(row.Root);
                _rows.Children.Insert(i, row.Root);
            }

            row.Set(data);
        }

        _more.Text = Strings.ProgressMore(view.Hidden);
        _more.Visibility = view.Hidden > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static partial nint GetWindowLongPtr(nint hwnd, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static partial nint SetWindowLongPtr(nint hwnd, int index, nint value);

    /// <summary>Que un clic (en "cancelar") no le quite el foco a lo que la persona estaba usando.</summary>
    private void NoActivate()
    {
        const int GwlExStyle = -20;
        const nint WsExNoActivate = 0x08000000;
        const nint WsExToolWindow = 0x00000080;
        var handle = new WindowInteropHelper(this).Handle;
        _ = SetWindowLongPtr(handle, GwlExStyle, GetWindowLongPtr(handle, GwlExStyle) | WsExNoActivate | WsExToolWindow);
    }

    /// <summary>Abajo a la derecha del área de trabajo (arriba de la barra de tareas).</summary>
    private void PlaceInCorner()
    {
        if (!_inCorner)
        {
            return;
        }

        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth - ScreenMargin;
        Top = area.Bottom - ActualHeight - ScreenMargin;
    }

    private LinearGradientBrush Tint() => new(
        _look.IsDark ? Color.FromArgb(0x40, 0x2A, 0x2A, 0x33) : Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF),
        _look.IsDark ? Color.FromArgb(0x60, 0x12, 0x12, 0x16) : Color.FromArgb(0x40, 0xF2, 0xF2, 0xF7),
        90);

    private Grid Header(Action dismiss)
    {
        var header = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(Logo());
        Grid.SetColumn(_header, 1);
        header.Children.Add(_header);
        // Con texto, no una X: la X de cada fila cancela, esta solo esconde el panel.
        var hide = DialogLook.Pill(Strings.ProgressHide, _look.Subtle, _look.Text);
        hide.Height = 24;
        hide.FontSize = 11.5;
        hide.Padding = new Thickness(10, 0, 10, 0);
        hide.Focusable = false;
        hide.Click += (_, _) => dismiss();
        Grid.SetColumn(hide, 2);
        header.Children.Add(hide);
        return header;
    }

    private static FrameworkElement Logo()
    {
        if (!File.Exists(DesignAssets.LogoPng))
        {
            return new Border { Width = 18 };
        }

        var image = new System.Windows.Media.Imaging.BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(DesignAssets.LogoPng);
        image.DecodePixelWidth = 40;
        image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        image.EndInit();
        return new System.Windows.Controls.Image { Source = image, Width = 18, Height = 18 };
    }

    private Button IconButton(string icon, string name)
    {
        var button = DialogLook.Pill(DialogLook.Icon(icon, 10, _look.Muted), Brushes.Transparent, _look.Muted);
        button.Width = 24;
        button.Height = 24;
        button.Padding = new Thickness(0);
        button.ToolTip = name;
        button.Focusable = false;
        AutomationProperties.SetName(button, name);
        return button;
    }

    private Row NewRow(ProgressRow data)
    {
        var name = new TextBlock { FontSize = 13, FontWeight = FontWeights.Medium, TextTrimming = TextTrimming.CharacterEllipsis };
        var percent = new TextBlock { FontSize = 12, Foreground = _look.Muted, Margin = new Thickness(8, 0, 0, 0) };
        var detail = new TextBlock { FontSize = 12, Foreground = _look.Muted, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 1, 0, 4) };
        var bar = new ProgressBar { Height = 4, Minimum = 0, Maximum = 1, Foreground = _look.AccentBrush };
        var cancel = IconButton("close", Strings.ProgressCancel(data.FileName));
        cancel.VerticalAlignment = VerticalAlignment.Center;
        cancel.Margin = new Thickness(6, 0, 0, 0);
        cancel.Click += (_, _) => _cancel(data.Id);

        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.Children.Add(name);
        Grid.SetColumn(percent, 1);
        top.Children.Add(percent);
        var text = new StackPanel { Children = { top, detail, bar } };

        var root = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.Children.Add(text);
        Grid.SetColumn(cancel, 1);
        root.Children.Add(cancel);
        AutomationProperties.SetName(bar, data.FileName);
        return new Row(root, name, percent, detail, bar);
    }

    private sealed record Row(Grid Root, TextBlock Name, TextBlock Percent, TextBlock Detail, ProgressBar Bar)
    {
        public void Set(ProgressRow data)
        {
            Name.Text = data.FileName;
            Name.ToolTip = data.FileName;
            if (!data.IsRunning)
            {
                Percent.Text = string.Empty;
                Detail.Text = $"{data.Description} · {Strings.ProgressQueued}";
                Bar.IsIndeterminate = false;
                Bar.Visibility = Visibility.Hidden; // una barra vacía parece trabada
                return;
            }

            Bar.Visibility = Visibility.Visible;
            Bar.IsIndeterminate = data.Progress is null;
            Bar.Value = data.Progress ?? 0;
            Percent.Text = data.Progress is { } done ? string.Create(CultureInfo.CurrentCulture, $"{Math.Floor(done * 100):0} %") : string.Empty;
            Detail.Text = $"{data.Description} · {RemainingText.Format(data.Remaining)}";
        }
    }
}

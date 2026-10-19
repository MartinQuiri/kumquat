// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Kumquat.Core.Previews;
using Kumquat.Core.Text;
using Kumquat.Dialogs.Look;

namespace Kumquat.Dialogs.Previews;

/// <summary>
/// Reproductor de audio o video con línea de tiempo. Los fotogramas y el sonido salen del propio
/// motor de Kumquat (<see cref="IMediaPlayback"/>): se ve y se oye cualquier formato que Kumquat
/// acepte, y al mover un asa o el cabezal se muestra el fotograma exacto de ese instante.
/// </summary>
internal sealed class MediaPreview : Grid, IDisposable
{
    private readonly IMediaPlayback? _playback;
    private readonly TimelineMode _mode;
    private readonly RangeTimeline _timeline;
    private readonly Image _screen = new() { Stretch = Stretch.Uniform };
    private readonly Button _play;
    private readonly TextBlock _clock;
    private readonly TextBlock _selection;
    private readonly CancellationTokenSource _life = new();
    private WriteableBitmap? _bitmap;
    private CancellationTokenSource? _playing;
    private TimeSpan? _pendingFrame;
    private bool _fetching;
    private VideoFrame? _latest;
    private int _renderQueued;

    public MediaPreview(FilePreview preview, DialogLook look, TimelineMode mode)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(look);
        _playback = preview.Playback;
        _mode = mode;
        var isVideo = preview.Kind == PreviewKind.Video;
        var strip = Load(preview.Image);
        _timeline = new RangeTimeline(strip, preview.ImageTiles, waveform: !isVideo, look, mode, preview.Duration)
        {
            SeekRequested = OnSeekRequested,
        };
        _timeline.RangeChanged += (_, _) => OnRangeChanged();
        _clock = new TextBlock { Foreground = look.Text, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        _selection = new TextBlock { Foreground = look.Muted, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        _play = PlayButton(look);
        _play.Visibility = _playback is null ? Visibility.Collapsed : Visibility.Visible;
        System.Windows.Documents.Typography.SetNumeralAlignment(this, FontNumeralAlignment.Tabular);

        Margin = new Thickness(18);
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = isVideo ? new GridLength(64) : GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        if (isVideo)
        {
            Add(Screen(strip, preview.ImageTiles), 0);
            _timeline.Margin = new Thickness(0, 14, 0, 0);
            Add(_timeline, 1);
        }
        else
        {
            _timeline.Margin = new Thickness(0, 20, 0, 20);
            Add(_timeline, 0);
        }

        Add(Transport(), 2);
        Loaded += (_, _) => RequestFrame(_timeline.Position);
        Unloaded += (_, _) => Close();
        UpdateLabels();
    }

    /// <summary>El usuario cambió el tramo con las asas.</summary>
    public event EventHandler? RangeChanged;

    /// <summary>El usuario eligió otro instante (modo <see cref="TimelineMode.Point"/>).</summary>
    public event EventHandler? PointChanged;

    public TimeSpan Start => _timeline.Start;

    public TimeSpan End => _timeline.End;

    /// <summary>Instante elegido o en reproducción.</summary>
    public TimeSpan Point => _timeline.Position;

    /// <summary>Botón reproducir/pausa (pruebas).</summary>
    internal Button PlayButtonForTests => _play;

    /// <summary>Imagen que se muestra (pruebas).</summary>
    internal ImageSource? ScreenForTests => _screen.Source;

    /// <summary>Simula que el usuario eligió un instante en la línea de tiempo (pruebas).</summary>
    internal void SeekForTests(TimeSpan time)
    {
        _timeline.SetPosition(time);
        OnSeekRequested(_timeline.Position);
    }

    /// <summary>Fija el tramo desde los campos de texto.</summary>
    public void SetRange(TimeSpan start, TimeSpan end)
    {
        _timeline.SetRange(start, end);
        _timeline.SetPosition(_timeline.Start);
        RequestFrame(_timeline.Start);
        UpdateLabels();
    }

    /// <summary>Fija el instante desde el campo de texto.</summary>
    public void SetPoint(TimeSpan time)
    {
        _timeline.SetPosition(time);
        RequestFrame(_timeline.Position);
        UpdateLabels();
    }

    /// <summary>Detiene la reproducción y la lectura de fotogramas.</summary>
    public void Close()
    {
        if (!_life.IsCancellationRequested)
        {
            _life.Cancel();
        }

        Interlocked.Exchange(ref _latest, null)?.Dispose();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Close();
        _life.Dispose();
    }

    private static BitmapImage? Load(string? path)
    {
        if (path is null)
        {
            return null;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UriFormatException)
        {
            return null;
        }
    }

    private void Add(UIElement element, int row)
    {
        SetRow(element, row);
        Children.Add(element);
    }

    private Border Screen(BitmapSource? strip, int tiles)
    {
        // Hasta que llega el primer fotograma exacto, el primero de la tira.
        if (strip is not null && strip.PixelWidth / Math.Max(1, tiles) > 0)
        {
            var first = new CroppedBitmap(strip, new Int32Rect(0, 0, strip.PixelWidth / Math.Max(1, tiles), strip.PixelHeight));
            first.Freeze();
            _screen.Source = first;
        }

        RenderOptions.SetBitmapScalingMode(_screen, BitmapScalingMode.HighQuality);
        var stage = new Grid { Children = { _screen } };
        stage.SizeChanged += (_, e) => stage.Clip = new RectangleGeometry(new Rect(e.NewSize), 14, 14);
        return new Border { Background = Brushes.Black, CornerRadius = new CornerRadius(14), Child = stage, Effect = PagesPreview.CardShadow() };
    }

    private Grid Transport()
    {
        var row = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Children.Add(_play);
        SetColumn(_clock, 1);
        row.Children.Add(_clock);
        SetColumn(_selection, 2);
        row.Children.Add(_selection);
        return row;
    }

    private Button PlayButton(DialogLook look)
    {
        var button = DialogLook.Pill(DialogLook.Icon("play", 16, Brushes.White), look.AccentGradient, Brushes.White);
        button.Width = 40;
        button.Padding = new Thickness(0);
        System.Windows.Automation.AutomationProperties.SetName(button, "Reproducir");
        button.Click += (_, _) => TogglePlay();
        return button;
    }

    private void TogglePlay()
    {
        if (_playing is not null)
        {
            _playing.Cancel();
            return;
        }

        _ = PlayAsync();
    }

    private async Task PlayAsync()
    {
        // Recortar: suena el tramo elegido. Si no, desde el cabezal (o desde el inicio si está al final).
        var nearEnd = TimeSpan.FromMilliseconds(100);
        var to = _mode == TimelineMode.Range ? _timeline.End : _timeline.Duration;
        var from = _mode == TimelineMode.Range
            ? (_timeline.Position >= _timeline.Start && _timeline.Position < to - nearEnd ? _timeline.Position : _timeline.Start)
            : (_timeline.Position < to - nearEnd ? _timeline.Position : TimeSpan.Zero);
        using var playing = CancellationTokenSource.CreateLinkedTokenSource(_life.Token);
        _playing = playing;
        _play.Content = DialogLook.Icon("pause", 16, Brushes.White);
        System.Windows.Automation.AutomationProperties.SetName(_play, "Pausa");
        try
        {
            await Task.Run(() => _playback!.PlayAsync(from, to, OnFrame, OnPosition, playing.Token), playing.Token);
        }
        catch (OperationCanceledException)
        {
            // Pausa o ventana cerrada.
        }
#pragma warning disable CA1031 // Límite de una tarea de UI: se registra y la ventana vuelve a "reproducir".
        catch (Exception ex) when (DialogDiagnostics.IsRecoverable(ex))
#pragma warning restore CA1031
        {
            DialogDiagnostics.Report("reproducir la vista previa", ex);
        }
        finally
        {
            _playing = null;
        }

        if (_life.IsCancellationRequested)
        {
            return;
        }

        _play.Content = DialogLook.Icon("play", 16, Brushes.White);
        System.Windows.Automation.AutomationProperties.SetName(_play, "Reproducir");
        if (_mode == TimelineMode.Point)
        {
            // Pausar en un momento elige ese fotograma.
            RequestFrame(_timeline.Position);
            PointChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Llega desde el hilo del reproductor: se muestra solo el más reciente.</summary>
    private void OnFrame(VideoFrame frame)
    {
        Interlocked.Exchange(ref _latest, frame)?.Dispose();
        if (Interlocked.Exchange(ref _renderQueued, 1) == 0)
        {
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Render, RenderLatest);
        }
    }

    private void RenderLatest()
    {
        Interlocked.Exchange(ref _renderQueued, 0);
        using var frame = Interlocked.Exchange(ref _latest, null);
        if (frame is not null && !_life.IsCancellationRequested)
        {
            Show(frame);
        }
    }

    private void OnPosition(TimeSpan position)
        => _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            if (_playing is not null)
            {
                _timeline.SetPosition(position);
                UpdateLabels();
            }
        });

    private void OnSeekRequested(TimeSpan time)
    {
        _playing?.Cancel();
        RequestFrame(time);
        UpdateLabels();
        if (_mode == TimelineMode.Point)
        {
            PointChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnRangeChanged()
    {
        UpdateLabels();
        RangeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Pide el fotograma exacto de un instante. Mientras se arrastra llegan muchos pedidos:
    /// solo se atiende el último (nunca hay más de un FFmpeg a la vez).
    /// </summary>
    private void RequestFrame(TimeSpan time)
    {
        if (_playback is null || _playback.FrameSize.Width == 0)
        {
            return;
        }

        _pendingFrame = time;
        if (!_fetching)
        {
            _ = FetchFramesAsync();
        }
    }

    private async Task FetchFramesAsync()
    {
        _fetching = true;
        var token = _life.Token; // se copia: el token sigue válido aunque la fuente se deseche al cerrar
        try
        {
            while (_pendingFrame is { } time && !token.IsCancellationRequested)
            {
                _pendingFrame = null;
                using var frame = await Task.Run(() => _playback!.FrameAtAsync(time, token), token);
                if (frame is not null && _playing is null)
                {
                    Show(frame);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Ventana cerrada.
        }
#pragma warning disable CA1031 // Límite de una tarea de UI: se registra; queda el último fotograma mostrado.
        catch (Exception ex) when (DialogDiagnostics.IsRecoverable(ex))
#pragma warning restore CA1031
        {
            DialogDiagnostics.Report("leer un fotograma", ex);
        }
        finally
        {
            _fetching = false;
        }
    }

    private unsafe void Show(VideoFrame frame)
    {
        if (_bitmap is null || _bitmap.PixelWidth != frame.Width || _bitmap.PixelHeight != frame.Height)
        {
            _bitmap = new WriteableBitmap(frame.Width, frame.Height, 96, 96, PixelFormats.Bgra32, null);
            _screen.Source = _bitmap;
        }

        fixed (byte* pixels = frame.Pixels)
        {
            _bitmap.WritePixels(new Int32Rect(0, 0, frame.Width, frame.Height), (nint)pixels, frame.Stride * frame.Height, frame.Stride);
        }
    }

    private void UpdateLabels()
    {
        _clock.Text = $"{TimeText.Format(_timeline.Position)} / {TimeText.Format(_timeline.Duration)}";
        _selection.Text = _mode switch
        {
            TimelineMode.Range => $"Tramo: {TimeText.Format(_timeline.Start)} – {TimeText.Format(_timeline.End)}  ·  {TimeText.Format(_timeline.End - _timeline.Start)}",
            TimelineMode.Point => $"Fotograma en {TimeText.Format(_timeline.Position)}",
            _ => string.Empty,
        };
    }
}

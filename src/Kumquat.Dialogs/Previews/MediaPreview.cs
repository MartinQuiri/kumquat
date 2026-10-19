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
/// Reproductor de audio o video con línea de tiempo. El video muestra el fotograma del instante
/// elegido al mover las asas; reproducir respeta el tramo elegido. La reproducción usa Media
/// Foundation de Windows: si un formato no se puede reproducir, la línea de tiempo sigue sirviendo.
/// </summary>
internal sealed class MediaPreview : Grid
{
    private readonly MediaElement _media;
    private readonly RangeTimeline _timeline;
    private readonly Button _play;
    private readonly TextBlock _clock;
    private readonly TextBlock _selection;
    private readonly DispatcherTimer _timer;
    private readonly DialogLook _look;
    private readonly bool _editable;
    private bool _playing;

    public MediaPreview(FilePreview preview, DialogLook look, bool editable)
    {
        ArgumentNullException.ThrowIfNull(preview);
        _look = look;
        _editable = editable;
        var isVideo = preview.Kind == PreviewKind.Video;
        var image = Load(preview.Image);
        _media = new MediaElement { LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Close, ScrubbingEnabled = true, Stretch = Stretch.Uniform, Source = new Uri(preview.SourcePath) };
        _media.MediaOpened += (_, _) => Seek(_timeline!.Start);
        _media.MediaFailed += (_, _) => _play!.Visibility = Visibility.Collapsed;
        _timeline = new RangeTimeline(image, preview.ImageTiles, waveform: !isVideo, look, editable, preview.Duration);
        _timeline.SeekRequested = Seek;
        _timeline.RangeChanged += (_, _) => OnRangeChanged();
        _clock = new TextBlock { Foreground = look.Text, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        _selection = new TextBlock { Foreground = look.Muted, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        _play = PlayButton();
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(40), DispatcherPriority.Render, (_, _) => Tick(), Dispatcher);
        System.Windows.Documents.Typography.SetNumeralAlignment(this, FontNumeralAlignment.Tabular);

        Margin = new Thickness(18);
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = isVideo ? new GridLength(64) : GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        if (isVideo)
        {
            Add(Screen(image, preview.ImageTiles), 0);
            _timeline.Margin = new Thickness(0, 14, 0, 0);
            Add(_timeline, 1);
        }
        else
        {
            _timeline.Margin = new Thickness(0, 20, 0, 20);
            Add(_timeline, 0);
            Children.Add(new Border { Width = 0, Height = 0, Child = _media }); // solo audio: invisible
        }

        Add(Transport(), 2);
        Loaded += (_, _) => _media.Pause(); // abre el medio y muestra el primer fotograma
        Unloaded += (_, _) => Close();
        UpdateLabels();
    }

    /// <summary>El usuario cambió el tramo con las asas.</summary>
    public event EventHandler? RangeChanged;

    public TimeSpan Start => _timeline.Start;

    public TimeSpan End => _timeline.End;

    public TimeSpan Duration => _timeline.Duration;

    /// <summary>Fija el tramo desde los campos de texto.</summary>
    public void SetRange(TimeSpan start, TimeSpan end)
    {
        _timeline.SetRange(start, end);
        Seek(_timeline.Start);
        UpdateLabels();
    }

    /// <summary>Detiene la reproducción y suelta el archivo.</summary>
    public void Close()
    {
        _timer.Stop();
        _media.Stop();
        _media.Close();
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
        var stage = new Grid();

        // Mientras abre el reproductor (o si no puede), el primer fotograma de la tira.
        if (strip is not null && strip.PixelWidth / Math.Max(1, tiles) > 0)
        {
            var first = new CroppedBitmap(strip, new Int32Rect(0, 0, strip.PixelWidth / Math.Max(1, tiles), strip.PixelHeight));
            first.Freeze();
            stage.Children.Add(new Image { Source = first, Stretch = Stretch.Uniform });
        }

        stage.Children.Add(_media);
        var screen = new Border { Background = Brushes.Black, CornerRadius = new CornerRadius(14), Child = stage, Effect = PagesPreview.CardShadow() };
        stage.SizeChanged += (_, e) => stage.Clip = new RectangleGeometry(new Rect(e.NewSize), 14, 14);
        return screen;
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

    private Button PlayButton()
    {
        var button = DialogLook.Pill(DialogLook.Icon("play", 16, Brushes.White), _look.AccentGradient, Brushes.White);
        button.Width = 40;
        button.Padding = new Thickness(0);
        System.Windows.Automation.AutomationProperties.SetName(button, "Reproducir");
        button.Click += (_, _) => TogglePlay();
        return button;
    }

    private void TogglePlay()
    {
        if (_playing)
        {
            Pause();
            return;
        }

        // Si el cabezal está fuera del tramo (o al final), empieza desde el inicio del tramo.
        var position = _media.Position;
        if (position < _timeline.Start || position >= _timeline.End - TimeSpan.FromMilliseconds(80))
        {
            _media.Position = _timeline.Start;
        }

        _media.Play();
        _playing = true;
        _play.Content = DialogLook.Icon("pause", 16, Brushes.White);
        _timer.Start();
    }

    private void Pause()
    {
        _media.Pause();
        _playing = false;
        _play.Content = DialogLook.Icon("play", 16, Brushes.White);
        _timer.Stop();
        UpdateLabels();
    }

    private void Tick()
    {
        var position = _media.Position;
        if (_editable && position >= _timeline.End)
        {
            Pause();
            position = _timeline.End;
        }

        _timeline.SetPosition(position);
        UpdateLabels();
    }

    private void Seek(TimeSpan time)
    {
        if (_playing)
        {
            Pause();
        }

        _media.Position = time;
        _timeline.SetPosition(time);
        UpdateLabels();
    }

    private void OnRangeChanged()
    {
        UpdateLabels();
        RangeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateLabels()
    {
        _clock.Text = $"{TimeText.Format(_timeline.Position)} / {TimeText.Format(_timeline.Duration)}";
        _selection.Text = _editable ? $"Tramo: {TimeText.Format(_timeline.Start)} – {TimeText.Format(_timeline.End)}  ·  {TimeText.Format(_timeline.End - _timeline.Start)}" : string.Empty;
    }
}

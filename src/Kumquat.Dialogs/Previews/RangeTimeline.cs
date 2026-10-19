// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Kumquat.Dialogs.Look;

namespace Kumquat.Dialogs.Previews;

/// <summary>
/// Línea de tiempo con tira de fotogramas (video) o forma de onda (audio), cabezal de
/// reproducción y, al recortar, un marco con dos asas para elegir el tramo (como en Fotos de iOS).
/// </summary>
internal sealed class RangeTimeline : Grid
{
    private const double HandleWidth = 14;
    private static readonly TimeSpan MinimumRange = TimeSpan.FromMilliseconds(100);

    private readonly TimelineMode _mode;
    private readonly Canvas _overlay = new() { IsHitTestVisible = false };
    private readonly Rectangle _dimLeft;
    private readonly Rectangle _dimRight;
    private readonly Border _frame;
    private readonly Border _startHandle;
    private readonly Border _endHandle;
    private readonly Rectangle _playhead;
    private readonly FrameworkElement? _waveSelected;
    private DragTarget _drag;

    public RangeTimeline(BitmapSource? image, int tiles, bool waveform, DialogLook look, TimelineMode mode, TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(look);
        _mode = mode;
        var editable = mode == TimelineMode.Range;
        Duration = duration > TimeSpan.Zero ? duration : TimeSpan.FromSeconds(1);
        End = Duration;
        Background = Brushes.Transparent; // recibe clics en toda el área
        Cursor = Cursors.Hand;

        var surface = new Grid { Margin = new Thickness(HandleWidth, 0, HandleWidth, 0) };
        surface.SizeChanged += (_, e) => surface.Clip = new RectangleGeometry(new Rect(e.NewSize), 9, 9);
        surface.Children.Add(new Border { Background = look.Subtle });
        if (waveform && image is not null)
        {
            surface.Children.Add(Wave(image, look.Muted));
            _waveSelected = Wave(image, look.AccentGradient);
            surface.Children.Add(_waveSelected);
        }
        else if (image is not null)
        {
            surface.Children.Add(Filmstrip(image, Math.Max(1, tiles)));
        }

        Children.Add(surface);
        Children.Add(_overlay);

        var dim = DialogLook.Brush(Color.FromArgb(0x99, 0, 0, 0));
        _dimLeft = new Rectangle { Fill = dim };
        _dimRight = new Rectangle { Fill = dim };
        _frame = new Border { BorderBrush = look.AccentBrush, BorderThickness = new Thickness(0, 3, 0, 3) };
        _startHandle = Handle(look, new CornerRadius(8, 0, 0, 8));
        _endHandle = Handle(look, new CornerRadius(0, 8, 8, 0));
        // En modo instante el cabezal es la elección: más grueso y del color de la herramienta.
        _playhead = new Rectangle { Width = 3, RadiusX = 1.5, RadiusY = 1.5, Fill = mode == TimelineMode.Point ? look.AccentBrush : Brushes.White, Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 4, ShadowDepth = 0, Opacity = 0.6 } };
        if (editable)
        {
            if (!waveform)
            {
                _overlay.Children.Add(_dimLeft);
                _overlay.Children.Add(_dimRight);
            }

            _overlay.Children.Add(_frame);
            _overlay.Children.Add(_startHandle);
            _overlay.Children.Add(_endHandle);
        }

        _overlay.Children.Add(_playhead);
        SizeChanged += (_, _) => Arrange();
    }

    private enum DragTarget
    {
        None,
        Start,
        End,
        Seek,
    }

    /// <summary>El usuario movió un asa del tramo.</summary>
    public event EventHandler? RangeChanged;

    /// <summary>Se llama cuando el usuario pide ver un instante (clic, arrastre del cabezal o de un asa).</summary>
    public Action<TimeSpan>? SeekRequested { get; set; }

    public TimeSpan Duration { get; }

    public TimeSpan Start { get; private set; }

    public TimeSpan End { get; private set; }

    public TimeSpan Position { get; private set; }

    /// <summary>Fija el tramo (desde los campos de texto: no dispara <see cref="RangeChanged"/>).</summary>
    public void SetRange(TimeSpan start, TimeSpan end)
    {
        End = Clamp(end, MinimumRange, Duration);
        Start = Clamp(start, TimeSpan.Zero, End - MinimumRange);
        Arrange();
    }

    /// <summary>Mueve el cabezal.</summary>
    public void SetPosition(TimeSpan position)
    {
        Position = Clamp(position, TimeSpan.Zero, Duration);
        Arrange();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseLeftButtonDown(e);
        var x = e.GetPosition(this).X;
        _drag = _mode != TimelineMode.Range ? DragTarget.Seek
            : Math.Abs(x - (X(Start) - (HandleWidth / 2))) <= HandleWidth ? DragTarget.Start
            : Math.Abs(x - (X(End) + (HandleWidth / 2))) <= HandleWidth ? DragTarget.End
            : DragTarget.Seek;
        CaptureMouse();
        DragTo(x);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseMove(e);
        if (_drag != DragTarget.None && IsMouseCaptured)
        {
            DragTo(e.GetPosition(this).X);
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        _drag = DragTarget.None;
        ReleaseMouseCapture();
    }

    private static Rectangle Wave(BitmapSource image, Brush fill) => new()
    {
        Fill = fill,
        OpacityMask = new ImageBrush(image) { Stretch = Stretch.Fill },
        Margin = new Thickness(6, 8, 6, 8),
    };

    private static UniformGrid Filmstrip(BitmapSource strip, int tiles)
    {
        var grid = new UniformGrid { Rows = 1, Columns = tiles };
        var width = strip.PixelWidth / tiles;
        for (var i = 0; i < tiles && width > 0; i++)
        {
            var frame = new CroppedBitmap(strip, new Int32Rect(i * width, 0, width, strip.PixelHeight));
            frame.Freeze();
            grid.Children.Add(new Image { Source = frame, Stretch = Stretch.UniformToFill });
        }

        return grid;
    }

    private static Border Handle(DialogLook look, CornerRadius corners) => new()
    {
        Width = HandleWidth,
        Background = look.AccentBrush,
        CornerRadius = corners,
        Child = new Rectangle { Width = 3, Height = 16, RadiusX = 1.5, RadiusY = 1.5, Fill = Brushes.White, Opacity = 0.9 },
    };

    private static TimeSpan Clamp(TimeSpan value, TimeSpan min, TimeSpan max)
        => value < min ? min : value > max ? max : value;

    private double TrackWidth => Math.Max(1, ActualWidth - (2 * HandleWidth));

    private double X(TimeSpan time) => HandleWidth + (time / Duration * TrackWidth);

    private TimeSpan TimeAt(double x) => Duration * Math.Clamp((x - HandleWidth) / TrackWidth, 0, 1);

    private void DragTo(double x)
    {
        var time = TimeAt(x);
        switch (_drag)
        {
            case DragTarget.Start:
                Start = Clamp(time, TimeSpan.Zero, End - MinimumRange);
                Position = Start;
                RangeChanged?.Invoke(this, EventArgs.Empty);
                break;
            case DragTarget.End:
                End = Clamp(time, Start + MinimumRange, Duration);
                Position = End;
                RangeChanged?.Invoke(this, EventArgs.Empty);
                break;
            default:
                Position = time;
                break;
        }

        Arrange();
        SeekRequested?.Invoke(Position);
    }

    private void Arrange()
    {
        var height = ActualHeight;
        var left = X(Start);
        var right = X(End);
        Place(_dimLeft, HandleWidth, 0, left - HandleWidth, height);
        Place(_dimRight, right, 0, ActualWidth - HandleWidth - right, height);
        Place(_frame, left, 0, right - left, height);
        Place(_startHandle, left - HandleWidth, 0, HandleWidth, height);
        Place(_endHandle, right, 0, HandleWidth, height);
        var thickness = _mode == TimelineMode.Point ? 5 : 3;
        Place(_playhead, X(Position) - (thickness / 2.0), -5, thickness, height + 10);
        if (_waveSelected is not null)
        {
            // La onda dentro del tramo va en color; fuera, apagada.
            var offset = _waveSelected.Margin.Left + HandleWidth;
            _waveSelected.Clip = _mode == TimelineMode.Range ? new RectangleGeometry(new Rect(Math.Max(0, left - offset), -10, Math.Max(0, right - left), height + 20)) : null;
        }
    }

    private static void Place(FrameworkElement element, double x, double y, double width, double height)
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        element.Width = Math.Max(0, width);
        element.Height = Math.Max(0, height);
    }
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Kumquat.Dialogs.Look;

namespace Kumquat.Dialogs.Previews;

/// <summary>Tarjeta de una página: miniatura (o el número, si no se dibujó), número debajo y marca de selección.</summary>
internal sealed class PageCard : StackPanel
{
    private readonly Border _frame;
    private readonly FrameworkElement _badge;
    private readonly DialogLook _look;

    public PageCard(int page, ImageSource? image, DialogLook look, double width)
    {
        Page = page;
        _look = look;
        Margin = new Thickness(7, 6, 7, 8);
        Width = width;

        var paper = new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(5),
            MinHeight = width * 1.3,
            Effect = PagesPreview.CardShadow(),
            Child = image is null
                ? new TextBlock { Text = page.ToString(CultureInfo.CurrentCulture), FontSize = 22, Foreground = Brushes.Silver, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                : new Image { Source = image, Stretch = Stretch.Uniform },
        };
        RenderOptions.SetBitmapScalingMode(paper, BitmapScalingMode.HighQuality);

        // El marco de selección rodea al papel con un pequeño aire, como en iLovePDF.
        _frame = new Border { BorderThickness = new Thickness(2.5), CornerRadius = new CornerRadius(9), Padding = new Thickness(4), BorderBrush = Brushes.Transparent, Child = paper };
        _badge = Badge();
        var holder = new Grid();
        holder.Children.Add(_frame);
        holder.Children.Add(_badge);
        Children.Add(holder);
        Children.Add(new TextBlock { Text = page.ToString(CultureInfo.CurrentCulture), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0), FontSize = 12, Foreground = look.Muted });
        IsSelected = false;
    }

    public int Page { get; }

    public bool IsSelected
    {
        get => _badge.Visibility == Visibility.Visible;
        set
        {
            _frame.BorderBrush = value ? _look.AccentBrush : Brushes.Transparent;
            _badge.Visibility = value ? Visibility.Visible : Visibility.Hidden;
        }
    }

    private Border Badge() => new()
    {
        Width = 24,
        Height = 24,
        CornerRadius = new CornerRadius(12),
        Background = _look.AccentBrush,
        BorderBrush = Brushes.White,
        BorderThickness = new Thickness(2),
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Top,
        Margin = new Thickness(0, -4, -4, 0),
        Child = DialogLook.Icon("check", 12, Brushes.White),
    };
}

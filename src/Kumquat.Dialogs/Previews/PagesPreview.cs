// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Effects;
using Kumquat.Core.Previews;
using Kumquat.Dialogs.Look;

namespace Kumquat.Dialogs.Previews;

/// <summary>
/// Cuadrícula de páginas de un PDF (como iLovePDF). En modo selección, un clic marca o desmarca
/// la página; la selección se sincroniza con el campo "Páginas" de la ventana.
/// </summary>
internal sealed class PagesPreview : DockPanel
{
    private const double CardWidth = 118;

    private readonly DialogLook _look;
    private readonly bool _selectable;
    private readonly List<PageCard> _cards = [];
    private readonly SortedSet<int> _selected = [];
    private readonly TextBlock _summary;

    public PagesPreview(FilePreview preview, DialogLook look, bool selectable)
    {
        ArgumentNullException.ThrowIfNull(preview);
        _look = look;
        _selectable = selectable;
        PageCount = preview.PageCount;

        _summary = new TextBlock { Foreground = look.Muted, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center };
        var bar = BuildBar();
        SetDock(bar, Dock.Top);
        Children.Add(bar);

        var grid = new WrapPanel { Margin = new Thickness(6, 4, 6, 12) };
        for (var page = 1; page <= preview.PageCount; page++)
        {
            var image = page <= preview.Pages.Count ? Thumbnail(preview.Pages[page - 1]) : null;
            var card = new PageCard(page, image, look, CardWidth);
            if (selectable)
            {
                card.Cursor = Cursors.Hand;
                card.MouseLeftButtonUp += (_, _) => Toggle(card.Page);
            }

            _cards.Add(card);
            grid.Children.Add(card);
        }

        Children.Add(new ScrollViewer { Content = grid, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(8, 0, 8, 0) });
        UpdateSummary();
    }

    /// <summary>El usuario cambió la selección con el mouse.</summary>
    public event EventHandler? SelectionChanged;

    public int PageCount { get; }

    /// <summary>Páginas marcadas (base 1).</summary>
    public IReadOnlyCollection<int> Selected => _selected;

    /// <summary>Marca exactamente estas páginas (desde el campo de texto; no dispara el evento).</summary>
    public void Select(IEnumerable<int> pages)
    {
        _selected.Clear();
        _selected.UnionWith(pages.Where(p => p >= 1 && p <= PageCount));
        Refresh();
    }

    private static System.Windows.Media.Imaging.BitmapImage? Thumbnail(string path)
    {
        try
        {
            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; // no deja el archivo abierto
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

    private Grid BuildBar()
    {
        var bar = new Grid { Margin = new Thickness(14, 12, 14, 8) };
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new StackPanel();
        title.Children.Add(new TextBlock
        {
            Text = PageCount == 1 ? "1 página" : string.Create(CultureInfo.CurrentCulture, $"{PageCount} páginas"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 15,
            Foreground = _look.Text,
        });
        title.Children.Add(_summary);
        bar.Children.Add(title);
        if (_selectable)
        {
            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            actions.Children.Add(SmallButton("Todas", () => SetAll(true)));
            actions.Children.Add(SmallButton("Ninguna", () => SetAll(false)));
            Grid.SetColumn(actions, 1);
            bar.Children.Add(actions);
        }

        return bar;
    }

    private Button SmallButton(string text, Action onClick)
    {
        var button = DialogLook.Pill(text, _look.Subtle, _look.Text);
        button.Height = 30;
        button.Padding = new Thickness(14, 0, 14, 0);
        button.FontSize = 12.5;
        button.Margin = new Thickness(6, 0, 0, 0);
        button.Click += (_, _) => onClick();
        return button;
    }

    private void SetAll(bool selected)
    {
        _selected.Clear();
        if (selected)
        {
            _selected.UnionWith(Enumerable.Range(1, PageCount));
        }

        Refresh();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Toggle(int page)
    {
        if (!_selected.Remove(page))
        {
            _selected.Add(page);
        }

        Refresh();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Refresh()
    {
        foreach (var card in _cards)
        {
            card.IsSelected = _selected.Contains(card.Page);
        }

        UpdateSummary();
    }

    private void UpdateSummary()
    {
        _summary.Text = !_selectable ? "Vista previa"
            : _selected.Count == 0 ? "Haz clic en las páginas que quieres extraer"
            : _selected.Count == 1 ? "1 página elegida"
            : string.Create(CultureInfo.CurrentCulture, $"{_selected.Count} páginas elegidas");
    }

    /// <summary>Sombra suave de las tarjetas de página.</summary>
    internal static DropShadowEffect CardShadow() => new() { BlurRadius = 12, ShadowDepth = 2, Opacity = 0.22, Direction = 270 };
}

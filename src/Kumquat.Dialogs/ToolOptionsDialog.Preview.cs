// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kumquat.Core.Previews;
using Kumquat.Core.Text;
using Kumquat.Core.Tools;
using Kumquat.Design;
using Kumquat.Dialogs.Previews;

namespace Kumquat.Dialogs;

/// <summary>
/// Panel de vista previa: se genera en segundo plano y queda sincronizado con los campos
/// (páginas elegidas ⇄ "Páginas", tramo ⇄ "Desde"/"Hasta").
/// </summary>
public sealed partial class ToolOptionsDialog
{
    private readonly CancellationTokenSource _previewCancel = new();
    private FilePreview? _preview;
    private MediaPreview? _media;
    private bool _syncing;

    private void OnLoaded()
    {
        BringToFront();
        if (_request.Preview is not null && _preview is null)
        {
            _ = LoadPreviewAsync(_request.Preview);
        }
    }

    private void OnClosed()
    {
        _previewCancel.Cancel();
        _media?.Close();
        _preview?.Dispose();
        _previewCancel.Dispose();
    }

    private async Task LoadPreviewAsync(Func<CancellationToken, Task<FilePreview>> create)
    {
        ShowStatus(loading: true);
        var token = _previewCancel.Token;
        FilePreview preview;
        try
        {
            preview = await Task.Run(() => create(token), token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested)
        {
            preview.Dispose();
            return;
        }

        ShowPreview(preview);
    }

    /// <summary>Muestra una vista previa ya generada (la ventana pasa a ser su dueña).</summary>
    internal void ShowPreview(FilePreview preview)
    {
        _preview?.Dispose();
        _preview = preview;
        if (_previewHost is null)
        {
            return;
        }

        switch (preview.Kind)
        {
            case PreviewKind.Pages:
                _previewHost.Child = Pages(preview);
                break;
            case PreviewKind.Video or PreviewKind.Audio:
                _previewHost.Child = Media(preview);
                break;
            case PreviewKind.Image when preview.Image is not null:
                _previewHost.Child = Photo(preview.Image);
                break;
            default:
                ShowStatus(loading: false);
                break;
        }
    }

    private PagesPreview Pages(FilePreview preview)
    {
        var box = _parameters.FirstOrDefault(p => p.Kind == ToolParameterKind.Pages) is { } p ? _boxes.GetValueOrDefault(p.Key) : null;
        var pages = new PagesPreview(preview, _look, selectable: box is not null);
        if (box is null)
        {
            return pages;
        }

        void FromText()
        {
            if (!_syncing && PageRanges.TryNormalize(box.Text, out var normalized))
            {
                pages.Select(PageRanges.Expand(normalized, pages.PageCount));
            }
        }

        FromText();
        box.TextChanged += (_, _) => FromText();
        pages.SelectionChanged += (_, _) => Sync(() => box.Text = PageRanges.Describe(pages.Selected));
        return pages;
    }

    private MediaPreview Media(FilePreview preview)
    {
        var start = _boxes.GetValueOrDefault("start");
        var end = _boxes.GetValueOrDefault("end");
        var editable = start is not null && end is not null && _parameters.Any(p => p.Kind == ToolParameterKind.Time);
        var media = new MediaPreview(preview, _look, editable);
        _media = media;
        if (!editable)
        {
            return media;
        }

        void FromText()
        {
            if (!_syncing && TimeText.TryParse(start!.Text, out var from) && TimeText.TryParse(end!.Text, out var to) && to > from)
            {
                media.SetRange(from, to);
            }
        }

        FromText();
        start!.TextChanged += (_, _) => FromText();
        end!.TextChanged += (_, _) => FromText();
        media.RangeChanged += (_, _) => Sync(() =>
        {
            start.Text = TimeText.Format(media.Start);
            end.Text = TimeText.Format(media.End);
        });
        return media;
    }

    private static Grid Photo(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path);
        bitmap.EndInit();
        bitmap.Freeze();
        var image = new Image { Source = bitmap, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
        image.SizeChanged += (_, e) => image.Clip = new RectangleGeometry(new Rect(e.NewSize), 10, 10);
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        var shadow = new Border { Child = image, Effect = PagesPreview.CardShadow(), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        return new Grid { Margin = new Thickness(28), Children = { shadow } };
    }

    /// <summary>Mientras se genera, una barra; si no hay vista previa, el ícono grande de la herramienta.</summary>
    private void ShowStatus(bool loading)
    {
        if (_previewHost is null)
        {
            return;
        }

        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        if (loading)
        {
            panel.Children.Add(new ProgressBar { IsIndeterminate = true, Width = 160, Margin = new Thickness(0, 0, 0, 14) });
        }
        else
        {
            var tile = _look.IconTile(Icons.ForItem(_request.ToolId), 84);
            tile.Margin = new Thickness(0, 0, 0, 16);
            panel.Children.Add(tile);
        }

        panel.Children.Add(new TextBlock { Text = loading ? "Preparando vista previa…" : "Sin vista previa para este archivo", Foreground = _look.Muted, FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center });
        _previewHost.Child = panel;
    }

    /// <summary>Actualiza un campo desde la vista previa sin que el campo vuelva a actualizar la vista previa.</summary>
    private void Sync(Action update)
    {
        _syncing = true;
        try
        {
            update();
        }
        finally
        {
            _syncing = false;
        }
    }
}

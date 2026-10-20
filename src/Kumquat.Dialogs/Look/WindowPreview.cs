// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Windows;
using System.Windows.Media;

namespace Kumquat.Dialogs.Look;

/// <summary>Imágenes de las ventanas para docs/images (las generan los tests).</summary>
internal static class WindowPreview
{
    /// <summary>Renderiza la ventana a un PNG (documentación); el acrílico se simula con un degradado.</summary>
    public static void Render(Window dialog, string pngPath, bool dark)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        dialog.Left = -20000;
        dialog.Top = -20000;
        dialog.ShowActivated = false;
        dialog.Show();
        dialog.UpdateLayout();
        var content = (FrameworkElement)dialog.Content;
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
            context.DrawRectangle(backdrop, null, new Rect(size));
            context.DrawRectangle(new VisualBrush(content), null, new Rect(size));
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
}

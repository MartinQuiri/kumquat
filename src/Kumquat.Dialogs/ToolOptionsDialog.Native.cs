// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kumquat.Core.Previews;

namespace Kumquat.Dialogs;

/// <summary>Traer al frente y captura para la documentación.</summary>
public sealed partial class ToolOptionsDialog
{
    /// <summary>
    /// Renderiza la ventana fuera de pantalla a un PNG (vista previa para la documentación). El
    /// material acrílico no existe fuera de la pantalla: se simula con un fondo de colores suaves.
    /// </summary>
    internal static void RenderPreview(ToolDialogRequest request, FilePreview? preview, string pngPath, bool dark)
    {
        var dialog = new ToolOptionsDialog(request, dark) { Left = -20000, Top = -20000, ShowActivated = false };
        dialog.Show();
        if (preview is not null)
        {
            dialog.ShowPreview(preview);
        }

        dialog.UpdateLayout();
        var content = (FrameworkElement)dialog.Content;
        var size = new Size(content.ActualWidth, content.ActualHeight);
        const double scale = 2.0;
        var bitmap = new RenderTargetBitmap((int)(size.Width * scale), (int)(size.Height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(SimulatedAcrylic(dark), null, new Rect(size));
            context.DrawRectangle(new VisualBrush(content), null, new Rect(size));
        }

        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var file = File.Create(pngPath))
        {
            encoder.Save(file);
        }

        dialog.Close();
    }

    private static LinearGradientBrush SimulatedAcrylic(bool dark)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop(dark ? Color.FromRgb(0x2B, 0x2A, 0x38) : Color.FromRgb(0xF4, 0xEE, 0xF2), 0),
                new GradientStop(dark ? Color.FromRgb(0x1E, 0x24, 0x2E) : Color.FromRgb(0xE9, 0xF0, 0xF6), 0.55),
                new GradientStop(dark ? Color.FromRgb(0x2A, 0x22, 0x26) : Color.FromRgb(0xF6, 0xEF, 0xE6), 1),
            },
        };
        brush.Freeze();
        return brush;
    }

    /// <summary>
    /// Tras un drop el que está al frente es el Explorador: sin esto la ventana se abriría detrás.
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

        MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
    }

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

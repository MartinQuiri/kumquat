// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Kumquat.Dialogs.Look;

/// <summary>
/// Material acrílico de Windows 11 (desenfoca lo que hay detrás de la ventana, como el vidrio de
/// visionOS), esquinas redondeadas y barra de título del tema. En Windows 10 no hay material:
/// queda el fondo sólido del tema.
/// </summary>
internal static partial class WindowBackdrop
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmsbtTransientWindow = 3; // acrílico
    private const int DwmwcpRound = 2;

    /// <summary>Indica si el sistema admite fondos de sistema (Windows 11 22H2+).</summary>
    public static bool IsSupported => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621);

    /// <summary>Aplica el material. Llamar con la ventana ya creada (SourceInitialized o después).</summary>
    public static bool TryApply(Window window, bool dark)
    {
        if (SystemParameters.HighContrast)
        {
            return false; // En alto contraste, ventana opaca con los colores del sistema.
        }

        ArgumentNullException.ThrowIfNull(window);
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == 0)
        {
            return false;
        }

        var darkMode = dark ? 1 : 0;
        _ = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref darkMode, sizeof(int));
        var corners = DwmwcpRound;
        _ = DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref corners, sizeof(int));
        if (!IsSupported)
        {
            return false;
        }

        // El material se ve solo donde WPF no pinta: fondo de la ventana y del área cliente transparentes.
        var backdrop = DwmsbtTransientWindow;
        if (DwmSetWindowAttribute(handle, DwmwaSystemBackdropType, ref backdrop, sizeof(int)) != 0)
        {
            return false;
        }

        if (HwndSource.FromHwnd(handle) is { CompositionTarget: { } target })
        {
            target.BackgroundColor = Colors.Transparent;
        }

        var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        _ = DwmExtendFrameIntoClientArea(handle, ref margins);
        window.Background = Brushes.Transparent;
        return true;
    }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmExtendFrameIntoClientArea(nint hwnd, ref Margins margins);

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Kumquat.Interaction.Native;

namespace Kumquat.Interaction.Wheel;

/// <summary>
/// Ventana en capas, siempre encima y que nunca toma el foco. Se crea al arrancar, queda
/// visible pero estacionada fuera de pantalla y se mueve bajo el cursor cuando hace falta:
/// así su destino OLE ya está registrado cuando el arrastre llega (una ventana mostrada
/// por primera vez a mitad de un arrastre no recibe el drop).
/// </summary>
internal sealed unsafe class WheelWindow : NativeWindow
{
    private const string ClassName = "KumquatWheel";

    private WheelTheme _theme = WheelTheme.FromSystem();
    private float _scale = 1f;
    private POINT _center;

    public WheelWindow()
    {
        Create(ClassName, Win32.WS_POPUP, Win32.WS_EX_LAYERED | Win32.WS_EX_TOPMOST | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TOOLWINDOW);
        Park();
        Win32.ShowWindow(Handle, Win32.SW_SHOWNOACTIVATE);
    }

    /// <summary>Mensajes de la app enviados a esta ventana (WM_APP + n).</summary>
    public Func<uint, nint, nint, bool>? AppMessageHandler { get; set; }

    /// <summary>Indica si la rueda está bajo el cursor.</summary>
    public bool IsShown { get; private set; }

    /// <summary>Centro de la rueda en píxeles de pantalla.</summary>
    public POINT Center => _center;

    /// <summary>Escala DPI actual (1 = 96 DPI).</summary>
    public float Scale => _scale;

    /// <summary>Geometría actual para un número de sectores.</summary>
    public WheelLayout LayoutFor(int sectors) => WheelRenderer.Layout(sectors, _scale);

    /// <summary>Centra la rueda en un punto de pantalla y la dibuja.</summary>
    public void ShowAt(POINT screenPoint, WheelVisualState state)
    {
        _center = screenPoint;
        _scale = DpiScaleAt(screenPoint);
        _theme = WheelTheme.FromSystem();
        IsShown = true;
        Render(state);
    }

    /// <summary>Redibuja en la posición actual.</summary>
    public void Render(WheelVisualState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var size = WheelRenderer.CanvasSize(_scale);
        var position = IsShown ? new POINT(_center.X - (size / 2), _center.Y - (size / 2)) : ParkingSpot(size);
        Present(size, position, state);
    }

    /// <summary>Saca la rueda de la vista sin ocultarla (sigue registrada para OLE).</summary>
    public void Park()
    {
        IsShown = false;
        var size = WheelRenderer.CanvasSize(_scale);
        Present(size, ParkingSpot(size), WheelVisualState.Waiting);
    }

    protected override nint? WndProc(uint message, nint wParam, nint lParam)
    {
        if (message >= Win32.WM_APP && AppMessageHandler?.Invoke(message, wParam, lParam) == true)
        {
            return 0;
        }

        return null;
    }

    private static POINT ParkingSpot(int size)
        => new(Win32.GetSystemMetrics(Win32.SM_XVIRTUALSCREEN) - size - 64, Win32.GetSystemMetrics(Win32.SM_YVIRTUALSCREEN) - size - 64);

    private static float DpiScaleAt(POINT point)
    {
        var monitor = Win32.MonitorFromPoint(point, Win32.MONITOR_DEFAULTTONEAREST);
        return Win32.GetDpiForMonitor(monitor, 0, out var dpiX, out _) == 0 && dpiX > 0 ? dpiX / 96f : 1f;
    }

    private void Present(int size, POINT position, WheelVisualState state)
    {
        var header = new BITMAPINFOHEADER
        {
            BiSize = (uint)sizeof(BITMAPINFOHEADER),
            BiWidth = size,
            BiHeight = -size, // top-down
            BiPlanes = 1,
            BiBitCount = 32,
        };

        var screen = Win32.GetDC(0);
        var memory = Win32.CreateCompatibleDC(screen);
        var bitmap = Win32.CreateDIBSection(screen, &header, 0, out var bits, 0, 0);
        if (bitmap == 0)
        {
            Win32.DeleteDC(memory);
            _ = Win32.ReleaseDC(0, screen);
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        var previous = Win32.SelectObject(memory, bitmap);
        try
        {
            using (var canvas = new Bitmap(size, size, size * 4, PixelFormat.Format32bppPArgb, bits))
            using (var g = Graphics.FromImage(canvas))
            {
                WheelRenderer.Render(g, size, _scale, state, _theme);
            }

            var dst = position;
            var src = new POINT(0, 0);
            var extent = new SIZE { Cx = size, Cy = size };
            var blend = new BLENDFUNCTION { BlendOp = Win32.AC_SRC_OVER, SourceConstantAlpha = 255, AlphaFormat = Win32.AC_SRC_ALPHA };
            if (!Win32.UpdateLayeredWindow(Handle, screen, &dst, &extent, memory, &src, 0, &blend, Win32.ULW_ALPHA))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            Win32.SetWindowPos(Handle, Win32.HWND_TOPMOST, position.X, position.Y, 0, 0, Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW);
        }
        finally
        {
            Win32.SelectObject(memory, previous);
            Win32.DeleteObject(bitmap);
            Win32.DeleteDC(memory);
            _ = Win32.ReleaseDC(0, screen);
        }
    }
}

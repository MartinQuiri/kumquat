// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Drawing;
using System.Drawing.Drawing2D;
using Kumquat.Design;
using Kumquat.Interaction.Native;
using Kumquat.Interaction.Wheel;

namespace Kumquat.Interaction.Tray;

/// <summary>Ícono en el área de notificación con menú y notificaciones tipo globo.</summary>
public sealed unsafe class TrayIcon : IDisposable
{
    private const uint NimAdd = 0;
    private const uint NimModify = 1;
    private const uint NimDelete = 2;
    private const uint NimSetVersion = 4;
    private const uint NifMessage = 0x01;
    private const uint NifIcon = 0x02;
    private const uint NifTip = 0x04;
    private const uint NifInfo = 0x10;
    private const uint NifShowTip = 0x80;
    private const uint NiifInfo = 0x01;
    private const uint NiifError = 0x03;
    private const uint NotifyIconVersion4 = 4;
    private const uint WmTray = Win32.WM_APP + 50;
    private const int SmCxSmIcon = 49;

    private readonly TrayWindow _window;
    private readonly nint _icon;
    private readonly Func<IReadOnlyList<TrayMenuItem>> _menu;
    private bool _added;

    /// <param name="tooltip">Texto al pasar el mouse.</param>
    /// <param name="menu">Construye el menú cada vez que se abre.</param>
    public TrayIcon(string tooltip, Func<IReadOnlyList<TrayMenuItem>> menu)
    {
        _menu = menu ?? throw new ArgumentNullException(nameof(menu));
        _window = new TrayWindow(this);
        _icon = CreateKumquatIcon();

        var data = NewData(NifMessage | NifIcon | NifTip | NifShowTip);
        data.UCallbackMessage = WmTray;
        data.HIcon = _icon;
        Copy(tooltip, data.SzTip, 128);
        _added = Win32.Shell_NotifyIconW(NimAdd, &data);
        data.UVersion = NotifyIconVersion4;
        Win32.Shell_NotifyIconW(NimSetVersion, &data);
    }

    /// <summary>Muestra una notificación.</summary>
    public void Notify(string title, string message, bool isError = false)
    {
        if (!_added)
        {
            return;
        }

        var data = NewData(NifInfo);
        Copy(title, data.SzInfoTitle, 64);
        Copy(message, data.SzInfo, 256);
        data.DwInfoFlags = isError ? NiifError : NiifInfo;
        Win32.Shell_NotifyIconW(NimModify, &data);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_added)
        {
            var data = NewData(0);
            Win32.Shell_NotifyIconW(NimDelete, &data);
            _added = false;
        }

        Win32.DestroyIcon(_icon);
        _window.Dispose();
    }

    private static void Copy(string text, char* destination, int capacity)
    {
        var span = new Span<char>(destination, capacity);
        span.Clear();
        text.AsSpan(0, Math.Min(text.Length, capacity - 1)).CopyTo(span);
    }

    /// <summary>Logo de Kumquat al tamaño de ícono pequeño del sistema; si falta el archivo, uno dibujado.</summary>
    internal static nint CreateKumquatIcon()
    {
        var side = Win32.GetSystemMetrics(SmCxSmIcon);
        var icon = File.Exists(DesignAssets.LogoIco) ? Win32.LoadImage(0, DesignAssets.LogoIco, Win32.IMAGE_ICON, side, side, Win32.LR_LOADFROMFILE) : 0;
        return icon != 0 ? icon : CreateFallbackIcon();
    }

    private static nint CreateFallbackIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var fruit = new SolidBrush(WheelTheme.Kumquat);
            g.FillEllipse(fruit, 3, 6, 26, 24);
            using var leaf = new SolidBrush(Color.FromArgb(255, 67, 160, 71));
            g.FillEllipse(leaf, 15, 1, 12, 7);
            using var shine = new SolidBrush(Color.FromArgb(110, 255, 255, 255));
            g.FillEllipse(shine, 8, 11, 7, 5);
        }

        return bitmap.GetHicon();
    }

    private NOTIFYICONDATAW NewData(uint flags)
        => new() { CbSize = (uint)sizeof(NOTIFYICONDATAW), HWnd = _window.Handle, UId = 1, UFlags = flags };

    private void ShowMenu(int x, int y)
    {
        var items = _menu();
        var menu = Win32.CreatePopupMenu();
        try
        {
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var flags = item.Text is null ? Win32.MF_SEPARATOR : Win32.MF_STRING | (item.OnClick is null ? Win32.MF_GRAYED : 0);
                Win32.AppendMenuW(menu, flags, (nuint)(i + 1), item.Text);
            }

            // Sin esto el menú no se cierra al hacer clic fuera (comportamiento documentado de Win32).
            Win32.SetForegroundWindow(_window.Handle);
            var chosen = Win32.TrackPopupMenuEx(menu, Win32.TPM_RETURNCMD | Win32.TPM_RIGHTBUTTON, x, y, _window.Handle, 0);
            if (chosen > 0 && chosen <= items.Count)
            {
                items[(int)chosen - 1].OnClick?.Invoke();
            }
        }
        finally
        {
            Win32.DestroyMenu(menu);
        }
    }

    private sealed class TrayWindow : NativeWindow
    {
        private readonly TrayIcon _owner;

        public TrayWindow(TrayIcon owner)
        {
            _owner = owner;
            Create("KumquatTray", Win32.WS_POPUP, Win32.WS_EX_TOOLWINDOW);
        }

        protected override nint? WndProc(uint message, nint wParam, nint lParam)
        {
            if (message != WmTray)
            {
                return null;
            }

            // Con NOTIFYICON_VERSION_4: LOWORD(lParam) = evento; wParam = coordenadas del ancla.
            var ev = (uint)(lParam & 0xFFFF);
            if (ev is Win32.WM_CONTEXTMENU or Win32.WM_RBUTTONUP or 0x0400 /* NIN_SELECT */)
            {
                _owner.ShowMenu((short)(wParam & 0xFFFF), (short)((wParam >> 16) & 0xFFFF));
            }

            return 0;
        }
    }
}

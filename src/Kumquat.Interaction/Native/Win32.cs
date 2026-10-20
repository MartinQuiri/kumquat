// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;

namespace Kumquat.Interaction.Native;

/// <summary>Funciones de Win32 usadas por la capa de interacción (generadas en compilación).</summary>
internal static unsafe partial class Win32
{
    public const uint WM_DESTROY = 0x0002;
    public const uint WM_CLOSE = 0x0010;
    public const uint WM_COMMAND = 0x0111;
    public const uint WM_TIMER = 0x0113;
    public const uint WM_MOUSEMOVE = 0x0200;
    public const uint WM_LBUTTONDOWN = 0x0201;
    public const uint WM_LBUTTONUP = 0x0202;
    public const uint WM_RBUTTONDOWN = 0x0204;
    public const uint WM_RBUTTONUP = 0x0205;
    public const uint WM_CONTEXTMENU = 0x007B;
    public const uint WM_KEYDOWN = 0x0100;
    public const uint WM_KEYUP = 0x0101;
    public const uint WM_SYSKEYDOWN = 0x0104;
    public const uint WM_SYSKEYUP = 0x0105;
    public const uint WM_DPICHANGED = 0x02E0;
    public const uint WM_APP = 0x8000;
    public const uint WM_HOTKEY = 0x0312;
    public const uint WM_GETOBJECT = 0x003D;
    public const int COLOR_WINDOW = 5;
    public const int COLOR_WINDOWTEXT = 8;
    public const int COLOR_HIGHLIGHT = 13;
    public const int COLOR_HIGHLIGHTTEXT = 14;
    public const uint MOD_ALT = 0x1;
    public const uint MOD_CONTROL = 0x2;
    public const uint MOD_SHIFT = 0x4;
    public const uint MOD_NOREPEAT = 0x4000;

    public const int WH_KEYBOARD_LL = 13;
    public const int WH_MOUSE_LL = 14;

    public const uint WS_POPUP = 0x80000000;
    public const uint WS_EX_TOPMOST = 0x00000008;
    public const uint WS_EX_TOOLWINDOW = 0x00000080;
    public const uint WS_EX_LAYERED = 0x00080000;
    public const uint WS_EX_NOACTIVATE = 0x08000000;

    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public const int SW_SHOWNOACTIVATE = 4;
    public static readonly nint HWND_TOPMOST = -1;
    public static readonly nint HWND_MESSAGE = -3;

    public const uint ULW_ALPHA = 0x00000002;
    public const byte AC_SRC_OVER = 0x00;
    public const byte AC_SRC_ALPHA = 0x01;

    public const int SM_CXDRAG = 68;
    public const int SM_CYDRAG = 69;
    public const int SM_XVIRTUALSCREEN = 76;
    public const int SM_YVIRTUALSCREEN = 77;

    public const uint MF_STRING = 0x0000;
    public const uint MF_SEPARATOR = 0x0800;
    public const uint MF_GRAYED = 0x0001;
    public const uint TPM_RETURNCMD = 0x0100;
    public const uint TPM_RIGHTBUTTON = 0x0002;

    public const uint MONITOR_DEFAULTTONEAREST = 2;

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial ushort RegisterClassExW(WNDCLASSEXW* windowClass);

    [LibraryImport("user32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint CreateWindowExW(uint exStyle, string className, string windowName, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    public static partial nint DefWindowProcW(nint hwnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    public static partial int GetMessageW(MSG* msg, nint hwnd, uint min, uint max);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool TranslateMessage(MSG* msg);

    [LibraryImport("user32.dll")]
    public static partial nint DispatchMessageW(MSG* msg);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PostMessageW(nint hwnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PostThreadMessageW(uint threadId, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    public static partial void PostQuitMessage(int exitCode);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int cx, int cy, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(nint hwnd, int command);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UpdateLayeredWindow(nint hwnd, nint hdcDst, POINT* pptDst, SIZE* psize, nint hdcSrc, POINT* pptSrc, uint crKey, BLENDFUNCTION* pblend, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetCursorPos(out POINT point);

    [LibraryImport("user32.dll")]
    public static partial int GetSystemMetrics(int index);

    [LibraryImport("user32.dll")]
    public static partial short GetAsyncKeyState(int virtualKey);

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial nint SetWindowsHookExW(int hookId, delegate* unmanaged[Stdcall]<int, nint, nint, nint> callback, nint module, uint threadId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnhookWindowsHookEx(nint hook);

    [LibraryImport("user32.dll")]
    public static partial nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    public static partial nint GetDC(nint hwnd);

    [LibraryImport("user32.dll")]
    public static partial int ReleaseDC(nint hwnd, nint hdc);

    [LibraryImport("user32.dll")]
    public static partial nint MonitorFromPoint(POINT point, uint flags);

    [LibraryImport("user32.dll")]
    public static partial nint CreatePopupMenu();

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AppendMenuW(nint menu, uint flags, nuint id, string? text);

    [LibraryImport("user32.dll")]
    public static partial uint TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint hwnd, nint parameters);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyMenu(nint menu);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    public static partial nuint SetTimer(nint hwnd, nuint id, uint elapseMs, nint timerProc);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool KillTimer(nint hwnd, nuint id);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyIcon(nint icon);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RegisterHotKey(nint window, int id, uint modifiers, uint virtualKey);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterHotKey(nint window, int id);

    public const uint IMAGE_ICON = 1;
    public const uint LR_LOADFROMFILE = 0x10;

    /// <summary>
    /// Carga un .ico eligiendo la imagen más cercana al tamaño pedido. (LoadIconWithScaleDown
    /// necesitaría Common Controls 6 activado por manifiesto.)
    /// </summary>
    [LibraryImport("user32.dll", EntryPoint = "LoadImageW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    public static partial nint LoadImage(nint instance, string name, uint type, int width, int height, uint flags);

    [LibraryImport("shcore.dll")]
    public static partial int GetDpiForMonitor(nint monitor, int dpiType, out uint dpiX, out uint dpiY);

    [LibraryImport("gdi32.dll")]
    public static partial nint CreateCompatibleDC(nint hdc);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteDC(nint hdc);

    [LibraryImport("gdi32.dll")]
    public static partial nint SelectObject(nint hdc, nint obj);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteObject(nint obj);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    public static partial nint CreateDIBSection(nint hdc, BITMAPINFOHEADER* info, uint usage, out nint bits, nint section, uint offset);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint GetModuleHandleW(string? moduleName);

    [LibraryImport("kernel32.dll")]
    public static partial uint GetCurrentThreadId();

    [LibraryImport("ole32.dll")]
    public static partial int OleInitialize(nint reserved);

    [LibraryImport("ole32.dll")]
    public static partial void OleUninitialize();

    [LibraryImport("ole32.dll")]
    public static partial int RegisterDragDrop(nint hwnd, nint dropTarget);

    [LibraryImport("ole32.dll")]
    public static partial int RevokeDragDrop(nint hwnd);

    [LibraryImport("ole32.dll")]
    public static partial void ReleaseStgMedium(STGMEDIUM* medium);

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint DragQueryFileW(nint drop, uint index, char* file, uint length);

    [LibraryImport("shell32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool Shell_NotifyIconW(uint message, NOTIFYICONDATAW* data);

    [LibraryImport("user32.dll")]
    public static partial uint GetSysColor(int index);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetLastInputInfo(LASTINPUTINFO* info);

    /// <summary>Indica si el alto contraste de Windows está activo.</summary>
    public static bool IsHighContrastOn()
    {
        const uint SpiGetHighContrast = 0x0042;
        const uint HcfHighContrastOn = 0x1;
        var info = new HIGHCONTRASTW { CbSize = (uint)sizeof(HIGHCONTRASTW) };
        return SystemParametersInfoW(SpiGetHighContrast, info.CbSize, &info, 0) && (info.DwFlags & HcfHighContrastOn) != 0;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfoW(uint action, uint param, void* data, uint winIni);

    [StructLayout(LayoutKind.Sequential)]
    private struct HIGHCONTRASTW
    {
        public uint CbSize;
        public uint DwFlags;
        public nint LpszDefaultScheme;
    }
}

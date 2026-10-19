// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;

namespace Kumquat.E2E.Tests;

/// <summary>Entrada sintética con SendInput: mueve el mouse real y presiona teclas reales.</summary>
internal static unsafe partial class SyntheticInput
{
    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint MouseMove = 0x0001;
    private const uint MouseLeftDown = 0x0002;
    private const uint MouseLeftUp = 0x0004;
    private const uint MouseVirtualDesk = 0x4000;
    private const uint MouseAbsolute = 0x8000;
    private const uint KeyUp = 0x0002;
    private const ushort VkShift = 0x10;

    public static void MoveTo(int x, int y)
    {
        var vx = GetSystemMetrics(76);
        var vy = GetSystemMetrics(77);
        var vw = GetSystemMetrics(78);
        var vh = GetSystemMetrics(79);
        Send(new Input
        {
            Type = InputMouse,
            Mouse = new MouseInput
            {
                Dx = (int)Math.Round((x - vx) * 65535.0 / (vw - 1)),
                Dy = (int)Math.Round((y - vy) * 65535.0 / (vh - 1)),
                Flags = MouseMove | MouseAbsolute | MouseVirtualDesk,
            },
        });
    }

    public static void LeftDown() => Send(new Input { Type = InputMouse, Mouse = new MouseInput { Flags = MouseLeftDown } });

    public static void LeftUp() => Send(new Input { Type = InputMouse, Mouse = new MouseInput { Flags = MouseLeftUp } });

    public static void TapShift()
    {
        Send(new Input { Type = InputKeyboard, Keyboard = new KeyboardInput { Vk = VkShift } });
        Thread.Sleep(40);
        Send(new Input { Type = InputKeyboard, Keyboard = new KeyboardInput { Vk = VkShift, Flags = KeyUp } });
    }

    /// <summary>Presiona las teclas en orden y las suelta al revés (p. ej. Ctrl+Alt+K).</summary>
    public static void PressChord(params ushort[] keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        foreach (var key in keys)
        {
            Send(new Input { Type = InputKeyboard, Keyboard = new KeyboardInput { Vk = key } });
            Thread.Sleep(30);
        }

        foreach (var key in keys.Reverse())
        {
            Send(new Input { Type = InputKeyboard, Keyboard = new KeyboardInput { Vk = key, Flags = KeyUp } });
            Thread.Sleep(30);
        }
    }

    public static (int X, int Y) CursorPosition()
    {
        GetCursorPos(out var x);
        return (x.X, x.Y);
    }

    public static float DpiScaleAt(int x, int y)
    {
        var monitor = MonitorFromPoint(new Point { X = x, Y = y }, 2);
        return GetDpiForMonitor(monitor, 0, out var dpi, out _) == 0 ? dpi / 96f : 1f;
    }

    /// <summary>Coordenadas físicas en todos los monitores (como Kumquat).</summary>
    public static void BecomePerMonitorDpiAware() => SetProcessDpiAwarenessContext(-4);

    private static void Send(Input input)
    {
        if (SendInput(1, &input, sizeof(Input)) != 1)
        {
            throw new InvalidOperationException($"SendInput falló ({Marshal.GetLastPInvokeError()}). ¿Hay un escritorio interactivo?");
        }
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial uint SendInput(uint count, Input* inputs, int size);

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int index);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out Point point);

    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromPoint(Point point, uint flags);

    [LibraryImport("shcore.dll")]
    private static partial int GetDpiForMonitor(nint monitor, int type, out uint dpiX, out uint dpiY);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetProcessDpiAwarenessContext(nint context);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct Input
    {
        [FieldOffset(0)]
        public uint Type;

        [FieldOffset(8)]
        public MouseInput Mouse;

        [FieldOffset(8)]
        public KeyboardInput Keyboard;
    }
}

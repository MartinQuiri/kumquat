// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;

namespace Kumquat.Interaction.Shell;

/// <summary>
/// VARIANT mínimo y blittable (24 bytes en 64 bits): enteros y vacío (IShellWindows.Item y
/// FindWindowSW) y texto y booleano (propiedades de UI Automation, que libera quien las pide).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct Variant
{
    private const ushort VtI4 = 3;
    private const ushort VtBstr = 8;
    private const ushort VtBool = 11;

    public ushort Type;
    public ushort Reserved1;
    public ushort Reserved2;
    public ushort Reserved3;
    public long Value;
    public nint Record;

    /// <summary>Entero de 32 bits (VT_I4).</summary>
    public static Variant Int32(int value) => new() { Type = VtI4, Value = value };

    /// <summary>Texto (VT_BSTR); el BSTR pasa a ser de quien recibe el VARIANT.</summary>
    public static Variant String(string value) => new() { Type = VtBstr, Value = Marshal.StringToBSTR(value) };

    /// <summary>Booleano (VT_BOOL: -1 verdadero, 0 falso).</summary>
    public static Variant Boolean(bool value) => new() { Type = VtBool, Value = value ? -1 : 0 };
}

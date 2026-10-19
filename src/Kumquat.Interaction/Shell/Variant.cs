// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;

namespace Kumquat.Interaction.Shell;

/// <summary>
/// VARIANT mínimo y blittable (24 bytes en 64 bits): solo enteros y vacío, que es lo que piden
/// IShellWindows.Item y FindWindowSW.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct Variant
{
    private const ushort VtI4 = 3;

    public ushort Type;
    public ushort Reserved1;
    public ushort Reserved2;
    public ushort Reserved3;
    public long Value;
    public nint Record;

    /// <summary>Entero de 32 bits (VT_I4).</summary>
    public static Variant Int32(int value) => new() { Type = VtI4, Value = value };
}

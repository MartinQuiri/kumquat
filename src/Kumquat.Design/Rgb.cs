// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Design;

/// <summary>Color opaco, independiente de GDI+ y WPF.</summary>
/// <param name="R">Rojo.</param>
/// <param name="G">Verde.</param>
/// <param name="B">Azul.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct Rgb(byte R, byte G, byte B)
{
    /// <summary>Mezcla con otro color (<paramref name="amount"/> = 0 es este, 1 es el otro).</summary>
    public Rgb Mix(Rgb other, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return new Rgb(Lerp(R, other.R), Lerp(G, other.G), Lerp(B, other.B));

        byte Lerp(byte a, byte b) => (byte)Math.Round(a + ((b - a) * amount));
    }
}

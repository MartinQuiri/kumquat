// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;

namespace Kumquat.Interaction.Native;

/// <summary>Escribe texto en el portapapeles de Windows (CF_UNICODETEXT) sin depender de WinForms/WPF.</summary>
public static unsafe partial class ClipboardText
{
    private const uint CfUnicodeText = 13;
    private const uint GmemMoveable = 0x0002;

    /// <summary>Reemplaza el contenido del portapapeles. Devuelve <c>false</c> si otro proceso lo tiene tomado.</summary>
    public static bool TrySet(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // Otro proceso puede tener el portapapeles abierto un instante: se reintenta brevemente.
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (OpenClipboard(0))
            {
                try
                {
                    return Write(text);
                }
                finally
                {
                    CloseClipboard();
                }
            }

            Thread.Sleep(20);
        }

        return false;
    }

    private static bool Write(string text)
    {
        if (!EmptyClipboard())
        {
            return false;
        }

        var bytes = (nuint)((text.Length + 1) * sizeof(char));
        var memory = GlobalAlloc(GmemMoveable, bytes);
        if (memory == 0)
        {
            return false;
        }

        var target = (char*)GlobalLock(memory);
        if (target == null)
        {
            GlobalFree(memory);
            return false;
        }

        try
        {
            text.AsSpan().CopyTo(new Span<char>(target, text.Length));
            target[text.Length] = '\0';
        }
        finally
        {
            GlobalUnlock(memory);
        }

        if (SetClipboardData(CfUnicodeText, memory) == 0)
        {
            GlobalFree(memory);
            return false;
        }

        return true; // El sistema es dueño de la memoria desde aquí.
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenClipboard(nint owner);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseClipboard();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EmptyClipboard();

    [LibraryImport("user32.dll")]
    private static partial nint SetClipboardData(uint format, nint memory);

    [LibraryImport("kernel32.dll")]
    private static partial nint GlobalAlloc(uint flags, nuint bytes);

    [LibraryImport("kernel32.dll")]
    private static partial void* GlobalLock(nint memory);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalUnlock(nint memory);

    [LibraryImport("kernel32.dll")]
    private static partial nint GlobalFree(nint memory);
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;

namespace Kumquat.Interaction.Wheel;

/// <summary>
/// Pedidos de "abrir la rueda con estos archivos" desde otro proceso (el menú contextual del
/// Explorador lanza <c>Kumquat.exe --wheel archivo</c>): se envían a la instancia que ya corre con
/// <c>WM_COPYDATA</c>, que solo acepta texto y solo de procesos del mismo nivel de integridad.
/// </summary>
public static unsafe partial class WheelRequests
{
    /// <summary>Marca de los mensajes de Kumquat ("KQWH"), para ignorar cualquier otro WM_COPYDATA.</summary>
    internal const nuint Signature = 0x4B515748;

    internal const uint WmCopyData = 0x004A;

    private const uint SmtoAbortIfHung = 0x2;

    /// <summary>
    /// Envía las rutas a la instancia en ejecución. <c>false</c> si no hay ninguna (o no respondió
    /// a tiempo): entonces quien llama debe arrancar normalmente.
    /// </summary>
    public static bool TrySend(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var window = FindWindowW(WheelWindow.ClassName, null);
        return window != 0 && Send(window, paths);
    }

    /// <summary>Envía las rutas a una ventana concreta.</summary>
    internal static bool Send(nint window, IReadOnlyList<string> paths)
    {
        foreach (var path in paths)
        {
            fixed (char* text = path)
            {
                var data = new CopyData { Signature = Signature, Bytes = (uint)((path.Length + 1) * sizeof(char)), Data = (nint)text };
                if (SendMessageTimeoutW(window, WmCopyData, 0, (nint)(&data), SmtoAbortIfHung, 2000, out _) == 0)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Lee la ruta de un WM_COPYDATA de Kumquat, o <c>null</c> si no es uno.</summary>
    internal static string? Read(nint lParam)
    {
        var data = (CopyData*)lParam;
        if (data is null || data->Signature != Signature || data->Data == 0 || data->Bytes < sizeof(char) || data->Bytes > 2 * 32_768 * sizeof(char))
        {
            return null;
        }

        var text = new string((char*)data->Data, 0, (int)(data->Bytes / sizeof(char))).TrimEnd('\0');
        return text.Length > 0 ? text : null;
    }

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint FindWindowW(string className, string? windowName);

    [LibraryImport("user32.dll")]
    private static partial nint SendMessageTimeoutW(nint window, uint message, nint wParam, nint lParam, uint flags, uint timeoutMilliseconds, out nint result);

    [StructLayout(LayoutKind.Sequential)]
    private struct CopyData
    {
        public nuint Signature;
        public uint Bytes;
        public nint Data;
    }
}

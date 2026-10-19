// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Interaction.Native;

namespace Kumquat.Interaction.DragDrop;

/// <summary>
/// Lee rutas de archivo (<c>CF_HDROP</c>) de un <c>IDataObject</c> nativo llamando
/// directamente a su vtable: evita depender del interop clásico de COM.
/// </summary>
/// <remarks>
/// TODO(fase 2): archivos virtuales (<c>CFSTR_FILEDESCRIPTORW</c> + <c>CFSTR_FILECONTENTS</c>)
/// para adjuntos de Outlook, carpetas zip y navegadores.
/// </remarks>
internal static unsafe class DataObjectReader
{
    private const ushort CfHdrop = 15;
    private const uint DvaspectContent = 1;
    private const uint TymedHglobal = 1;
    private const int MaxPath = 32_768;

    // Índices de la vtable de IDataObject (tras los 3 de IUnknown).
    private const int GetDataSlot = 3;
    private const int QueryGetDataSlot = 5;

    /// <summary>Rutas de archivo del objeto, o lista vacía si no trae archivos.</summary>
    public static IReadOnlyList<string> ReadFiles(nint dataObject)
    {
        if (dataObject == 0)
        {
            return [];
        }

        var format = new FORMATETC { CfFormat = CfHdrop, DwAspect = DvaspectContent, Lindex = -1, Tymed = TymedHglobal };
        var vtable = *(void***)dataObject;
        var queryGetData = (delegate* unmanaged[Stdcall]<nint, FORMATETC*, int>)vtable[QueryGetDataSlot];
        if (queryGetData(dataObject, &format) != 0)
        {
            return [];
        }

        var getData = (delegate* unmanaged[Stdcall]<nint, FORMATETC*, STGMEDIUM*, int>)vtable[GetDataSlot];
        STGMEDIUM medium;
        if (getData(dataObject, &format, &medium) != 0)
        {
            return [];
        }

        try
        {
            return ReadDropHandle(medium.Handle);
        }
        finally
        {
            Win32.ReleaseStgMedium(&medium);
        }
    }

    private static List<string> ReadDropHandle(nint drop)
    {
        var count = Win32.DragQueryFileW(drop, uint.MaxValue, null, 0);
        var files = new List<string>((int)Math.Min(count, 4096));
        var buffer = new char[MaxPath];
        fixed (char* p = buffer)
        {
            for (uint i = 0; i < count; i++)
            {
                var length = Win32.DragQueryFileW(drop, i, p, MaxPath);
                if (length > 0)
                {
                    files.Add(new string(p, 0, (int)length));
                }
            }
        }

        return files;
    }
}

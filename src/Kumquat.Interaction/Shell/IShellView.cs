// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Kumquat.Interaction.Shell;

/// <summary>Vista de carpeta del Explorador (shobjidl_core.h): da la selección como IDataObject.</summary>
[GeneratedComInterface]
[Guid("000214E3-0000-0000-C000-000000000046")]
internal partial interface IShellView
{
    [PreserveSig]
    int GetWindow(out nint hwnd);

    [PreserveSig]
    int ContextSensitiveHelp(int enterMode);

    [PreserveSig]
    int TranslateAccelerator();

    [PreserveSig]
    int EnableModeless();

    [PreserveSig]
    int UIActivate();

    [PreserveSig]
    int Refresh();

    [PreserveSig]
    int CreateViewWindow();

    [PreserveSig]
    int DestroyViewWindow();

    [PreserveSig]
    int GetCurrentInfo();

    [PreserveSig]
    int AddPropertySheetPages();

    [PreserveSig]
    int SaveViewState();

    /// <summary>Selecciona un elemento (identificador relativo a la carpeta) con banderas <c>SVSI_*</c>.</summary>
    [PreserveSig]
    int SelectItem(nint childItem, uint flags);

    /// <summary>Con <c>SVGIO_SELECTION</c> e IID_IDataObject: los elementos seleccionados.</summary>
    [PreserveSig]
    int GetItemObject(uint item, in Guid iid, out nint instance);
}

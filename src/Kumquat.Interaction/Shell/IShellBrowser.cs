// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Kumquat.Interaction.Shell;

/// <summary>Explorador (o pestaña) que contiene una vista de carpeta (shobjidl_core.h).</summary>
[GeneratedComInterface]
[Guid("000214E2-0000-0000-C000-000000000046")]
internal partial interface IShellBrowser
{
    /// <summary>IOleWindow: ventana de la pestaña (<c>ShellTabWindowClass</c> en Windows 11).</summary>
    [PreserveSig]
    int GetWindow(out nint hwnd);

    [PreserveSig]
    int ContextSensitiveHelp(int enterMode);

    [PreserveSig]
    int InsertMenusSB();

    [PreserveSig]
    int SetMenuSB();

    [PreserveSig]
    int RemoveMenusSB();

    [PreserveSig]
    int SetStatusTextSB();

    [PreserveSig]
    int EnableModelessSB();

    [PreserveSig]
    int TranslateAcceleratorSB();

    [PreserveSig]
    int BrowseObject();

    [PreserveSig]
    int GetViewStateStream();

    [PreserveSig]
    int GetControlWindow();

    [PreserveSig]
    int SendControlMsg();

    [PreserveSig]
    int QueryActiveShellView(out IShellView view);
}

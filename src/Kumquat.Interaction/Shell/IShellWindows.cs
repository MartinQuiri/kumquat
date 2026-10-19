// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Kumquat.Interaction.Shell;

/// <summary>
/// Ventanas del Explorador abiertas (exdisp.h). Es una interfaz dual: los cuatro primeros
/// métodos son los de IDispatch, que solo ocupan su lugar en la vtable.
/// </summary>
[GeneratedComInterface]
[Guid("85CB6900-4D95-11CF-960C-0080C7F4EE85")]
internal partial interface IShellWindows
{
    [PreserveSig]
    int DispatchGetTypeInfoCount();

    [PreserveSig]
    int DispatchGetTypeInfo();

    [PreserveSig]
    int DispatchGetIDsOfNames();

    [PreserveSig]
    int DispatchInvoke();

    [PreserveSig]
    int GetCount(out int count);

    [PreserveSig]
    int Item(Variant index, out nint dispatch);

    [PreserveSig]
    int NewEnum(out nint enumerator);

    [PreserveSig]
    int Register();

    [PreserveSig]
    int RegisterPending();

    [PreserveSig]
    int Revoke();

    [PreserveSig]
    int OnNavigate();

    [PreserveSig]
    int OnActivated();

    [PreserveSig]
    int FindWindowSW(in Variant location, in Variant locationRoot, int windowClass, out int hwnd, int options, out nint dispatch);
}

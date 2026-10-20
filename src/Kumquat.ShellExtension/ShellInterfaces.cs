// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

#pragma warning disable MA0048 // Interfaces COM chicas y relacionadas: juntas se leen mejor.

namespace Kumquat.ShellExtension;

/// <summary>Comando del menú contextual del Explorador (shobjidl_core.h).</summary>
[GeneratedComInterface]
[Guid("A08CE4D0-FA25-44AB-B57C-C7B1C323E0B9")]
internal partial interface IExplorerCommand
{
    [PreserveSig]
    int GetTitle(nint itemArray, out nint name);

    [PreserveSig]
    int GetIcon(nint itemArray, out nint icon);

    [PreserveSig]
    int GetToolTip(nint itemArray, out nint tooltip);

    [PreserveSig]
    int GetCanonicalName(out Guid name);

    [PreserveSig]
    int GetState(nint itemArray, int okToBeSlow, out uint state);

    [PreserveSig]
    int Invoke(nint itemArray, nint bindContext);

    [PreserveSig]
    int GetFlags(out uint flags);

    [PreserveSig]
    int EnumSubCommands(out nint enumerator);
}

/// <summary>Fábrica de objetos COM (unknwn.h).</summary>
[GeneratedComInterface]
[Guid("00000001-0000-0000-C000-000000000046")]
internal partial interface IClassFactory
{
    [PreserveSig]
    int CreateInstance(nint outer, in Guid iid, out nint instance);

    [PreserveSig]
    int LockServer(int lockServer);
}

/// <summary>Los elementos elegidos en el Explorador (shobjidl_core.h), lo que se usa.</summary>
[GeneratedComInterface]
[Guid("B63EA76D-1F85-456F-A19C-48159EFA858B")]
internal partial interface IShellItemArray
{
    [PreserveSig]
    int BindToHandler(nint context, in Guid handler, in Guid iid, out nint result);

    [PreserveSig]
    int GetPropertyStore(int flags, in Guid iid, out nint store);

    [PreserveSig]
    int GetPropertyDescriptionList(nint keyType, in Guid iid, out nint list);

    [PreserveSig]
    int GetAttributes(int flags, uint mask, out uint attributes);

    [PreserveSig]
    int GetCount(out uint count);

    [PreserveSig]
    int GetItemAt(uint index, out nint item);
}

/// <summary>Un elemento del Explorador (shobjidl_core.h), lo que se usa.</summary>
[GeneratedComInterface]
[Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
internal partial interface IShellItem
{
    [PreserveSig]
    int BindToHandler(nint context, in Guid handler, in Guid iid, out nint result);

    [PreserveSig]
    int GetParent(out nint parent);

    [PreserveSig]
    int GetDisplayName(uint form, out nint name);
}

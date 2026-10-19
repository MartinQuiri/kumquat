// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Kumquat.Interaction.Native;

namespace Kumquat.Interaction.DragDrop;

/// <summary>
/// Interfaz COM <c>IDropTarget</c> de OLE. El <c>IDataObject</c> se recibe como puntero
/// crudo: solo se necesitan dos métodos de su vtable (ver <see cref="DataObjectReader"/>).
/// </summary>
[GeneratedComInterface]
[Guid("00000122-0000-0000-C000-000000000046")]
internal partial interface IDropTarget
{
    [PreserveSig]
    int DragEnter(nint dataObject, uint keyState, POINTL point, ref uint effect);

    [PreserveSig]
    int DragOver(uint keyState, POINTL point, ref uint effect);

    [PreserveSig]
    int DragLeave();

    [PreserveSig]
    int Drop(nint dataObject, uint keyState, POINTL point, ref uint effect);
}

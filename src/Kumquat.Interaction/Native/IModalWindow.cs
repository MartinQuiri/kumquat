// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Kumquat.Interaction.Native;

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("b4db1657-70d7-485e-8e3e-6fcb5a5c1802")]
internal partial interface IModalWindow
{
    [PreserveSig]
    int Show(nint owner);
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Kumquat.Interaction.Shell;

/// <summary>IServiceProvider de COM (servprov.h): de una ventana del Explorador a su IShellBrowser.</summary>
[GeneratedComInterface]
[Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
internal partial interface IServiceProvider
{
    [PreserveSig]
    int QueryService(in Guid service, in Guid iid, out nint instance);
}

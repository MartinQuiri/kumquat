// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Kumquat.Interaction.Native;

[GeneratedComInterface]
[Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
internal partial interface IShellItem
{
    void BindToHandler(nint context, in Guid handler, in Guid iid, out nint result);

    void GetParent(out IShellItem parent);

    void GetDisplayName(uint form, out nint name);
}

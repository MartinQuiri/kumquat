// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Kumquat.Dialogs.Shelf;

/// <summary>Miniatura o ícono de un archivo tal como lo muestra el Explorador.</summary>
[GeneratedComInterface]
[Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
internal partial interface IShellItemImageFactory
{
    void GetImage(ShellThumbnails.NativeSize size, int flags, out nint bitmap);
}

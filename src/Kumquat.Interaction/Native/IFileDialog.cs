// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Kumquat.Interaction.Native;

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("42f85136-db7e-439c-85f1-e4075d135fc8")]
internal partial interface IFileDialog : IModalWindow
{
    void SetFileTypes(uint count, nint filterSpec);

    void SetFileTypeIndex(uint index);

    void GetFileTypeIndex(out uint index);

    void Advise(nint events, out uint cookie);

    void Unadvise(uint cookie);

    void SetOptions(uint options);

    void GetOptions(out uint options);

    void SetDefaultFolder(nint item);

    void SetFolder(nint item);

    void GetFolder(out nint item);

    void GetCurrentSelection(out nint item);

    void SetFileName(string name);

    void GetFileName(out nint name);

    void SetTitle(string title);

    void SetOkButtonLabel(string text);

    void SetFileNameLabel(string label);

    void GetResult(out IShellItem item);
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Interaction.DragDrop;

/// <summary>Efectos de drop de OLE.</summary>
[Flags]
internal enum DropEffects : uint
{
    None = 0,
    Copy = 1,
    Move = 2,
    Link = 4,
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Interaction.DragDrop;

namespace Kumquat.Interaction.Tests;

public sealed class DropEffectTests
{
    private const uint None = (uint)DropEffects.None;
    private const uint Copy = (uint)DropEffects.Copy;
    private const uint Move = (uint)DropEffects.Move;
    private const uint Link = (uint)DropEffects.Link;

    [Theory]
    [InlineData(Copy | Move | Link, true, Copy)]
    [InlineData(Move, true, None)]
    [InlineData(Move | Link, true, Link)]
    [InlineData(Copy | Move, false, None)]
    public void NuncaDevuelveMove(uint allowed, bool accepted, uint expected)
    {
        // MOVE permitiría a Explorer borrar el original tras un "movimiento optimizado".
        DropTarget.Choose(allowed, accepted).ShouldBe(expected);
        ((DropEffects)DropTarget.Choose(allowed, accepted)).HasFlag(DropEffects.Move).ShouldBeFalse();
    }
}

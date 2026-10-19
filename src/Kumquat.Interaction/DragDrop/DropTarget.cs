// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices.Marshalling;
using Kumquat.Interaction.Native;

namespace Kumquat.Interaction.DragDrop;

/// <summary>
/// Implementación COM de <c>IDropTarget</c>. Nunca responde <c>MOVE</c>: si lo hiciera,
/// Explorer podría borrar el original tras un "movimiento optimizado".
/// </summary>
[GeneratedComClass]
internal sealed partial class DropTarget(IDropHandler handler) : IDropTarget
{
    private const int SOk = 0;

    public int DragEnter(nint dataObject, uint keyState, POINTL point, ref uint effect)
    {
        var files = DataObjectReader.ReadFiles(dataObject);
        var accepted = files.Count > 0 && handler.OnDragEnter(files, ToPoint(point));
        effect = Choose(effect, accepted);
        return SOk;
    }

    public int DragOver(uint keyState, POINTL point, ref uint effect)
    {
        effect = Choose(effect, handler.OnDragOver(ToPoint(point)));
        return SOk;
    }

    public int DragLeave()
    {
        handler.OnDragLeave();
        return SOk;
    }

    public int Drop(nint dataObject, uint keyState, POINTL point, ref uint effect)
    {
        var files = DataObjectReader.ReadFiles(dataObject);
        var accepted = files.Count > 0 && handler.OnDragOver(ToPoint(point));
        effect = Choose(effect, accepted);
        if (accepted)
        {
            handler.OnDrop(files, ToPoint(point));
        }

        return SOk;
    }

    /// <summary>COPY si la fuente lo permite; si no, LINK; nunca MOVE.</summary>
    internal static uint Choose(uint allowed, bool accepted)
    {
        if (!accepted)
        {
            return (uint)DropEffects.None;
        }

        var effects = (DropEffects)allowed;
        if (effects.HasFlag(DropEffects.Copy))
        {
            return (uint)DropEffects.Copy;
        }

        return effects.HasFlag(DropEffects.Link) ? (uint)DropEffects.Link : (uint)DropEffects.None;
    }

    private static POINT ToPoint(POINTL p) => new(p.X, p.Y);
}

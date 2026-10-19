// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Interaction.Native;

namespace Kumquat.Interaction.DragDrop;

/// <summary>Receptor de eventos de arrastre, ya traducidos a tipos .NET.</summary>
internal interface IDropHandler
{
    /// <summary>Entró un arrastre. Devuelve si la rueda puede aceptar estos archivos.</summary>
    bool OnDragEnter(IReadOnlyList<string> files, POINT screenPoint);

    /// <summary>El cursor se movió. Devuelve si el punto actual acepta el drop.</summary>
    bool OnDragOver(POINT screenPoint);

    /// <summary>El cursor salió de la ventana sin soltar.</summary>
    void OnDragLeave();

    /// <summary>Se soltó sobre la ventana.</summary>
    void OnDrop(IReadOnlyList<string> files, POINT screenPoint);
}

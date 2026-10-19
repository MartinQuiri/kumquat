// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Formats;

namespace Kumquat.Interaction.Wheel;

/// <summary>Un sector de la rueda.</summary>
/// <param name="Label">Texto corto (p. ej. <c>PNG</c>, "Comprimir").</param>
/// <param name="Id">Identificador del destino, herramienta o acción.</param>
/// <param name="Ring">Anillo al que pertenece.</param>
/// <param name="Target">Formato de destino, solo en el anillo de conversión.</param>
public sealed record WheelItem(string Label, string Id, WheelRingKind Ring, FileFormat? Target = null)
{
    /// <summary>Sector de conversión.</summary>
    public static WheelItem Convert(FileFormat target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return new WheelItem(target.DisplayName, target.Id, WheelRingKind.Convert, target);
    }

    /// <summary>Sector de herramienta.</summary>
    public static WheelItem Tool(string id, string label) => new(label, id, WheelRingKind.Tools);

    /// <summary>Sector de acción.</summary>
    public static WheelItem Action(string id, string label) => new(label, id, WheelRingKind.Actions);
}

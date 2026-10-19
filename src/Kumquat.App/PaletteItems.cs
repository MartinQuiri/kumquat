// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Design;
using Kumquat.Dialogs.Search;
using Kumquat.Interaction.Wheel;

namespace Kumquat.App;

/// <summary>De los sectores de la rueda a las filas del buscador (mismos destinos, otro modo de elegir).</summary>
internal static class PaletteItems
{
    public static PaletteItem From(WheelItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Ring switch
        {
            WheelRingKind.Convert when item.Target is { } target => new PaletteItem(
                item.Id,
                $"Convertir a {item.Label}",
                "Convertir",
                Icons.ForCategory(target.Category),
                Palette.Kumquat,
                [target.Id, target.DisplayName, .. target.Extensions]),
            WheelRingKind.Actions => new PaletteItem(item.Id, item.Label, "Acciones", Icons.ForItem(item.Id), Palette.Actions, CommandKeywords.For(item.Id)),
            _ => new PaletteItem(item.Id, item.Label, "Herramientas", Icons.ForItem(item.Id), Palette.ForItem(item.Id), CommandKeywords.For(item.Id)),
        };
    }
}

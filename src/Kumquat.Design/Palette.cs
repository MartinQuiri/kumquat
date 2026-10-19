// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Design;

/// <summary>
/// Colores de Kumquat, inspirados en los colores de sistema de Apple: cada familia de
/// herramientas tiene el suyo (como en iLovePDF), y el naranja del logo es el acento general.
/// </summary>
public static class Palette
{
    /// <summary>Naranja kumquat (acento general y anillo de conversión).</summary>
    public static Rgb Kumquat { get; } = new(0xFF, 0x95, 0x0A);

    /// <summary>Anillo de herramientas.</summary>
    public static Rgb Tools { get; } = new(0x30, 0xB0, 0xC7);

    /// <summary>Anillo de acciones.</summary>
    public static Rgb Actions { get; } = new(0xAF, 0x52, 0xDE);

    /// <summary>Herramientas de PDF.</summary>
    public static Rgb Pdf { get; } = new(0xFF, 0x45, 0x3A);

    /// <summary>Herramientas de imagen.</summary>
    public static Rgb Image { get; } = new(0x34, 0xC7, 0x59);

    /// <summary>Herramientas de video.</summary>
    public static Rgb Video { get; } = new(0x5E, 0x5C, 0xE6);

    /// <summary>Herramientas de audio.</summary>
    public static Rgb Audio { get; } = new(0xFF, 0x37, 0x5F);

    /// <summary>Documentos de Office.</summary>
    public static Rgb Office { get; } = new(0x0A, 0x84, 0xFF);

    /// <summary>Comprimidos.</summary>
    public static Rgb Archive { get; } = new(0xA2, 0x84, 0x5E);

    /// <summary>Color propio de una herramienta o acción, según la familia de su id.</summary>
    public static Rgb ForItem(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        var family = id.Split('.')[0];
        return family switch
        {
            "pdf" => Pdf,
            "image" => Image,
            "video" or "media" => Video,
            "audio" => Audio,
            "office" => Office,
            "archive" => Archive,
            "action" => Actions,
            _ => Kumquat,
        };
    }
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Drawing;
using System.Drawing.Text;
using Kumquat.Design;

namespace Kumquat.Interaction.Wheel;

/// <summary>
/// Tipografía Inter cargada desde los TTF que acompañan al ejecutable (sin instalarla en el
/// sistema). Si faltan, se usa Segoe UI.
/// </summary>
internal static class WheelFonts
{
    private static readonly Lazy<Families> Loaded = new(Load);

    /// <summary>Peso semi-negrita (formatos del anillo interior).</summary>
    public static FontFamily SemiBold => Loaded.Value.SemiBold;

    /// <summary>Peso medio (etiquetas de herramientas y acciones).</summary>
    public static FontFamily Medium => Loaded.Value.Medium;

    /// <summary>Peso regular (mensajes).</summary>
    public static FontFamily Regular => Loaded.Value.Regular;

    /// <summary>Indica si se encontró Inter.</summary>
    public static bool IsInter => Loaded.Value.IsInter;

    private static Families Load()
    {
        // La colección y las familias viven lo que el proceso: GDI+ las referencia mientras haya fuentes creadas.
#pragma warning disable CA2000
        var collection = new PrivateFontCollection();
        foreach (var file in DesignAssets.FontFiles)
        {
            collection.AddFontFile(file);
        }

        // GDI agrupa los TTF estáticos por nombre de familia "heredado": "Inter", "Inter Medium", "Inter SemiBold".
        FontFamily? Find(string name) => collection.Families.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
        var regular = Find("Inter");
        var medium = Find("Inter Medium");
        var semiBold = Find("Inter SemiBold");
        if (regular is null || medium is null || semiBold is null)
        {
            return new Families(new FontFamily("Segoe UI"), new FontFamily("Segoe UI Semibold"), new FontFamily("Segoe UI Semibold"), IsInter: false);
        }

        return new Families(regular, medium, semiBold, IsInter: true);
#pragma warning restore CA2000
    }

    private sealed record Families(FontFamily Regular, FontFamily Medium, FontFamily SemiBold, bool IsInter);
}

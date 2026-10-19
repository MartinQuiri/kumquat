// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using NetVips;

namespace Kumquat.Engines.Vips;

/// <summary>Operaciones de imagen puntuales que usan otros motores (p. ej. el de PDF).</summary>
public static class VipsImaging
{
    /// <summary>Dimensiones ya orientadas (como se ve la foto), leyendo solo la cabecera.</summary>
    public static (int Width, int Height) OrientedSize(string path)
    {
        using var image = Image.NewFromFile(path, access: Enums.Access.Sequential);
        var swap = image.Contains("orientation") && image.Get("orientation") is int o && o is >= 5 and <= 8;
        return swap ? (image.Height, image.Width) : (image.Width, image.Height);
    }

    /// <summary>Indica si la foto tiene orientación EXIF que haya que aplicar a los píxeles.</summary>
    public static bool HasOrientation(string path)
    {
        using var image = Image.NewFromFile(path, access: Enums.Access.Sequential);
        return image.Contains("orientation") && image.Get("orientation") is int o && o > 1;
    }

    /// <summary>Guarda como JPEG con la orientación aplicada y sin alfa (fondo blanco).</summary>
    public static void SaveAsJpeg(string source, string destination, int quality)
    {
        using var image = VipsLoader.OpenOriented(source, needsRandomAccess: false);
        using var flat = image.HasAlpha() ? image.Flatten(background: [255, 255, 255]) : image.Copy();
        flat.Jpegsave(destination, q: quality, optimizeCoding: true);
    }

    /// <summary>Convierte un PNG (p. ej. una página renderizada) en JPEG.</summary>
    public static void PngToJpeg(string source, string destination, int quality)
    {
        using var image = Image.NewFromFile(source, access: Enums.Access.Sequential);
        using var flat = image.HasAlpha() ? image.Flatten(background: [255, 255, 255]) : image.Copy();
        flat.Jpegsave(destination, q: quality, optimizeCoding: true);
    }
}

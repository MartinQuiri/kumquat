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

    /// <summary>
    /// Guarda como PNG con la orientación aplicada. Los SVG se dibujan a 300 ppp (a 72, el tamaño por
    /// defecto, el texto queda demasiado chico para reconocerlo).
    /// </summary>
    public static void SaveAsPng(string source, string destination)
    {
        var vector = string.Equals(Path.GetExtension(source), ".svg", StringComparison.OrdinalIgnoreCase);
        using var image = VipsLoader.OpenOriented(source, needsRandomAccess: false, vector ? new VOption { { "dpi", 300.0 } } : null);
        image.Pngsave(destination);
    }

    /// <summary>Resolución declarada en la imagen, en puntos por pulgada (<c>null</c> si no la declara).</summary>
    public static double? Dpi(string path)
    {
        using var image = Image.NewFromFile(path, access: Enums.Access.Sequential);

        // libvips guarda la resolución en píxeles por milímetro; 1 px/mm es su "sin dato".
        var dpi = image.Xres * 25.4;
        return image.Xres > 1.001 ? dpi : null;
    }

    /// <summary>Miniatura PNG con la orientación aplicada (primer fotograma si es animada).</summary>
    public static void Thumbnail(string source, string destination, int size)
    {
        using var image = Image.Thumbnail(source, size, height: size, size: Enums.Size.Down);
        image.Pngsave(destination);
    }
}

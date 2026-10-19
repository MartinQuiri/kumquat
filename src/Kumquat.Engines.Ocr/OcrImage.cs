// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;
using Kumquat.Engines.Vips;

namespace Kumquat.Engines.Ocr;

/// <summary>
/// Prepara una foto para Tesseract. Los formatos que Tesseract lee pasan tal cual (un JPEG queda
/// JPEG dentro del PDF: no engorda); HEIC, AVIF, SVG o fotos con rotación EXIF se normalizan.
/// </summary>
internal static class OcrImage
{
    private static readonly FileFormat[] Native = [KnownFormats.Png, KnownFormats.Jpg, KnownFormats.Tiff, KnownFormats.Bmp, KnownFormats.Gif, KnownFormats.WebP];

    private static readonly FileFormat[] Lossless = [KnownFormats.Png, KnownFormats.Tiff, KnownFormats.Bmp, KnownFormats.Gif, KnownFormats.Svg];

    /// <summary>Formatos de imagen que se pueden reconocer.</summary>
    public static readonly FileFormat[] Supported = [.. Native, KnownFormats.Heic, KnownFormats.Avif, KnownFormats.Svg];

    /// <summary>Ruta lista para Tesseract y su resolución.</summary>
    public static (string Path, int Dpi) Prepare(string path, FileFormat format, ITempWorkspace workspace)
    {
        var dpi = DpiOf(path);
        if (Native.Contains(format) && !VipsImaging.HasOrientation(path))
        {
            return (path, dpi);
        }

        // Sin pérdida para capturas y gráficos; JPEG de alta calidad para fotos.
        if (Lossless.Contains(format))
        {
            var png = workspace.NewFilePath(".png");
            VipsImaging.SaveAsPng(path, png);
            return (png, dpi);
        }

        var jpeg = workspace.NewFilePath(".jpg");
        VipsImaging.SaveAsJpeg(path, jpeg, quality: 95);
        return (jpeg, dpi);
    }

    /// <summary>
    /// Resolución declarada en la imagen. Las fotos de teléfono suelen decir 72 ppp (o nada), lo que
    /// para Tesseract es "texto gigante": por debajo de 150 se usa <see cref="TesseractEngine.DefaultDpi"/>.
    /// </summary>
    private static int DpiOf(string path)
    {
        var dpi = VipsImaging.Dpi(path);
        return dpi is >= 150 and <= 1200 ? (int)Math.Round(dpi.Value) : TesseractEngine.DefaultDpi;
    }
}

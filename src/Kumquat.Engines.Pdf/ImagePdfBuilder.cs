// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Globalization;
using System.Text;
using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;
using Kumquat.Core.Results;
using Kumquat.Engines.Vips;

namespace Kumquat.Engines.Pdf;

/// <summary>
/// Arma un PDF con una imagen por página usando <c>mutool create</c>, que incrusta los JPEG tal
/// cual (sin recodificar: un JPG de 400 KB da un PDF de ~400 KB, no de 12 MB). Las fotos con
/// orientación EXIF se enderezan antes, porque los visores de PDF ignoran esa etiqueta.
/// </summary>
public sealed class ImagePdfBuilder
{
    /// <summary>Formatos de imagen aceptados.</summary>
    public static readonly FileFormat[] Inputs =
        [KnownFormats.Jpg, KnownFormats.Png, KnownFormats.Gif, KnownFormats.Tiff, KnownFormats.WebP, KnownFormats.Heic, KnownFormats.Avif];

    /// <summary>Resolución supuesta para el tamaño de página (96 ppp: 1 px = 0,75 pt).</summary>
    private const double PointsPerPixel = 72.0 / 96.0;

    private readonly PdfEngines _engines;
    private readonly ITempWorkspaceFactory _workspaces;

    /// <summary>Crea el constructor de PDF.</summary>
    public ImagePdfBuilder(PdfEngines engines, ITempWorkspaceFactory workspaces)
    {
        _engines = engines ?? throw new ArgumentNullException(nameof(engines));
        _workspaces = workspaces ?? throw new ArgumentNullException(nameof(workspaces));
    }

    /// <summary>Indica si mutool está instalado.</summary>
    public bool IsAvailable => _engines.Mutool is not null && VipsConverter.IsLibraryAvailable;

    /// <summary>Crea <paramref name="outputPdf"/> con las imágenes en el orden dado.</summary>
    public async Task<Result> BuildAsync(IReadOnlyList<(string Path, FileFormat Format)> images, string outputPdf, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(images);
        using var workspace = _workspaces.Create();
        var pages = new List<string>(images.Count);
        foreach (var (path, format) in images)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var embeddable = await Task.Run(() => PrepareImage(path, format, workspace), cancellationToken);
            var (width, height) = await Task.Run(() => VipsImaging.OrientedSize(embeddable), cancellationToken);
            var pageFile = workspace.NewFilePath(".txt");
            await File.WriteAllTextAsync(pageFile, PageContent(embeddable, width, height), new UTF8Encoding(false), cancellationToken);
            pages.Add(pageFile);
        }

        return await _engines.RunMutoolAsync(["create", "-o", PdfEngines.Abs(outputPdf), .. pages], cancellationToken);
    }

    /// <summary>
    /// JPEG sin orientación y PNG/GIF/TIFF se incrustan tal cual; el resto (o JPEG girado) se
    /// pasa a JPEG de alta calidad con la orientación aplicada.
    /// </summary>
    private static string PrepareImage(string path, FileFormat format, ITempWorkspace workspace)
    {
        var embeddable = format == KnownFormats.Jpg || format == KnownFormats.Png || format == KnownFormats.Gif || format == KnownFormats.Tiff;
        var passthrough = embeddable && !VipsImaging.HasOrientation(path);
        if (passthrough)
        {
            return PdfEngines.Abs(path);
        }

        var prepared = workspace.NewFilePath(".jpg");
        VipsImaging.SaveAsJpeg(path, prepared, quality: 95);
        return prepared;
    }

    private static string PageContent(string image, int width, int height)
    {
        var w = (width * PointsPerPixel).ToString("0.##", CultureInfo.InvariantCulture);
        var h = (height * PointsPerPixel).ToString("0.##", CultureInfo.InvariantCulture);
        return $"%%MediaBox 0 0 {w} {h}\n%%Image Im0 {image}\nq {w} 0 0 {h} 0 0 cm /Im0 Do Q\n";
    }
}

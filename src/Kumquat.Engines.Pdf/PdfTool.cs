// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Formats;
using Kumquat.Core.Results;
using Kumquat.Core.Tools;
using Kumquat.Engines.Vips;

namespace Kumquat.Engines.Pdf;

/// <summary>Herramientas de PDF (qpdf + MuPDF) y "unir imágenes en un PDF".</summary>
public sealed class PdfTool : ITool
{
    private static readonly FileFormat[] PdfOnly = [KnownFormats.Pdf];

    private readonly FileFormat[] _appliesTo;
    private readonly FileFormat? _output;
    private readonly Func<bool> _available;
    private readonly Func<ToolStep, CancellationToken, Task<Result>> _apply;

    private PdfTool(string id, string label, string suffix, FileFormat[] appliesTo, FileFormat? output, Func<bool> available, Func<ToolStep, CancellationToken, Task<Result>> apply, ToolMode mode = ToolMode.EachFile, int minInputs = 1)
    {
        Id = id;
        Label = label;
        OutputSuffix = suffix;
        _appliesTo = appliesTo;
        _output = output;
        _available = available;
        _apply = apply;
        Mode = mode;
        MinInputs = minInputs;
    }

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public string Label { get; }

    /// <inheritdoc />
    public string OutputSuffix { get; }

    /// <inheritdoc />
    public bool IsAvailable => _available();

    /// <inheritdoc />
    public ToolMode Mode { get; }

    /// <inheritdoc />
    public int MinInputs { get; }

    /// <summary>Herramientas en el orden de la rueda.</summary>
    public static IReadOnlyList<ITool> CreateAll(PdfEngines engines, ImagePdfBuilder images)
    {
        ArgumentNullException.ThrowIfNull(engines);
        ArgumentNullException.ThrowIfNull(images);
        bool HasMutool() => engines.Mutool is not null;
        bool HasQpdf() => engines.Qpdf is not null;

        return
        [
            // Unir: varios PDF en el orden natural de sus nombres.
            new PdfTool("pdf.merge", "Unir PDF", "unido", PdfOnly, KnownFormats.Pdf, HasQpdf,
                (step, ct) => engines.RunQpdfAsync(["--empty", "--pages", .. step.Inputs.Select(i => PdfEngines.Abs(i.Path)), "--", PdfEngines.Abs(step.OutputPath)], ct),
                ToolMode.Combine, minInputs: 2),

            // Comprimir: reduce imágenes de más de 225 ppp a 150 ppp (JPEG calidad 70), compacta
            // objetos y comprime flujos. Es lo que hace "comprimir PDF" en los servicios web.
            new PdfTool("pdf.compress", "Comprimir", "comprimido", PdfOnly, KnownFormats.Pdf, HasMutool,
                (step, ct) => engines.RunMutoolAsync(
                [
                    "clean", "-gggg", "-z", "-f", "-i", "-Z",
                    "--color-image-subsample-method", "bicubic", "--color-image-subsample-dpi", "225,150", "--color-image-recompress-method", "jpeg:70",
                    "--gray-image-subsample-method", "bicubic", "--gray-image-subsample-dpi", "225,150", "--gray-image-recompress-method", "jpeg:70",
                    PdfEngines.Abs(step.InputPath), PdfEngines.Abs(step.OutputPath),
                ], ct)),

            new PdfTool("pdf.rotate", "Rotar 90°", "rotado", PdfOnly, KnownFormats.Pdf, HasQpdf,
                (step, ct) => engines.RunQpdfAsync([PdfEngines.Abs(step.InputPath), PdfEngines.Abs(step.OutputPath), "--rotate=+90"], ct)),

            // Dividir: una carpeta con un PDF por página (informe-01.pdf, informe-02.pdf...).
            new PdfTool("pdf.split", "Dividir páginas", "páginas", PdfOnly, null, HasQpdf,
                (step, ct) => engines.RunQpdfAsync(["--split-pages", PdfEngines.Abs(step.InputPath), Path.Combine(step.OutputPath, Path.GetFileNameWithoutExtension(step.InputPath) + ".pdf")], ct)),

            // Páginas a JPG: MuPDF renderiza PNG a 150 ppp y libvips los pasa a JPEG calidad 90.
            new PdfTool("pdf.to-jpg", "Páginas a JPG", "JPG", PdfOnly, null, () => HasMutool() && VipsConverter.IsLibraryAvailable,
                (step, ct) => PagesToJpegAsync(engines, step, ct)),

            // Unir imágenes en un PDF (una por página).
            new PdfTool("image.to-pdf", "Unir en PDF", "PDF", ImagePdfBuilder.Inputs, KnownFormats.Pdf, () => images.IsAvailable,
                (step, ct) => images.BuildAsync([.. step.Inputs.Select(i => (i.Path, i.Format!))], step.OutputPath, ct),
                ToolMode.Combine, minInputs: 2),
        ];
    }

    /// <inheritdoc />
    public bool AppliesTo(FileFormat format) => _appliesTo.Contains(format);

    /// <inheritdoc />
    public FileFormat? OutputFormat(FileFormat? input) => _output;

    /// <inheritdoc />
    public Task<Result> ApplyAsync(ToolStep step, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);
        return _apply(step, cancellationToken);
    }

    private static async Task<Result> PagesToJpegAsync(PdfEngines engines, ToolStep step, CancellationToken cancellationToken)
    {
        var stem = Path.GetFileNameWithoutExtension(step.InputPath);
        var pattern = Path.Combine(step.OutputPath, $"{stem}-%03d.png");
        var rendered = await engines.RunMutoolAsync(["draw", "-r", "150", "-o", pattern, PdfEngines.Abs(step.InputPath)], cancellationToken);
        if (!rendered.IsSuccess)
        {
            return rendered;
        }

        foreach (var png in Directory.EnumerateFiles(step.OutputPath, "*.png").ToList())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Run(() => VipsImaging.PngToJpeg(png, Path.ChangeExtension(png, ".jpg"), quality: 90), cancellationToken);
            File.Delete(png);
        }

        return Result.Success;
    }
}

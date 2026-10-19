// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Text;
using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;
using Kumquat.Core.Results;
using Kumquat.Core.Tools;
using Kumquat.Engines.Pdf;

namespace Kumquat.Engines.Ocr;

/// <summary>Herramientas de reconocimiento de texto (OCR) con Tesseract.</summary>
public sealed class OcrTool : ITool
{
    private readonly FileFormat[] _appliesTo;
    private readonly FileFormat _output;
    private readonly Func<bool> _available;
    private readonly Func<ToolStep, IProgress<double>?, CancellationToken, Task<Result>> _apply;

    private OcrTool(string id, string label, string suffix, FileFormat[] appliesTo, FileFormat output, Func<bool> available, Func<ToolStep, IProgress<double>?, CancellationToken, Task<Result>> apply)
    {
        Id = id;
        Label = label;
        OutputSuffix = suffix;
        _appliesTo = appliesTo;
        _output = output;
        _available = available;
        _apply = apply;
    }

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public string Label { get; }

    /// <inheritdoc />
    public string OutputSuffix { get; }

    /// <inheritdoc />
    public bool IsAvailable => _available();

    /// <summary>Herramientas en el orden de la rueda.</summary>
    public static IReadOnlyList<ITool> CreateAll(TesseractEngine tesseract, PdfEngines pdf, ITempWorkspaceFactory workspaces)
    {
        ArgumentNullException.ThrowIfNull(tesseract);
        ArgumentNullException.ThrowIfNull(pdf);
        ArgumentNullException.ThrowIfNull(workspaces);
        var pdfOcr = new PdfOcr(tesseract, pdf, workspaces);
        bool CanReadPdf() => tesseract.IsAvailable && pdf.Mutool is not null && pdf.Qpdf is not null;
        return
        [
            new OcrTool("image.ocr-text", "Extraer texto", "texto", OcrImage.Supported, KnownFormats.Txt, () => tesseract.IsAvailable, (step, _, ct) => RecognizeImageAsync(tesseract, workspaces, step, "txt", ct)),
            new OcrTool("image.ocr-pdf", "PDF buscable", "OCR", OcrImage.Supported, KnownFormats.Pdf, () => tesseract.IsAvailable, (step, _, ct) => RecognizeImageAsync(tesseract, workspaces, step, "pdf", ct)),
            new OcrTool("pdf.ocr", "Reconocer texto", "OCR", [KnownFormats.Pdf], KnownFormats.Pdf, CanReadPdf, (step, progress, ct) => pdfOcr.RunAsync(step.InputPath, step.OutputPath, progress, ct)),
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
        return _apply(step, progress, cancellationToken);
    }

    private static async Task<Result> RecognizeImageAsync(TesseractEngine tesseract, ITempWorkspaceFactory workspaces, ToolStep step, string format, CancellationToken cancellationToken)
    {
        using var workspace = workspaces.Create();
        var (image, dpi) = OcrImage.Prepare(step.InputPath, step.InputFormat, workspace);

        // Tesseract agrega la extensión: la base es la ruta de salida sin ".txt"/".pdf".
        var result = await tesseract.RecognizeAsync(image, Path.ChangeExtension(step.OutputPath, null), format, dpi, cancellationToken);
        if (!result.IsSuccess || !string.Equals(format, "txt", StringComparison.Ordinal))
        {
            return result;
        }

        // Tesseract termina cada página con un salto de página (\f): sobra en un .txt.
        var text = (await File.ReadAllTextAsync(step.OutputPath, cancellationToken)).Replace("\f", string.Empty, StringComparison.Ordinal).Trim();
        if (text.Length == 0)
        {
            return new Error("no-text-found", "No se encontró texto en la imagen.");
        }

        await File.WriteAllTextAsync(step.OutputPath, text + Environment.NewLine, new UTF8Encoding(false), cancellationToken);
        return Result.Success;
    }
}

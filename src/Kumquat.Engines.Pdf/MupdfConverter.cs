// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;
using Kumquat.Core.Results;

namespace Kumquat.Engines.Pdf;

/// <summary>
/// Conversiones con <c>mutool convert</c>: imagen → PDF, PDF/EPUB → texto, EPUB → PDF.
/// </summary>
public sealed class MupdfConverter : IConverter
{
    /// <summary>Identificador del convertidor.</summary>
    public const string ConverterId = "mupdf";

    private readonly PdfEngines _engines;
    private readonly ImagePdfBuilder _images;

    /// <summary>Crea el convertidor.</summary>
    public MupdfConverter(PdfEngines engines, ImagePdfBuilder images)
    {
        _engines = engines ?? throw new ArgumentNullException(nameof(engines));
        _images = images ?? throw new ArgumentNullException(nameof(images));
        Edges =
        [
            .. ImagePdfBuilder.Inputs.Select(f => new ConversionEdge(f, KnownFormats.Pdf, ConverterId, 1.2)),
            new ConversionEdge(KnownFormats.Pdf, KnownFormats.Txt, ConverterId, 1.5),
            new ConversionEdge(KnownFormats.Epub, KnownFormats.Pdf, ConverterId, 1.5),
            new ConversionEdge(KnownFormats.Epub, KnownFormats.Txt, ConverterId, 1.5),
        ];
    }

    /// <inheritdoc />
    public string Id => ConverterId;

    /// <inheritdoc />
    public bool IsAvailable => _engines.Mutool is not null;

    /// <inheritdoc />
    public IReadOnlyCollection<ConversionEdge> Edges { get; }

    /// <inheritdoc />
    public async Task<Result> ConvertAsync(ConversionStep step, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (step.Edge.From.Category == FormatCategory.Image)
        {
            return await _images.BuildAsync([(step.InputPath, step.Edge.From)], step.OutputPath, cancellationToken);
        }

        var toText = step.Edge.To == KnownFormats.Txt;
        var result = await _engines.RunMutoolAsync(["convert", "-F", toText ? "text" : "pdf", "-o", PdfEngines.Abs(step.OutputPath), PdfEngines.Abs(step.InputPath)], cancellationToken);

        // Un PDF escaneado (solo imágenes) daría un TXT vacío: mejor decirlo.
        if (result.IsSuccess && toText && string.IsNullOrWhiteSpace(await File.ReadAllTextAsync(step.OutputPath, cancellationToken)))
        {
            return new Error("no-text", "El documento no contiene texto seleccionable (¿es un escaneo? necesitarías OCR).");
        }

        return result;
    }
}

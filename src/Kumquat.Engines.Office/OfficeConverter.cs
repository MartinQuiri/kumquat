// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;
using Kumquat.Core.Results;
using Kumquat.Infrastructure.Processes;

namespace Kumquat.Engines.Office;

/// <summary>
/// Documentos de oficina → PDF y PDF → Word, con lo que haya instalado: para cada familia se prefiere
/// Microsoft Office (la mejor fidelidad con sus propios formatos) y si no está, LibreOffice.
/// </summary>
public sealed class OfficeConverter : IConverter
{
    /// <summary>Identificador del convertidor.</summary>
    public const string ConverterId = "office";

    /// <summary>Documentos de texto → PDF (Word o LibreOffice Writer).</summary>
    public static readonly FileFormat[] TextDocuments = [KnownFormats.Docx, KnownFormats.Doc, KnownFormats.Odt, KnownFormats.Rtf];

    /// <summary>Hojas de cálculo → PDF (Excel o LibreOffice Calc).</summary>
    public static readonly FileFormat[] Spreadsheets = [KnownFormats.Xlsx, KnownFormats.Xls, KnownFormats.Ods];

    /// <summary>Presentaciones → PDF (PowerPoint o LibreOffice Impress).</summary>
    public static readonly FileFormat[] Presentations = [KnownFormats.Pptx, KnownFormats.Ppt, KnownFormats.Odp];

    private readonly Dictionary<(FileFormat From, FileFormat To), Func<ConversionStep, CancellationToken, Task<Result>>> _routes = [];

    /// <summary>Crea el convertidor con las aplicaciones detectadas.</summary>
    public OfficeConverter(OfficeApps apps, IProcessRunner runner, ITempWorkspaceFactory workspaces)
    {
        Apps = apps ?? throw new ArgumentNullException(nameof(apps));
        var microsoft = new MicrosoftOffice(runner, workspaces);
        var libre = apps.LibreOffice is { } soffice ? new LibreOffice(soffice, runner, workspaces) : null;

        Route(TextDocuments, apps.Word ? "Word" : null, microsoft, libre);
        Route(Spreadsheets, apps.Excel ? "Excel" : null, microsoft, libre);
        Route(Presentations, apps.PowerPoint ? "PowerPoint" : null, microsoft, libre);

        // PDF → Word: Word reconstruye párrafos y tablas; LibreOffice lo abre como dibujo (peor), por eso cuesta más.
        if (apps.Word)
        {
            _routes[(KnownFormats.Pdf, KnownFormats.Docx)] = (step, ct) => microsoft.ConvertAsync("Word", step.InputPath, step.OutputPath, "docx", ct);
        }
        else if (libre is not null)
        {
            _routes[(KnownFormats.Pdf, KnownFormats.Docx)] = (step, ct) => libre.ConvertAsync(step.InputPath, step.OutputPath, "docx:MS Word 2007 XML", "writer_pdf_import", ct);
        }

        Edges = [.. _routes.Keys.Select(k => new ConversionEdge(k.From, k.To, ConverterId, k.From == KnownFormats.Pdf ? (apps.Word ? 2.0 : 3.0) : 1.5))];
    }

    /// <inheritdoc />
    public string Id => ConverterId;

    /// <summary>Aplicaciones detectadas.</summary>
    public OfficeApps Apps { get; }

    /// <inheritdoc />
    public bool IsAvailable => _routes.Count > 0;

    /// <inheritdoc />
    public IReadOnlyCollection<ConversionEdge> Edges { get; }

    /// <inheritdoc />
    public Task<Result> ConvertAsync(ConversionStep step, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);
        return _routes.TryGetValue((step.Edge.From, step.Edge.To), out var convert)
            ? convert(step, cancellationToken)
            : Task.FromResult<Result>(new Error("no-route", $"No hay cómo convertir {step.Edge.From.DisplayName} a {step.Edge.To.DisplayName}."));
    }

    private void Route(FileFormat[] formats, string? microsoftApp, MicrosoftOffice microsoft, LibreOffice? libre)
    {
        foreach (var format in formats)
        {
            if (microsoftApp is not null)
            {
                _routes[(format, KnownFormats.Pdf)] = (step, ct) => microsoft.ConvertAsync(microsoftApp, step.InputPath, step.OutputPath, "pdf", ct);
            }
            else if (libre is not null)
            {
                _routes[(format, KnownFormats.Pdf)] = (step, ct) => libre.ConvertAsync(step.InputPath, step.OutputPath, "pdf", null, ct);
            }
        }
    }
}

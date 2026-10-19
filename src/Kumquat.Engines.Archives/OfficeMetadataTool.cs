// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using Kumquat.Core.Formats;
using Kumquat.Core.Results;
using Kumquat.Core.Tools;

namespace Kumquat.Engines.Archives;

/// <summary>
/// Autor, título y demás propiedades de Word, Excel y PowerPoint (Office Open XML). Solo se
/// reescribe <c>docProps/core.xml</c> dentro del paquete: el contenido del documento no cambia.
/// </summary>
public sealed class OfficeMetadataTool : ITool
{
    private const string CorePart = "docProps/core.xml";

    private static readonly XNamespace Cp = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace DcTerms = "http://purl.org/dc/terms/";
    private static readonly XNamespace Ct = "http://schemas.openxmlformats.org/package/2006/content-types";
    private static readonly XNamespace Rels = "http://schemas.openxmlformats.org/package/2006/relationships";

    private static readonly FileFormat[] Supported = [KnownFormats.Docx, KnownFormats.Xlsx, KnownFormats.Pptx];

    /// <summary>Clave → (elemento XML, etiqueta visible).</summary>
    private static readonly (string Key, XName Element, string Label)[] Fields =
    [
        ("title", Dc + "title", "Título"),
        ("creator", Dc + "creator", "Autor"),
        ("subject", Dc + "subject", "Asunto"),
        ("keywords", Cp + "keywords", "Palabras clave"),
        ("description", Dc + "description", "Comentarios"),
        ("category", Cp + "category", "Categoría"),
        ("lastModifiedBy", Cp + "lastModifiedBy", "Modificado por"),
    ];

    /// <inheritdoc />
    public string Id => "office.metadata";

    /// <inheritdoc />
    public string Label => "Metadatos";

    /// <inheritdoc />
    public string OutputSuffix => "metadatos";

    /// <inheritdoc />
    public bool IsAvailable => true;

    /// <inheritdoc />
    public bool AppliesTo(FileFormat format) => Supported.Contains(format);

    /// <inheritdoc />
    public FileFormat? OutputFormat(FileFormat? input) => input;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ToolParameter>> GetParametersAsync(IReadOnlyList<ToolInput> inputs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var current = await ReadAsync(inputs[0].Path, cancellationToken);
        return [.. Fields.Select(f => new ToolParameter(f.Key, f.Label, ToolParameterKind.Text, current.GetValueOrDefault(f.Key, string.Empty)))];
    }

    /// <inheritdoc />
    public async Task<Result> ApplyAsync(ToolStep step, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);
        try
        {
            File.Copy(step.InputPath, step.OutputPath);
            await using var zip = await ZipFile.OpenAsync(step.OutputPath, ZipArchiveMode.Update, cancellationToken);
            var core = await LoadCoreAsync(zip, cancellationToken) ?? await CreateCoreAsync(zip, cancellationToken);
            foreach (var (key, element, _) in Fields)
            {
                if (step.Option(key) is not { } value)
                {
                    continue;
                }

                core.Root!.Elements(element).Remove();
                if (value.Trim().Length > 0)
                {
                    core.Root.Add(new XElement(element, value.Trim()));
                }
            }

            zip.GetEntry(CorePart)?.Delete();
            await WriteXmlAsync(zip, CorePart, core, cancellationToken);
            return Result.Success;
        }
        catch (InvalidDataException)
        {
            return new Error("office-invalid", "El documento está dañado o no es un archivo de Office válido.");
        }
        catch (XmlException)
        {
            return new Error("office-invalid", "Las propiedades del documento están dañadas.");
        }
    }

    /// <summary>Propiedades actuales (vacío si el documento no tiene).</summary>
    internal static async Task<Dictionary<string, string>> ReadAsync(string path, CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            await using var zip = await ZipFile.OpenReadAsync(path, cancellationToken);
            if (await LoadCoreAsync(zip, cancellationToken) is { Root: { } root })
            {
                foreach (var (key, element, _) in Fields)
                {
                    if (root.Element(element)?.Value is { Length: > 0 } value)
                    {
                        values[key] = value;
                    }
                }
            }
        }
        catch (InvalidDataException)
        {
            // Documento dañado: se edita desde cero y el error real aparece al aplicar.
        }
        catch (XmlException)
        {
            // Idem.
        }

        return values;
    }

    private static async Task<XDocument?> LoadCoreAsync(ZipArchive zip, CancellationToken cancellationToken)
    {
        if (zip.GetEntry(CorePart) is not { } entry)
        {
            return null;
        }

        await using var stream = await entry.OpenAsync(cancellationToken);
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, Async = true };
        using var reader = XmlReader.Create(stream, settings);
        return await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);
    }

    /// <summary>Documento sin propiedades (raro): se crea la parte y se registra en el paquete.</summary>
    private static async Task<XDocument> CreateCoreAsync(ZipArchive zip, CancellationToken cancellationToken)
    {
        await UpdateXmlAsync(zip, "[Content_Types].xml", doc => doc.Root!.Add(new XElement(Ct + "Override", new XAttribute("PartName", "/" + CorePart), new XAttribute("ContentType", "application/vnd.openxmlformats-package.core-properties+xml"))), cancellationToken);
        await UpdateXmlAsync(zip, "_rels/.rels", doc => doc.Root!.Add(new XElement(Rels + "Relationship", new XAttribute("Id", "rIdKumquatCore"), new XAttribute("Type", "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties"), new XAttribute("Target", CorePart))), cancellationToken);
        return new XDocument(new XElement(Cp + "coreProperties", new XAttribute(XNamespace.Xmlns + "cp", Cp), new XAttribute(XNamespace.Xmlns + "dc", Dc), new XAttribute(XNamespace.Xmlns + "dcterms", DcTerms)));
    }

    private static async Task UpdateXmlAsync(ZipArchive zip, string part, Action<XDocument> change, CancellationToken cancellationToken)
    {
        var entry = zip.GetEntry(part) ?? throw new InvalidDataException($"Falta {part}.");
        XDocument doc;
        await using (var stream = await entry.OpenAsync(cancellationToken))
        {
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, Async = true });
            doc = await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);
        }

        change(doc);
        entry.Delete();
        await WriteXmlAsync(zip, part, doc, cancellationToken);
    }

    private static async Task WriteXmlAsync(ZipArchive zip, string part, XDocument doc, CancellationToken cancellationToken)
    {
        await using var stream = await zip.CreateEntry(part, CompressionLevel.Optimal).OpenAsync(cancellationToken);
        await using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Async = true, Encoding = new System.Text.UTF8Encoding(false) });
        await doc.SaveAsync(writer, cancellationToken);
    }
}

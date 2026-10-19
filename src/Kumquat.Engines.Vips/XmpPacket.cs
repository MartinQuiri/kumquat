// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Xml;
using System.Xml.Linq;

namespace Kumquat.Engines.Vips;

/// <summary>
/// Lee y edita los campos Dublin Core de un paquete XMP (autor, título, descripción, derechos)
/// conservando el resto del paquete. XMP es Unicode: es donde viven los nombres con tildes.
/// </summary>
internal static class XmpPacket
{
    private static readonly XNamespace X = "adobe:ns:meta/";
    private static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace XmlNs = "http://www.w3.org/XML/1998/namespace";

    /// <summary>Campo de Kumquat → elemento Dublin Core.</summary>
    public static readonly (string Key, string Element)[] Map =
        [("author", "creator"), ("title", "title"), ("description", "description"), ("copyright", "rights")];

    /// <summary>Valores actuales de los campos (vacío si no hay XMP o no es legible).</summary>
    public static Dictionary<string, string> Read(string? xmp)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (Parse(xmp) is not { } doc)
        {
            return values;
        }

        foreach (var (key, element) in Map)
        {
            var value = doc.Descendants(Dc + element).Descendants(Rdf + "li").Select(li => li.Value).FirstOrDefault();
            if (!string.IsNullOrEmpty(value))
            {
                values[key] = value;
            }
        }

        return values;
    }

    /// <summary>Aplica cambios (valor vacío = quitar el campo) y devuelve el paquete serializado.</summary>
    public static string Update(string? xmp, IReadOnlyDictionary<string, string> changes)
    {
        var doc = Parse(xmp) ?? new XDocument(new XElement(X + "xmpmeta", new XAttribute(XNamespace.Xmlns + "x", X), new XElement(Rdf + "RDF", new XAttribute(XNamespace.Xmlns + "rdf", Rdf))));
        var rdf = doc.Descendants(Rdf + "RDF").First();
        var description = rdf.Elements(Rdf + "Description").FirstOrDefault(d => d.Element(Dc + "creator") is not null || d.Attribute(XNamespace.Xmlns + "dc") is not null);
        if (description is null)
        {
            description = new XElement(Rdf + "Description", new XAttribute(Rdf + "about", string.Empty), new XAttribute(XNamespace.Xmlns + "dc", Dc));
            rdf.Add(description);
        }

        foreach (var (key, element) in Map)
        {
            if (!changes.TryGetValue(key, out var value))
            {
                continue;
            }

            // El campo puede estar en cualquier rdf:Description: se quita de todas y se escribe en una.
            rdf.Descendants(Dc + element).Remove();
            if (value.Length > 0)
            {
                description.Add(string.Equals(element, "creator", StringComparison.Ordinal)
                    ? new XElement(Dc + element, new XElement(Rdf + "Seq", new XElement(Rdf + "li", value)))
                    : new XElement(Dc + element, new XElement(Rdf + "Alt", new XElement(Rdf + "li", new XAttribute(XmlNs + "lang", "x-default"), value))));
            }
        }

        return "<?xpacket begin=\"﻿\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>" + doc.Root!.ToString(SaveOptions.DisableFormatting) + "<?xpacket end=\"w\"?>";
    }

    private static XDocument? Parse(string? xmp)
    {
        if (string.IsNullOrWhiteSpace(xmp))
        {
            return null;
        }

        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using var reader = XmlReader.Create(new StringReader(xmp.Trim('\0', ' ', '\r', '\n')), settings);
            var doc = XDocument.Load(reader);
            return doc.Descendants(Rdf + "RDF").Any() ? doc : null;
        }
        catch (XmlException)
        {
            return null;
        }
    }
}

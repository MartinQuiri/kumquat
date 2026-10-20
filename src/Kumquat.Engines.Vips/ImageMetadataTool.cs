// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Text;
using System.Text.RegularExpressions;
using Kumquat.Core.Formats;
using Kumquat.Core.Localization;
using Kumquat.Core.Results;
using Kumquat.Core.Tools;
using NetVips;

namespace Kumquat.Engines.Vips;

/// <summary>
/// Autor, título, descripción y copyright de fotos. Se escriben en tres lugares para que todos
/// los programas los vean: XMP (Unicode, el estándar moderno), los campos XP de Windows (Unicode,
/// los que muestra el Explorador) y EXIF clásico (solo ASCII: se omite si el texto tiene tildes,
/// porque el Explorador lo prioriza y mostraría "Maria" en vez de "María").
/// En JPEG la imagen no se recomprime: solo se reemplazan los bloques de metadatos.
/// </summary>
public sealed partial class ImageMetadataTool : ITool
{
    private static readonly FileFormat[] Supported = [KnownFormats.Jpg, KnownFormats.Png, KnownFormats.WebP, KnownFormats.Tiff, KnownFormats.Avif];

    private static (string Key, string Label)[] Fields =>
        [("author", Strings.FieldAuthor), ("title", Strings.FieldTitle), ("description", Strings.FieldDescription), ("copyright", Strings.FieldCopyright)];

    /// <inheritdoc />
    public string Id => "image.metadata";

    /// <inheritdoc />
    public string Label => Strings.ToolMetadata;

    /// <inheritdoc />
    public string OutputSuffix => Strings.SuffixMetadata;

    /// <inheritdoc />
    public bool IsAvailable => VipsConverter.IsLibraryAvailable;

    /// <inheritdoc />
    public bool AppliesTo(FileFormat format) => Supported.Contains(format);

    /// <inheritdoc />
    public FileFormat? OutputFormat(FileFormat? input) => input;

    /// <inheritdoc />
    public Task<IReadOnlyList<ToolParameter>> GetParametersAsync(IReadOnlyList<ToolInput> inputs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        return Task.Run<IReadOnlyList<ToolParameter>>(
            () =>
            {
                var current = Read(inputs[0].Path);
                return [.. Fields.Select(f => new ToolParameter(f.Key, f.Label, ToolParameterKind.Text, current.GetValueOrDefault(f.Key, string.Empty)))];
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result> ApplyAsync(ToolStep step, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);
        var changes = (step.Options ?? new Dictionary<string, string>(StringComparer.Ordinal))
            .Where(kv => Fields.Any(f => string.Equals(f.Key, kv.Key, StringComparison.Ordinal)))
            .ToDictionary(kv => kv.Key, kv => kv.Value.Trim(), StringComparer.Ordinal);
        return Task.Run(
            async () =>
            {
                try
                {
                    if (step.InputFormat == KnownFormats.Jpg)
                    {
                        await WriteJpegLosslessAsync(step.InputPath, step.OutputPath, changes, cancellationToken);
                    }
                    else
                    {
                        Resave(step.InputPath, step.OutputPath, step.InputFormat, changes);
                    }

                    return Result.Success;
                }
                catch (VipsException ex)
                {
                    return new Error("engine-failed", $"libvips: {ex.Message.Trim()}");
                }
                catch (InvalidDataException ex)
                {
                    return new Error("engine-failed", ex.Message);
                }
            },
            cancellationToken);
    }

    /// <summary>Lectura expuesta para pruebas.</summary>
    public static IReadOnlyDictionary<string, string> ReadForTests(string path) => Read(path);

    /// <summary>Valores actuales: XMP primero; si no, campos XP de Windows; si no, EXIF.</summary>
    internal static Dictionary<string, string> Read(string path)
    {
        using var image = Image.NewFromFile(path, access: Enums.Access.Sequential);
        var values = XmpPacket.Read(XmpText(image));
        void Fallback(string key, params string[] fields)
        {
            if (!values.ContainsKey(key) && fields.Select(f => ExifValue(image, f)).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) is { } value)
            {
                values[key] = value;
            }
        }

        Fallback("author", "exif-ifd0-XPAuthor", "exif-ifd0-Artist");
        Fallback("title", "exif-ifd0-XPTitle");
        Fallback("description", "exif-ifd0-XPComment", "exif-ifd0-ImageDescription");
        Fallback("copyright", "exif-ifd0-Copyright");
        return values;
    }

    /// <summary>Campos EXIF/XP que corresponden a cada campo de Kumquat: (campo, solo ASCII).</summary>
    private static (string Field, bool AsciiOnly)[] ExifFieldsFor(string key) => key switch
    {
        "author" => [("exif-ifd0-XPAuthor", false), ("exif-ifd0-Artist", true)],
        "title" => [("exif-ifd0-XPTitle", false)],
        "description" => [("exif-ifd0-XPComment", false), ("exif-ifd0-ImageDescription", true)],
        "copyright" => [("exif-ifd0-Copyright", true)],
        _ => [],
    };

    /// <summary>Aplica los cambios a los campos de metadatos de una imagen de libvips.</summary>
    private static Image ApplyFields(Image image, IReadOnlyDictionary<string, string> changes)
    {
        var xmp = XmpPacket.Update(XmpText(image), changes);
        return image.Mutate(m =>
        {
            m.Set(GValue.BlobType, "xmp-data", Encoding.UTF8.GetBytes(xmp));
            foreach (var (key, value) in changes)
            {
                foreach (var (field, asciiOnly) in ExifFieldsFor(key))
                {
                    var writable = value.Length > 0 && (!asciiOnly || Ascii.IsValid(value));
                    if (writable)
                    {
                        m.Set(GValue.GStrType, field, value);
                    }
                    else if (m.Contains(field))
                    {
                        m.Remove(field);
                    }
                }
            }
        });
    }

    private static void Resave(string input, string output, FileFormat format, IReadOnlyDictionary<string, string> changes)
    {
        using var image = Image.NewFromFile(input);
        using var updated = ApplyFields(image, changes);
        var options = format.Id switch
        {
            "webp" => new VOption { { "Q", 95 } },
            "avif" => new VOption { { "Q", 80 }, { "compression", Enums.ForeignHeifCompression.Av1 } },
            "tiff" => new VOption { { "compression", Enums.ForeignTiffCompression.Deflate } },
            _ => [],
        };
        updated.WriteToFile(output, options);
    }

    /// <summary>
    /// libvips serializa el EXIF nuevo guardando una imagen negra del mismo tamaño con los campos
    /// actualizados; de ese JPEG auxiliar se toma solo el bloque EXIF y se inserta en el original.
    /// </summary>
    private static async Task WriteJpegLosslessAsync(string input, string output, IReadOnlyDictionary<string, string> changes, CancellationToken cancellationToken)
    {
        var original = await File.ReadAllBytesAsync(input, cancellationToken);
        using var source = Image.NewFromFile(input, access: Enums.Access.Sequential);
        using var black = Image.Black(source.Width, source.Height).Copy(xres: source.Xres, yres: source.Yres);
        using var carrier = black.Mutate(m =>
        {
            foreach (var field in source.GetFields().Where(f => f.StartsWith("exif-", StringComparison.Ordinal) || string.Equals(f, "xmp-data", StringComparison.Ordinal)))
            {
                m.Set(source.GetTypeOf(field), field, source.Get(field));
            }
        });
        using var updated = ApplyFields(carrier, changes);
        var exif = JpegSegments.FindExif(updated.JpegsaveBuffer(q: 50));
        var xmp = JpegSegments.XmpPayload(Encoding.UTF8.GetString((byte[])updated.Get("xmp-data")));
        await File.WriteAllBytesAsync(output, JpegSegments.Replace(original, exif, xmp), cancellationToken);
    }

    private static string? XmpText(Image image)
        => image.Contains("xmp-data") && image.Get("xmp-data") is byte[] bytes ? Encoding.UTF8.GetString(bytes) : null;

    /// <summary>libvips muestra los campos EXIF como "valor (valor, Tipo, n components, n bytes)".</summary>
    private static string? ExifValue(Image image, string field)
    {
        if (!image.Contains(field) || image.Get(field) is not string text)
        {
            return null;
        }

        var match = VipsExifString().Match(text);
        return (match.Success ? match.Groups["v"].Value : text).Trim('\0', ' ');
    }

    [GeneratedRegex(@"^(?<v>.*) \(\k<v>, [A-Za-z ]+, \d+ components?, \d+ bytes?\)$", RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 200)]
    private static partial Regex VipsExifString();
}

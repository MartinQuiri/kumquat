// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Collections.Concurrent;
using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;
using Kumquat.Infrastructure.Processes;

namespace Kumquat.Engines.Tests.Integration;

/// <summary>
/// Corpus sintético: todos los archivos de prueba se generan con fuentes lavfi de FFmpeg
/// (testsrc2, sine). Así el repo no carga binarios ni archivos con licencias de terceros.
/// </summary>
internal static class Corpus
{
    public const int Width = 320;
    public const int Height = 240;
    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(2);

    private static readonly string Root = Path.Combine(Path.GetTempPath(), "kumquat-corpus", Guid.NewGuid().ToString("N"));
    private static readonly ConcurrentDictionary<string, Lazy<Task<string?>>> Samples = new(StringComparer.Ordinal);

    private const string SvgSample =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"320\" height=\"240\" viewBox=\"0 0 320 240\">" +
        "<rect width=\"320\" height=\"240\" fill=\"#ff8c00\"/><circle cx=\"160\" cy=\"120\" r=\"80\" fill=\"#2e7d32\"/></svg>\n";

    /// <summary>Muestra del formato pedido, o <c>null</c> si el entorno no puede generarla.</summary>
    public static Task<string?> GetAsync(FileFormat format)
        => Samples.GetOrAdd(format.Id, _ => new Lazy<Task<string?>>(() => CreateAsync(format))).Value;

    private static async Task<string?> CreateAsync(FileFormat format)
    {
        Directory.CreateDirectory(Root);
        var path = Path.Combine(Root, "sample" + format.DefaultExtension);

        switch (format.Id)
        {
            case "wav":
                return await LavfiAsync(path, ["-f", "lavfi", "-i", $"sine=frequency=440:duration={Duration.TotalSeconds}", "-c:a", "pcm_s16le"]);
            case "mp4":
                return await LavfiAsync(path,
                [
                    "-f", "lavfi", "-i", $"testsrc2=size={Width}x{Height}:rate=25:duration={Duration.TotalSeconds}",
                    "-f", "lavfi", "-i", $"sine=frequency=440:duration={Duration.TotalSeconds}",
                    "-shortest", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac",
                ]);
            case "png":
                return await LavfiAsync(path, ["-f", "lavfi", "-i", $"testsrc2=size={Width}x{Height}", "-frames:v", "1", "-update", "1"]);
            case "svg":
                await File.WriteAllTextAsync(path, SvgSample);
                return path;
            case "txt":
                await File.WriteAllTextAsync(path, "Kumquat\nMuestra de texto con ñ y tildes: canción.\n");
                return path;
            case "pdf":
                return await TwoPagePdfAsync(path);
            case "epub":
                MinimalEpub(path);
                return path;
            case "zip" or "tar" or "gz":
                return await ArchiveAsync(format, path);
        }

        // El resto se deriva de las bases con los propios convertidores.
        var baseFormat = format.Category switch
        {
            FormatCategory.Audio => KnownFormats.Wav,
            FormatCategory.Video => KnownFormats.Mp4,
            _ when format == KnownFormats.Gif => KnownFormats.Mp4,
            FormatCategory.Image => KnownFormats.Png,
            _ => null,
        };
        if (baseFormat is null)
        {
            return null;
        }

        var source = await GetAsync(baseFormat);
        var edge = TestEngines.All.Where(c => c.IsAvailable).SelectMany(c => c.Edges).FirstOrDefault(e => e.From == baseFormat && e.To == format);
        if (source is null || edge is null)
        {
            return null;
        }

        var result = await TestEngines.ById(edge.ConverterId).ConvertAsync(new ConversionStep(source, path, edge), null, CancellationToken.None);
        return result.IsSuccess ? path : null;
    }

    /// <summary>PDF de 2 páginas con imagen y texto: dividir, renderizar y extraer texto tienen qué hacer.</summary>
    private static async Task<string?> TwoPagePdfAsync(string path)
    {
        var png = await GetAsync(KnownFormats.Png);
        if (png is null || TestEngines.Pdf.Mutool is null)
        {
            return null;
        }

        var pages = new List<string>();
        for (var n = 1; n <= 2; n++)
        {
            var page = Path.Combine(Root, $"pagina{n}.txt");
            await File.WriteAllTextAsync(page, $"%%MediaBox 0 0 400 400\n%%Font F1 Helvetica\n%%Image Im0 {png}\nq 320 0 0 240 40 120 cm /Im0 Do Q\nBT /F1 18 Tf 40 60 Td (Kumquat pagina {n}) Tj ET\n");
            pages.Add(page);
        }

        var result = await TestEngines.Pdf.RunMutoolAsync(["create", "-o", path, .. pages], CancellationToken.None);
        return result.IsSuccess ? path : null;
    }

    /// <summary>EPUB 3 mínimo válido escrito a mano (mimetype sin comprimir, container, OPF y un capítulo).</summary>
    private static void MinimalEpub(string path)
    {
        using var zip = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Create);
        void Add(string name, string content, System.IO.Compression.CompressionLevel level = System.IO.Compression.CompressionLevel.Optimal)
        {
            using var writer = new StreamWriter(zip.CreateEntry(name, level).Open());
            writer.Write(content);
        }

        Add("mimetype", "application/epub+zip", System.IO.Compression.CompressionLevel.NoCompression);
        Add("META-INF/container.xml", "<?xml version=\"1.0\"?><container version=\"1.0\" xmlns=\"urn:oasis:names:tc:opendocument:xmlns:container\"><rootfiles><rootfile full-path=\"content.opf\" media-type=\"application/oebps-package+xml\"/></rootfiles></container>");
        Add("content.opf", "<?xml version=\"1.0\"?><package xmlns=\"http://www.idpf.org/2007/opf\" version=\"3.0\" unique-identifier=\"id\"><metadata xmlns:dc=\"http://purl.org/dc/elements/1.1/\"><dc:identifier id=\"id\">kumquat-test</dc:identifier><dc:title>Muestra</dc:title><dc:language>es</dc:language></metadata><manifest><item id=\"c1\" href=\"c1.xhtml\" media-type=\"application/xhtml+xml\"/></manifest><spine><itemref idref=\"c1\"/></spine></package>");
        Add("c1.xhtml", "<?xml version=\"1.0\"?><html xmlns=\"http://www.w3.org/1999/xhtml\"><head><title>Capítulo</title></head><body><h1>Capítulo uno</h1><p>Kumquat convierte libros.</p></body></html>");
    }

    /// <summary>ZIP/TAR con dos archivos de texto, o GZIP de un TAR (.tar.gz).</summary>
    private static async Task<string?> ArchiveAsync(FileFormat format, string path)
    {
        var folder = Directory.CreateDirectory(Path.Combine(Root, "archive-src-" + format.Id)).FullName;
        await File.WriteAllTextAsync(Path.Combine(folder, "uno.txt"), "uno");
        await File.WriteAllTextAsync(Path.Combine(folder, "dos.txt"), "dos");
        if (format == KnownFormats.Zip)
        {
            await using var zip = await System.IO.Compression.ZipFile.OpenAsync(path, System.IO.Compression.ZipArchiveMode.Create);
            await System.IO.Compression.ZipFileExtensions.CreateEntryFromFileAsync(zip, Path.Combine(folder, "uno.txt"), "uno.txt");
            await System.IO.Compression.ZipFileExtensions.CreateEntryFromFileAsync(zip, Path.Combine(folder, "dos.txt"), "carpeta/dos.txt");
            return path;
        }

        var tar = format == KnownFormats.Tar ? path : Path.Combine(Root, "muestra.tar");
        await System.Formats.Tar.TarFile.CreateFromDirectoryAsync(folder, tar, includeBaseDirectory: false);
        if (format == KnownFormats.Gzip)
        {
            await using var source = File.OpenRead(tar);
            await using var target = File.Create(path);
            await using var gzip = new System.IO.Compression.GZipStream(target, System.IO.Compression.CompressionLevel.Optimal);
            await source.CopyToAsync(gzip);
        }

        return path;
    }

    private static async Task<string?> LavfiAsync(string output, IReadOnlyList<string> args)
    {
        if (TestEngines.FfmpegExe is not { } ffmpeg)
        {
            return null;
        }

        var outcome = await TestEngines.Runner.RunAsync(
            new ProcessSpec { FileName = ffmpeg, Arguments = ["-hide_banner", "-loglevel", "error", "-y", .. args, output], Timeout = TimeSpan.FromMinutes(2) },
            CancellationToken.None);
        return outcome.Succeeded ? output : throw new InvalidOperationException($"No se pudo generar {output}: {outcome.StandardErrorTail}");
    }
}

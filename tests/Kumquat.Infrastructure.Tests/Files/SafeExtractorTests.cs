// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.IO.Compression;
using Kumquat.Infrastructure.Files;

namespace Kumquat.Infrastructure.Tests.Files;

public sealed class SafeExtractorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kumquat-tests", Guid.NewGuid().ToString("N"));

    public SafeExtractorTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task Extrae_ArchivoNormal()
    {
        var zip = CreateZip(("a.txt", "hola"), ("sub/b.txt", "chao"));

        var result = await SafeExtractor.ExtractZipAsync(zip, Path.Combine(_root, "out"), null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
        result.Value.Count.ShouldBe(2);
        (await File.ReadAllTextAsync(Path.Combine(_root, "out", "sub", "b.txt"), TestContext.Current.CancellationToken)).ShouldBe("chao");
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("sub/../../escape.txt")]
    [InlineData("..\\escape.txt")]
    public async Task ZipSlip_SeRechazaSinEscribirNada(string entryName)
    {
        var zip = CreateZip(("ok.txt", "x"), (entryName, "malicioso"));

        var result = await SafeExtractor.ExtractZipAsync(zip, Path.Combine(_root, "out"), null, TestContext.Current.CancellationToken);

        result.Error.ShouldNotBeNull().Code.ShouldBe("archive-path-traversal");
        File.Exists(Path.Combine(_root, "escape.txt")).ShouldBeFalse();
        File.Exists(Path.Combine(_root, "out", "ok.txt")).ShouldBeFalse();
    }

    [Fact]
    public async Task Bomba_RazonDeCompresionExcesiva_SeRechaza()
    {
        var zip = CreateZip(("ceros.bin", new string('\0', 10 * 1024 * 1024)));

        var result = await SafeExtractor.ExtractZipAsync(zip, Path.Combine(_root, "out"), null, TestContext.Current.CancellationToken);

        result.Error.ShouldNotBeNull().Code.ShouldBe("archive-bomb");
    }

    [Fact]
    public async Task DemasiadasEntradas_SeRechaza()
    {
        var zip = CreateZip([.. Enumerable.Range(0, 20).Select(i => ($"{i}.txt", "x"))]);

        var result = await SafeExtractor.ExtractZipAsync(zip, Path.Combine(_root, "out"), new ExtractionLimits(MaxEntries: 10), TestContext.Current.CancellationToken);

        result.Error.ShouldNotBeNull().Code.ShouldBe("archive-too-many-entries");
    }

    [Fact]
    public async Task TamanoTotalExcedido_SeRechaza()
    {
        var zip = CreateZip(("a.txt", new string('a', 4096)));

        var result = await SafeExtractor.ExtractZipAsync(zip, Path.Combine(_root, "out"), new ExtractionLimits(MaxTotalBytes: 1000, MaxCompressionRatio: 1e9), TestContext.Current.CancellationToken);

        result.Error.ShouldNotBeNull().Code.ShouldBe("archive-too-large");
    }

    [Theory]
    [InlineData("roto.zip", new byte[] { 0x50, 0x4B, 0x03, 0x04, 0xFF, 0xFF, 0x00 })]
    [InlineData("roto.gz", new byte[] { 0x1F, 0x8B, 0x08, 0x00, 0xDE, 0xAD })]
    [InlineData("nada.7z", new byte[] { 0x01, 0x02, 0x03 })]
    public async Task ArchivoDanado_DevuelveErrorSinLanzar(string name, byte[] content)
    {
        var path = Path.Combine(_root, name);
        await File.WriteAllBytesAsync(path, content, TestContext.Current.CancellationToken);

        var result = await SafeExtractor.ExtractAsync(path, Path.Combine(_root, "out"), null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task Gzip_DeUnArchivoSuelto_ConservaSuNombre()
    {
        var path = Path.Combine(_root, "notas.txt.gz");
        await using (var file = File.Create(path))
        await using (var gzip = new GZipStream(file, CompressionLevel.Optimal))
        await using (var writer = new StreamWriter(gzip))
        {
            await writer.WriteAsync("hola gzip");
        }

        var result = await SafeExtractor.ExtractAsync(path, Path.Combine(_root, "out"), null, TestContext.Current.CancellationToken);

        result.Value.ShouldHaveSingleItem().ShouldEndWith("notas.txt");
        (await File.ReadAllTextAsync(result.Value![0], TestContext.Current.CancellationToken)).ShouldBe("hola gzip");
    }

    private string CreateZip(params (string Name, string Content)[] entries)
    {
        var path = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".zip");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, content) in entries)
        {
            var entry = archive.CreateEntry(name, CompressionLevel.SmallestSize);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }

        return path;
    }
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Infrastructure.Files;

namespace Kumquat.Infrastructure.Tests.Files;

public sealed class FileTransferTests : IDisposable
{
    private readonly string _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "kumquat-tests", Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task Copiar_Archivo_ConservaElOriginal()
    {
        var source = Write("origen/foto.jpg", "x");
        var destination = Dir("destino");

        var result = await FileTransfer.CopyAsync(source, destination, TestContext.Current.CancellationToken);

        result.Value.ShouldBe(Path.Combine(destination, "foto.jpg"));
        File.Exists(source).ShouldBeTrue();
    }

    [Fact]
    public async Task Copiar_NombreOcupado_NoSobrescribe()
    {
        var source = Write("origen/foto.jpg", "nuevo");
        Write("destino/foto.jpg", "viejo");

        var result = await FileTransfer.CopyAsync(source, Path.Combine(_root, "destino"), TestContext.Current.CancellationToken);

        result.Value.ShouldBe(Path.Combine(_root, "destino", "foto (2).jpg"));
        (await File.ReadAllTextAsync(Path.Combine(_root, "destino", "foto.jpg"), TestContext.Current.CancellationToken)).ShouldBe("viejo");
    }

    [Fact]
    public async Task Mover_Archivo_LoQuitaDelOrigen()
    {
        var source = Write("origen/doc.pdf", "x");

        var result = await FileTransfer.MoveAsync(source, Dir("destino"), TestContext.Current.CancellationToken);

        File.Exists(source).ShouldBeFalse();
        File.Exists(result.Value!).ShouldBeTrue();
    }

    [Fact]
    public async Task Copiar_Carpeta_EsRecursivo()
    {
        Write("proyecto/a.txt", "a");
        Write("proyecto/sub/b.txt", "b");

        var result = await FileTransfer.CopyAsync(Path.Combine(_root, "proyecto"), Dir("respaldo"), TestContext.Current.CancellationToken);

        (await File.ReadAllTextAsync(Path.Combine(result.Value!, "sub", "b.txt"), TestContext.Current.CancellationToken)).ShouldBe("b");
    }

    [Fact]
    public async Task CarpetaDentroDeSiMisma_SeRechaza()
    {
        var folder = Dir("proyecto");
        var inside = Dir("proyecto/sub");

        var result = await FileTransfer.CopyAsync(folder, inside, TestContext.Current.CancellationToken);

        result.Error.ShouldNotBeNull().Code.ShouldBe("destination-inside-source");
    }

    [Fact]
    public async Task Mover_ALaMismaCarpeta_NoHaceNada()
    {
        var source = Write("aqui/x.txt", "x");

        var result = await FileTransfer.MoveAsync(source, Path.Combine(_root, "aqui"), TestContext.Current.CancellationToken);

        result.Value.ShouldBe(Path.GetFullPath(source));
        File.Exists(source).ShouldBeTrue();
    }

    [Fact]
    public async Task DestinoInexistente_DevuelveError()
    {
        var source = Write("x.txt", "x");

        var result = await FileTransfer.CopyAsync(source, Path.Combine(_root, "no-existe"), TestContext.Current.CancellationToken);

        result.Error.ShouldNotBeNull().Code.ShouldBe("destination-missing");
    }

    private string Write(string relative, string content)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private string Dir(string relative) => Directory.CreateDirectory(Path.Combine(_root, relative)).FullName;
}

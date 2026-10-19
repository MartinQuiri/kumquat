// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;
using Kumquat.Core.Results;

namespace Kumquat.Core.Tests.Conversion;

/// <summary>
/// La promesa central de Kumquat: nunca sobrescribir nada ni dejar archivos a medias con el
/// nombre final, pase lo que pase con el motor.
/// </summary>
public sealed class StagedOutputTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("kumquat-staged-").FullName;

    private string Input => Path.Combine(_dir, "foto.png");

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task Exito_PublicaConElNombreFinalYNoDejaTemporales()
    {
        var result = await RunAsync(path => Write(path, "nuevo"));

        result.Value.ShouldBe(Path.Combine(_dir, "foto.jpg"));
        (await File.ReadAllTextAsync(result.Value!, TestContext.Current.CancellationToken)).ShouldBe("nuevo");
        Hidden().ShouldBeEmpty();
    }

    [Fact]
    public async Task NombreOcupado_UsaElSiguienteLibre()
    {
        await File.WriteAllTextAsync(Path.Combine(_dir, "foto.jpg"), "original", TestContext.Current.CancellationToken);

        var result = await RunAsync(path => Write(path, "nuevo"));

        result.Value.ShouldBe(Path.Combine(_dir, "foto (2).jpg"));
        (await File.ReadAllTextAsync(Path.Combine(_dir, "foto.jpg"), TestContext.Current.CancellationToken)).ShouldBe("original");
    }

    [Fact]
    public async Task CarreraPorElNombre_NoPisaElArchivoQueAparecio()
    {
        // Otro proceso crea "foto.jpg" entre que Kumquat elige el nombre y lo publica: se simula
        // con una comprobación que la primera vez dice "libre" aunque el archivo exista.
        await File.WriteAllTextAsync(Path.Combine(_dir, "foto.jpg"), "de otro programa", TestContext.Current.CancellationToken);
        var lies = 1;
        bool Exists(string path) => (lies-- <= 0 || !path.EndsWith("foto.jpg", StringComparison.Ordinal)) && File.Exists(path);

        var result = await RunAsync(path => Write(path, "nuevo"), Exists);

        result.Value.ShouldBe(Path.Combine(_dir, "foto (2).jpg"));
        (await File.ReadAllTextAsync(Path.Combine(_dir, "foto.jpg"), TestContext.Current.CancellationToken)).ShouldBe("de otro programa");
        Hidden().ShouldBeEmpty();
    }

    [Fact]
    public async Task CarreraQueNoTermina_FallaSinPisarNada()
    {
        await File.WriteAllTextAsync(Path.Combine(_dir, "foto.jpg"), "original", TestContext.Current.CancellationToken);

        var result = await RunAsync(path => Write(path, "nuevo"), _ => false); // siempre "libre": siempre choca

        result.IsSuccess.ShouldBeFalse();
        result.Error!.Code.ShouldBe("io");
        (await File.ReadAllTextAsync(Path.Combine(_dir, "foto.jpg"), TestContext.Current.CancellationToken)).ShouldBe("original");
        Hidden().ShouldBeEmpty();
    }

    [Fact]
    public async Task MotorFalla_NoQuedaNiFinalNiTemporal()
    {
        var result = await RunAsync(async path =>
        {
            await File.WriteAllTextAsync(path, "a medias", TestContext.Current.CancellationToken);
            return new Error("engine", "falló a la mitad");
        });

        result.Error!.Code.ShouldBe("engine");
        File.Exists(Path.Combine(_dir, "foto.jpg")).ShouldBeFalse();
        Hidden().ShouldBeEmpty();
    }

    [Fact]
    public async Task MotorNoProduceNada_ErrorClaro()
        => (await RunAsync(_ => Task.FromResult(Result.Success))).Error!.Code.ShouldBe("no-output");

    [Fact]
    public async Task Cancelado_LimpiaElTemporal()
    {
        using var cancel = new CancellationTokenSource();
        var result = await StagedOutput.RunAsync(Input, KnownFormats.Jpg, _dir, null, async path =>
        {
            await File.WriteAllTextAsync(path, "a medias", TestContext.Current.CancellationToken);
            await cancel.CancelAsync();
            cancel.Token.ThrowIfCancellationRequested();
            return Result.Success;
        }, cancel.Token);

        result.Error.ShouldBe(Error.Cancelled);
        Directory.GetFiles(_dir).ShouldBeEmpty();
    }

    [Fact]
    public async Task Carpeta_SePublicaEntera_YNoPisaOtra()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "foto"));

        var result = await StagedOutput.RunDirectoryAsync(Input, _dir, suffix: null, async folder =>
        {
            await File.WriteAllTextAsync(Path.Combine(folder, "1.jpg"), "x", TestContext.Current.CancellationToken);
            return Result.Success;
        }, TestContext.Current.CancellationToken);

        result.Value.ShouldBe(Path.Combine(_dir, "foto (2)"));
        File.Exists(Path.Combine(result.Value!, "1.jpg")).ShouldBeTrue();
        Directory.GetFileSystemEntries(Path.Combine(_dir, "foto")).ShouldBeEmpty();
        Hidden().ShouldBeEmpty();
    }

    [Fact]
    public async Task CarpetaVacia_ErrorYSinRestos()
    {
        var result = await StagedOutput.RunDirectoryAsync(Input, _dir, suffix: null, _ => Task.FromResult(Result.Success), TestContext.Current.CancellationToken);

        result.Error!.Code.ShouldBe("no-output");
        Directory.GetFileSystemEntries(_dir).ShouldBeEmpty();
    }

    private static async Task<Result> Write(string path, string text)
    {
        await File.WriteAllTextAsync(path, text, TestContext.Current.CancellationToken);
        return Result.Success;
    }

    private Task<Result<string>> RunAsync(Func<string, Task<Result>> produce, Func<string, bool>? exists = null)
        => StagedOutput.RunAsync(Input, KnownFormats.Jpg, _dir, suffix: null, produce, TestContext.Current.CancellationToken, exists);

    private string[] Hidden() => Directory.GetFileSystemEntries(_dir, ".kumquat-*");
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Shelf;

namespace Kumquat.Core.Tests.Shelf;

public sealed class ShelfContentsTests
{
    private static readonly string Dir = Path.Combine(OperatingSystem.IsWindows() ? @"C:\" : "/", "fotos");

    private static string At(string name) => Path.Combine(Dir, name);

    [Fact]
    public void Agregar_SinRepetidos_EnOrdenDeLlegada()
    {
        var shelf = new ShelfContents();

        var first = shelf.Add([At("a.jpg"), At("b.pdf")]);
        var second = shelf.Add([At("b.pdf"), At("c.mp4"), "  "]);

        first.ShouldBe(2);
        second.ShouldBe(1, "b.pdf ya estaba y una ruta vacía no cuenta");
        shelf.Items.ShouldBe([At("a.jpg"), At("b.pdf"), At("c.mp4")]);
    }

    [Fact]
    public void Agregar_MismaRutaConOtrasMayusculas_EnWindowsEsLaMisma()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Solo Windows no distingue mayúsculas en las rutas.");
        var shelf = new ShelfContents();

        shelf.Add([At("Foto.JPG")]);
        shelf.Add([At("foto.jpg")]);

        shelf.Count.ShouldBe(1);
    }

    [Fact]
    public void Quitar_SoloSacaDeLaBandeja()
    {
        var shelf = new ShelfContents();
        shelf.Add([At("a.jpg"), At("b.pdf")]);

        shelf.Remove(At("a.jpg")).ShouldBeTrue();
        shelf.Remove(At("no-estaba.txt")).ShouldBeFalse();

        shelf.Items.ShouldBe([At("b.pdf")]);
    }

    [Fact]
    public void QuitarLosQueYaNoEstan_TrasMoverlosAOtraCarpeta()
    {
        var shelf = new ShelfContents();
        shelf.Add([At("a.jpg"), At("b.pdf"), At("c.mp4")]);

        var gone = shelf.RemoveMissing(path => !path.EndsWith("b.pdf", StringComparison.Ordinal));

        gone.ShouldBe([At("b.pdf")]);
        shelf.Items.ShouldBe([At("a.jpg"), At("c.mp4")]);
    }

    [Fact]
    public void Resumen_SingularYPlural()
    {
        var shelf = new ShelfContents();
        shelf.Add([At("a.jpg")]);
        shelf.Summary.ShouldBe("1 archivo");

        shelf.Add([At("b.jpg"), At("c.jpg")]);
        shelf.Summary.ShouldBe("3 archivos");
    }
}

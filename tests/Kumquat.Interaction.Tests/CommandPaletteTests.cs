// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Windows.Input;
using Kumquat.Design;
using Kumquat.Dialogs.Search;

namespace Kumquat.Interaction.Tests;

[Collection(WpfWindows.Name)]
public sealed class CommandPaletteTests
{
    private static readonly PaletteRequest Request = new(
        "vacaciones.png",
        "PNG · 2,4 MB",
        "image",
        [
            new("jpg", "Convertir a JPG", "Convertir", "image", Palette.Kumquat, ["jpg", "jpeg"]),
            new("webp", "Convertir a WebP", "Convertir", "image", Palette.Kumquat, ["webp"]),
            new("pdf", "Convertir a PDF", "Convertir", "pdf", Palette.Kumquat, ["pdf"]),
            new("image.compress", "Comprimir", "Herramientas", "compress", Palette.Image, ["reducir", "achicar"]),
            new("image.ocr-pdf", "PDF buscable", "Herramientas", "ocr-pdf", Palette.Image, ["ocr"]),
            new("image.metadata", "Metadatos", "Herramientas", "metadata", Palette.Image, ["autor"]),
            new("action.copy-path", "Copiar ruta", "Acciones", "copy-path", Palette.Actions, ["ruta"]),
        ]);

    [Fact]
    public void SinEscribir_MuestraTodoEnElOrdenDeLaRueda()
    {
        var (titles, selected) = OnSta(palette => (palette.VisibleTitles, palette.SelectedTitle));

        titles.Count.ShouldBe(7);
        titles[0].ShouldBe("Convertir a JPG");
        selected.ShouldBe("Convertir a JPG");
    }

    [Theory]
    [InlineData("pdf", "Convertir a PDF")]
    [InlineData("achicar", "Comprimir")]
    [InlineData("jpeg", "Convertir a JPG")]
    [InlineData("autor", "Metadatos")]
    public void Escribir_FiltraYResaltaElMejor(string query, string expected)
        => OnSta(palette =>
        {
            palette.Query = query;
            return palette.SelectedTitle;
        }).ShouldBe(expected);

    [Fact]
    public void LectorDePantalla_DiceLaOpcionElegidaYSuPosicion()
    {
        var (afterQuery, afterDown, nothing) = OnSta(palette =>
        {
            palette.Query = "pdf";
            var first = Kumquat.Dialogs.Look.Announcer.Last;
            palette.PressForTests(Key.Down);
            var second = Kumquat.Dialogs.Look.Announcer.Last;
            palette.Query = "zzzz";
            return (first, second, Kumquat.Dialogs.Look.Announcer.Last);
        });

        afterQuery.ShouldBe("Convertir a PDF, Convertir, 1 de 2");
        afterDown.ShouldBe("PDF buscable, Herramientas, 2 de 2");
        nothing.ShouldStartWith("Nada coincide");
    }

    [Fact]
    public void Flechas_RecorrenConVuelta_YEnterElige()
    {
        var result = OnSta(palette =>
        {
            palette.Query = "pdf"; // Convertir a PDF, PDF buscable
            palette.PressForTests(Key.Down);
            var second = palette.SelectedTitle;
            palette.PressForTests(Key.Down); // vuelve al primero
            var wrapped = palette.SelectedTitle;
            palette.PressForTests(Key.Up);
            palette.PressForTests(Key.Enter);
            return (second, wrapped, palette.Chosen);
        });

        result.second.ShouldBe("PDF buscable");
        result.wrapped.ShouldBe("Convertir a PDF");
        result.Chosen.ShouldBe("image.ocr-pdf");
    }

    [Fact]
    public void SinCoincidencias_EnterNoEligeNada()
        => OnSta(palette =>
        {
            palette.Query = "xyzzy";
            palette.PressForTests(Key.Enter);
            return (palette.VisibleTitles.Count, palette.Chosen);
        }).ShouldBe((0, (string?)null));

    [Fact]
    public void Escape_Cancela()
        => OnSta(palette =>
        {
            palette.PressForTests(Key.Escape);
            return palette.Chosen;
        }).ShouldBeNull();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void VistaPrevia(bool dark)
    {
        var path = Path.Combine(RepoImages(), dark ? "buscador-oscuro.png" : "buscador-claro.png");
        var thread = new Thread(() => CommandPalette.RenderPreview(Request, "pdf", path, dark));
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        new FileInfo(path).Length.ShouldBeGreaterThan(1000);
    }

    private static T OnSta<T>(Func<CommandPalette, T> action)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action(CommandPalette.CreateForTests(Request));
            }
#pragma warning disable CA1031 // Se relanza en el hilo del test.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return failure is null ? result : throw new InvalidOperationException("Falló en el hilo STA.", failure);
    }

    private static string RepoImages()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Kumquat.slnx")))
        {
            dir = dir.Parent;
        }

        return Directory.CreateDirectory(Path.Combine(dir?.FullName ?? Path.GetTempPath(), "docs", "images")).FullName;
    }
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Kumquat.Dialogs.Shelf;

namespace Kumquat.Interaction.Tests;

[Collection(WpfWindows.Name)]
public sealed class ShelfWindowTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("kumquat-bandeja-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Pila_ResumenYLista()
    {
        var files = Files("foto.jpg", "informe.pdf", "notas.txt");

        var (summary, rows, expanded) = OnSta(() =>
        {
            var shelf = ShelfWindow.CreateForTests(files);
            var before = shelf.SummaryForTests;
            shelf.ToggleForTests();
            return (before, shelf.RowNamesForTests, shelf.IsExpandedForTests);
        });

        summary.ShouldBe("3 archivos");
        expanded.ShouldBeTrue();
        rows.ShouldBe(["foto.jpg", "informe.pdf", "notas.txt"]);
    }

    [Fact]
    public void UnSoloArchivo_MuestraSuNombre()
        => OnSta(() => ShelfWindow.CreateForTests(Files("contrato.pdf")).SummaryForTests).ShouldBe("contrato.pdf");

    [Fact]
    public void SoltarMasArchivos_SeAgreganSinRepetir()
    {
        var first = Files("a.jpg", "b.jpg");
        var more = Files("b.jpg", "c.jpg");

        var rows = OnSta(() =>
        {
            var shelf = ShelfWindow.CreateForTests(first);
            shelf.AddForTests(more);
            shelf.ToggleForTests();
            return shelf.RowNamesForTests;
        });

        rows.ShouldBe(["a.jpg", "b.jpg", "c.jpg"]);
    }

    [Fact]
    public void LoQueSeMueveOSeBorra_SaleDeLaBandeja_YVaciaSeCierra()
    {
        var files = Files("a.jpg", "b.pdf");

        var (afterOne, closedAfterOne, closedAtEnd) = OnSta(() =>
        {
            var shelf = ShelfWindow.CreateForTests(files);
            File.Delete(files[1]); // como si se hubiera movido a otra carpeta
            shelf.PruneForTests();
            var summary = shelf.SummaryForTests;
            var closed = shelf.IsClosedForTests;
            File.Delete(files[0]);
            shelf.PruneForTests();
            return (summary, closed, shelf.IsClosedForTests);
        });

        afterOne.ShouldBe("a.jpg");
        closedAfterOne.ShouldBeFalse();
        closedAtEnd.ShouldBeTrue("una bandeja vacía no sirve: se cierra sola");
    }

    [Fact]
    public void Carpetas_TambienSePuedenDejar()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_dir, "Fotos del viaje")).FullName;

        var rows = OnSta(() =>
        {
            var shelf = ShelfWindow.CreateForTests([folder, .. Files("a.jpg")]);
            shelf.ToggleForTests();
            return shelf.RowNamesForTests;
        });

        rows.ShouldBe(["Fotos del viaje", "a.jpg"]);
    }

    [Theory]
    [InlineData(false, "bandeja.png")]
    [InlineData(true, "bandeja-lista.png")]
    public void VistaPrevia(bool expanded, string name)
    {
        var images = RepoImages();
        string[] files = [.. new[] { "opciones-recortar.png", "rueda-clara.png", "social-preview.png" }.Select(f => Path.Combine(images, f)).Where(File.Exists)];
        Assert.SkipWhen(files.Length < 3, "Faltan las imágenes de docs/images.");
        var path = Path.Combine(images, name);

        OnSta(() =>
        {
            var shelf = ShelfWindow.CreateForTests(files, dark: true);
            if (expanded)
            {
                shelf.ToggleForTests();
            }

            foreach (var file in files)
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri(file);
                image.DecodePixelWidth = 160;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                image.Freeze();
                shelf.SetThumbnailForTests(file, image, isIcon: false);
            }

            ShelfWindow.RenderPreview(shelf, path, dark: true);
            return 0;
        });

        new FileInfo(path).Length.ShouldBeGreaterThan(1000);
    }

    private string[] Files(params string[] names)
    {
        foreach (var name in names)
        {
            File.WriteAllText(Path.Combine(_dir, name), "x");
        }

        return [.. names.Select(n => Path.Combine(_dir, n))];
    }

    private static T OnSta<T>(Func<T> action)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
#pragma warning disable CA1031 // Se relanza en el hilo del test.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                failure = ex;
            }
            finally
            {
                // Cerrar el despachador de este hilo: si no, lo que WPF dejó pendiente con las imágenes
                // de la bandeja mantiene vivo un hilo y el proceso de pruebas no termina limpio.
                Dispatcher.CurrentDispatcher.InvokeShutdown();
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

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Previews;
using Kumquat.Core.Tools;
using Kumquat.Dialogs;
using Kumquat.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Kumquat.Interaction.Tests;

[Collection(WpfWindows.Name)]
public sealed class ToolOptionsDialogTests
{
    [Fact]
    public void SinCambios_SoloEnviaTiemposYContraseñas_NoLosTextos()
    {
        var values = OnStaThread(() => ToolOptionsDialog.CreateForTests(
        [
            new ToolParameter("author", "Autor", ToolParameterKind.Text, "Ana"),
            new ToolParameter("start", "Desde", ToolParameterKind.Time, "0:00"),
            new ToolParameter("password", "Contraseña", ToolParameterKind.Password, "x"),
            new ToolParameter("custom", "Campos personalizados", ToolParameterKind.CustomFields),
        ]).Collect());

        // Un texto no modificado no se envía: al editar metadatos de varios archivos no se pisan sus valores.
        values.ShouldNotContainKey("author");
        values["start"].ShouldBe("0:00");
        values["password"].ShouldBe("x");
        values.Keys.ShouldNotContain(k => k.StartsWith(ToolParameter.CustomPrefix, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void VistaPrevia_Metadatos(bool dark)
    {
        var path = Path.Combine(RepoImages(), dark ? "opciones-oscuro.png" : "opciones-claro.png");
        OnStaThread(() =>
        {
            ToolOptionsDialog.RenderPreview(
                new ToolDialogRequest("office.metadata", "Metadatos", "informe.docx",
                [
                    new ToolParameter("Title", "Título", ToolParameterKind.Text, "Informe anual"),
                    new ToolParameter("Author", "Autor", ToolParameterKind.Text, "María Pérez"),
                    new ToolParameter("Subject", "Asunto", ToolParameterKind.Text, string.Empty),
                    new ToolParameter("Keywords", "Palabras clave", ToolParameterKind.Text, "ventas, 2026"),
                ], _ => null),
                preview: null,
                path,
                dark);
            return 0;
        });

        new FileInfo(path).Length.ShouldBeGreaterThan(1000);
    }

    [Fact]
    public async Task VistaPrevia_ExtraerPaginas_ConMiniaturas()
    {
        var pdf = await SamplePdfAsync(pages: 7);
        var preview = await Previews().CreateAsync(pdf, TestContext.Current.CancellationToken);
        preview.Kind.ShouldBe(PreviewKind.Pages);
        var path = Path.Combine(RepoImages(), "opciones-paginas.png");
        OnStaThread(() =>
        {
            ToolOptionsDialog.RenderPreview(
                new ToolDialogRequest("pdf.extract", "Extraer páginas", "presentación.pdf",
                    [new ToolParameter("pages", "Páginas", ToolParameterKind.Pages, "1-2, 5", "Ej.: 1-3, 5, 8-fin", Required: true)], _ => null)
                {
                    Preview = _ => Task.FromResult(preview),
                },
                preview,
                path,
                dark: true);
            return 0;
        });

        new FileInfo(path).Length.ShouldBeGreaterThan(1000);
    }

    [Fact]
    public async Task VistaPrevia_RecortarVideo_ConTiraYTramo()
    {
        var video = await SampleVideoAsync();
        var preview = await Previews().CreateAsync(video, TestContext.Current.CancellationToken);
        preview.Kind.ShouldBe(PreviewKind.Video);
        var path = Path.Combine(RepoImages(), "opciones-recortar.png");
        OnStaThread(() =>
        {
            ToolOptionsDialog.RenderPreview(
                new ToolDialogRequest("media.trim", "Recortar", "vacaciones.mp4",
                [
                    new ToolParameter("start", "Desde", ToolParameterKind.Time, "0:02", "Minutos:segundos, p. ej. 0:15", Required: true),
                    new ToolParameter("end", "Hasta", ToolParameterKind.Time, "0:07", "Duración total: 0:10", Required: true),
                ], _ => null)
                {
                    Preview = _ => Task.FromResult(preview),
                },
                preview,
                path,
                dark: false);
            return 0;
        });

        new FileInfo(path).Length.ShouldBeGreaterThan(1000);
    }

    private static IPreviewService Previews()
        => new ServiceCollection().AddKumquat(logToFile: false).BuildServiceProvider().GetRequiredService<IPreviewService>();

    /// <summary>PDF de varias páginas de colores, hecho con FFmpeg + mutool (sin archivos de terceros).</summary>
    private static async Task<string> SamplePdfAsync(int pages)
    {
        var dir = Directory.CreateTempSubdirectory("kumquat-dlg-").FullName;
        var images = new List<(string, Kumquat.Core.Formats.FileFormat)>();
        for (var i = 0; i < pages; i++)
        {
            var png = Path.Combine(dir, $"p{i}.png");
            await RunFfmpegAsync(["-f", "lavfi", "-i", $"testsrc2=size=595x842:rate=1,hue=h={i * 50}", "-frames:v", "1", "-update", "1", png]);
            images.Add((png, Kumquat.Core.Formats.KnownFormats.Png));
        }

        var pdf = Path.Combine(dir, "muestra.pdf");
        using var services = new ServiceCollection().AddKumquat(logToFile: false).BuildServiceProvider();
        var builder = services.GetRequiredService<Kumquat.Engines.Pdf.ImagePdfBuilder>();
        (await builder.BuildAsync(images, pdf, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        return pdf;
    }

    private static async Task<string> SampleVideoAsync()
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory("kumquat-dlg-").FullName, "muestra.mp4");
        await RunFfmpegAsync(
        [
            "-f", "lavfi", "-i", "testsrc2=size=640x360:rate=25:duration=10", "-f", "lavfi", "-i", "sine=frequency=330:duration=10",
            "-shortest", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", path,
        ]);
        return path;
    }

    private static async Task RunFfmpegAsync(IReadOnlyList<string> arguments)
    {
        var ffmpeg = new Kumquat.Infrastructure.Engines.EngineLocator().Find("ffmpeg", Path.Combine("bin", "ffmpeg.exe"));
        Assert.SkipWhen(ffmpeg is null, "FFmpeg no está instalado (tools/fetch-engines.ps1).");
        var outcome = await new Kumquat.Infrastructure.Processes.ProcessRunner().RunAsync(
            new Kumquat.Infrastructure.Processes.ProcessSpec { FileName = ffmpeg, Arguments = ["-hide_banner", "-loglevel", "error", "-y", .. arguments] },
            TestContext.Current.CancellationToken);
        outcome.Succeeded.ShouldBeTrue();
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

    private static T OnStaThread<T>(Func<T> action)
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
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return failure is null ? result : throw new InvalidOperationException("Falló en el hilo STA.", failure);
    }
}

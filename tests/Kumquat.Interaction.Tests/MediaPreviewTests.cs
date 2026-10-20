// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Globalization;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Kumquat.Core.Previews;
using Kumquat.Core.Text;
using Kumquat.Core.Tools;
using Kumquat.Dialogs;
using Kumquat.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Kumquat.Interaction.Tests;

/// <summary>
/// La ventana de opciones de verdad, en el hilo de UI de la app (UiThread, como al soltar sobre
/// la rueda), con la vista previa generada por los motores: reproducir y elegir fotograma.
/// </summary>
[Collection(WpfWindows.Name)]
public sealed class MediaPreviewTests
{
    [Fact]
    public async Task Recortar_Reproducir_AvanzaYMuestraFotogramas()
    {
        var video = await SampleVideoAsync();
        var request = new ToolDialogRequest(
            "media.trim",
            "Recortar",
            "muestra.mp4",
            [new ToolParameter("start", "Desde", ToolParameterKind.Time, "0:01"), new ToolParameter("end", "Hasta", ToolParameterKind.Time, "0:04")],
            _ => null)
        {
            Preview = ct => Previews().CreateAsync(video, ct),
        };

        var log = RunDialog(request, (dialog, step) =>
        {
            var media = dialog.MediaPreviewForTests;
            if (media is null)
            {
                return step < 15 ? null : "sin reproductor";
            }

            if (step == 15)
            {
                media.PlayButtonForTests.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            }

            return step == 21 ? string.Create(CultureInfo.InvariantCulture, $"pos={media.Point.TotalSeconds:0.0};bitmap={media.ScreenForTests is WriteableBitmap};fin") : null;
        });

        // Arranca en el inicio del tramo (1 s) y en ~1,2 s de reproducción avanza en tiempo real.
        var position = double.Parse(log.Split("pos=")[1].Split(';')[0], CultureInfo.InvariantCulture);
        position.ShouldBeInRange(1.6, 3.2, log);
        log.ShouldContain("bitmap=True");
    }

    [Fact]
    public async Task Fotograma_ElegirEnLaLinea_ActualizaElCampoYLaImagen()
    {
        var video = await SampleVideoAsync();
        var request = new ToolDialogRequest(
            "video.frame",
            "Fotograma",
            "muestra.mp4",
            [new ToolParameter("time", "Instante", ToolParameterKind.Time, "0:05")],
            _ => null)
        {
            Preview = ct => Previews().CreateAsync(video, ct),
        };

        var log = RunDialog(request, (dialog, step) =>
        {
            var media = dialog.MediaPreviewForTests;
            if (media is null)
            {
                return step < 15 ? null : "sin reproductor";
            }

            if (step == 15)
            {
                media.Point.ShouldBe(TimeSpan.FromSeconds(5)); // al abrir manda el campo
                media.SeekForTests(TimeSpan.FromSeconds(7.5));
            }

            return step == 20 ? $"campo={dialog.FieldForTests("time")!.Text};bitmap={media.ScreenForTests is WriteableBitmap};fin" : null;
        });

        log.ShouldContain($"campo={TimeText.Format(TimeSpan.FromSeconds(7.5))}");
        log.ShouldContain("bitmap=True");
    }

    [Fact]
    public void VistaPreviaQueFalla_SeRegistraYLaVentanaSigueUsable()
    {
        var reported = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var previous = DialogDiagnostics.Error;
        DialogDiagnostics.Error = (activity, ex) => reported.Enqueue($"{activity}: {ex.Message}");
        try
        {
            var request = new ToolDialogRequest("media.trim", "Recortar", "roto.mp4", [new ToolParameter("start", "Desde", ToolParameterKind.Time, "0:00")], _ => null)
            {
                Preview = _ => throw new IOException("archivo dañado de prueba"),
            };

            var log = RunDialog(request, (dialog, step) => step == 5 ? $"campo={dialog.FieldForTests("start")!.IsEnabled};fin" : null);

            log.ShouldContain("campo=True");
            reported.ShouldContain(r => r.Contains("archivo dañado de prueba", StringComparison.Ordinal));
        }
        finally
        {
            DialogDiagnostics.Error = previous;
        }
    }

    private static IPreviewService Previews()
        => new ServiceCollection().AddKumquat(logToFile: false).BuildServiceProvider().GetRequiredService<IPreviewService>();

    /// <summary>
    /// Abre la ventana con <see cref="ToolOptionsDialog.Ask"/> en un <see cref="UiThread"/> y llama a
    /// <paramref name="probe"/> cada 200 ms hasta que devuelva un texto (que cierra la ventana).
    /// </summary>
    private static string RunDialog(ToolDialogRequest request, Func<ToolOptionsDialog, int, string?> probe)
    {
        var result = "sin resultado";
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            using var ui = new UiThread();
            var step = 0;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            timer.Tick += (_, _) =>
            {
                var dialog = PresentationSource.CurrentSources.OfType<HwndSource>().Select(s => s.RootVisual).OfType<ToolOptionsDialog>().FirstOrDefault();
                if (dialog is null)
                {
                    return;
                }

                string? text = null;
                try
                {
                    text = probe(dialog, ++step);
                }
#pragma warning disable CA1031 // Se relanza en el hilo del test.
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    failure = ex;
                }

                if (text is not null || failure is not null)
                {
                    result = text ?? result;
                    timer.Stop();
                    dialog.Close();
                    ui.Quit();
                }
            };
            ui.Post(() =>
            {
                timer.Start();
                ToolOptionsDialog.Ask(request);
            });
            UiThread.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(60)).ShouldBeTrue("La ventana no se cerró.");
        return failure is null ? result : throw new InvalidOperationException("Falló dentro de la ventana.", failure);
    }

    private static async Task<string> SampleVideoAsync()
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory("kumquat-media-").FullName, "muestra.mp4");
        var ffmpeg = new Kumquat.Infrastructure.Engines.EngineLocator().Find("ffmpeg", Path.Combine("bin", "ffmpeg.exe"));
        Assert.SkipWhen(ffmpeg is null, "FFmpeg no está instalado (tools/fetch-engines.ps1).");
        var outcome = await new Kumquat.Infrastructure.Processes.ProcessRunner().RunAsync(
            new Kumquat.Infrastructure.Processes.ProcessSpec
            {
                FileName = ffmpeg,
                Arguments =
                [
                    "-hide_banner", "-loglevel", "error", "-y",
                    "-f", "lavfi", "-i", "testsrc2=size=640x360:rate=25:duration=10", "-f", "lavfi", "-i", "sine=frequency=330:duration=10",
                    "-shortest", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", path,
                ],
            },
            TestContext.Current.CancellationToken);
        outcome.Succeeded.ShouldBeTrue();
        return path;
    }
}

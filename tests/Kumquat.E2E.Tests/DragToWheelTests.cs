// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Diagnostics;
using System.Drawing.Imaging;
using Kumquat.Core.Formats;

namespace Kumquat.E2E.Tests;

/// <summary>
/// Prueba de punta a punta con un arrastre OLE real: valida el riesgo central de la fase 0
/// (una ventana en capas pre-registrada recibe el drop cuando aparece a mitad del arrastre).
/// Mueve el mouse real durante unos segundos: requiere escritorio interactivo y no corre en CI.
/// </summary>
[Trait("Category", "E2E")]
public sealed class DragToWheelTests : IDisposable
{
    private const float LabelRadiusDip = (38 + 140) / 2f;

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "kumquat-e2e", Guid.NewGuid().ToString("N"));
    private Process? _app;

    public DragToWheelTests()
    {
        Directory.CreateDirectory(_dir);
        SyntheticInput.BecomePerMonitorDpiAware();
    }

    public void Dispose()
    {
        if (_app is { HasExited: false })
        {
            _app.Kill(entireProcessTree: true);
            _app.WaitForExit(5000);
        }

        _app?.Dispose();
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // Mejor esfuerzo.
        }
    }

    [Fact]
    public async Task ArrastrarPng_Shift_SoltarEnSectorNorte_CreaJpg()
    {
        Assert.SkipUnless(Environment.UserInteractive && Environment.GetEnvironmentVariable("CI") is null, "Requiere un escritorio interactivo.");
        var png = CreatePng(Path.Combine(_dir, "muestra.png"));
        StartKumquat();

        RunDragScenario(png, northSector: true);

        var expected = Path.Combine(_dir, "muestra.jpg");
        await WaitForFileAsync(expected, TimeSpan.FromSeconds(30));
        FormatSniffer.DetectFile(expected).ShouldBe(KnownFormats.Jpg);
        File.Exists(png).ShouldBeTrue("El original nunca debe moverse ni borrarse.");
    }

    [Fact]
    public async Task SoltarEnElCentro_Cancela_SinCrearArchivos()
    {
        Assert.SkipUnless(Environment.UserInteractive && Environment.GetEnvironmentVariable("CI") is null, "Requiere un escritorio interactivo.");
        var png = CreatePng(Path.Combine(_dir, "centro.png"));
        StartKumquat();

        RunDragScenario(png, northSector: false);

        await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        Directory.GetFiles(_dir).ShouldBe([png]);
    }

    private static string CreatePng(string path)
    {
        using var bitmap = new Bitmap(64, 48);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.DarkOrange);
        }

        bitmap.Save(path, ImageFormat.Png);
        return path;
    }

    private static void RunDragScenario(string file, bool northSector)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            using var form = new Form
            {
                Text = "Kumquat E2E (origen del arrastre)",
                TopMost = true,
                StartPosition = FormStartPosition.Manual,
                Location = new Point(300, 300),
                Size = new Size(260, 260),
                BackColor = Color.SteelBlue,
            };
            form.MouseDown += (_, _) =>
                form.DoDragDrop(new DataObject(DataFormats.FileDrop, new[] { file }), DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);
            form.Shown += (_, _) =>
            {
                form.Activate();
                var center = form.PointToScreen(new Point(form.ClientSize.Width / 2, form.ClientSize.Height / 2));
                var driver = new Thread(() =>
                {
                    try
                    {
                        Drive(center.X, center.Y, northSector);
                    }
#pragma warning disable CA1031 // Se reporta en el hilo del test.
                    catch (Exception ex)
#pragma warning restore CA1031
                    {
                        failure = ex;
                    }
                    finally
                    {
                        form.BeginInvoke(form.Close);
                    }
                })
                { IsBackground = true };
                driver.Start();
            };
            Application.Run(form);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(30)).ShouldBeTrue("El escenario de arrastre no terminó.");
        if (failure is not null)
        {
            throw new InvalidOperationException("Falló el escenario de arrastre.", failure);
        }
    }

    private static void Drive(int x, int y, bool northSector)
    {
        SyntheticInput.MoveTo(x, y);
        Thread.Sleep(250);
        SyntheticInput.LeftDown();
        Thread.Sleep(200);

        // Supera el umbral de arrastre del sistema para que el hook lo vea como arrastre.
        for (var i = 1; i <= 10; i++)
        {
            SyntheticInput.MoveTo(x + (i * 3), y);
            Thread.Sleep(20);
        }

        Thread.Sleep(250);
        var (cx, cy) = SyntheticInput.CursorPosition();
        SyntheticInput.TapShift();
        Thread.Sleep(500); // La rueda aparece centrada en (cx, cy).

        if (northSector)
        {
            var distance = LabelRadiusDip * SyntheticInput.DpiScaleAt(cx, cy);
            for (var i = 1; i <= 12; i++)
            {
                SyntheticInput.MoveTo(cx, cy - (int)(distance * i / 12));
                Thread.Sleep(25);
            }
        }
        else
        {
            SyntheticInput.MoveTo(cx + 1, cy);
        }

        Thread.Sleep(400);
        SyntheticInput.LeftUp();
        Thread.Sleep(500);
    }

    private static async Task WaitForFileAsync(string path, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            if (File.Exists(path))
            {
                return;
            }

            await Task.Delay(200, TestContext.Current.CancellationToken);
        }

        throw new ShouldAssertException($"No apareció {path} en {timeout.TotalSeconds} s. Revisa %LOCALAPPDATA%\\Kumquat\\logs.");
    }

    private void StartKumquat()
    {
        var exe = Path.Combine(AppContext.BaseDirectory, "Kumquat.exe");
        File.Exists(exe).ShouldBeTrue($"No se encontró {exe}");
        Process.GetProcessesByName("Kumquat").Length.ShouldBe(0, "Cierra Kumquat antes de correr los E2E (instancia única).");

        _app = Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false });
        Thread.Sleep(2500); // Arranque: hooks, ventana y registro OLE.
        _app!.HasExited.ShouldBeFalse("Kumquat se cerró al arrancar.");
    }
}

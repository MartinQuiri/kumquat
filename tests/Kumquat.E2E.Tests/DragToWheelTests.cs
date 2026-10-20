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
    /// <summary>Mitad del anillo de conversión: centro de 34 DIP + medio anillo de 56 (WheelRenderer).</summary>
    private const float LabelRadiusDip = 34 + (56 / 2f);

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

    [Fact]
    public async Task ArchivoVirtual_ComoUnAdjunto_SoltarEnJpg_QuedaEnLaCarpetaConfigurada()
    {
        Assert.SkipUnless(Environment.UserInteractive && Environment.GetEnvironmentVariable("CI") is null, "Requiere un escritorio interactivo.");

        // Lo que entrega Outlook o un navegador: nombre + contenido, sin ruta en disco.
        var png = await File.ReadAllBytesAsync(CreatePng(Path.Combine(_dir, "fuente.png")), TestContext.Current.CancellationToken);
        File.Delete(Path.Combine(_dir, "fuente.png"));
        var output = Directory.CreateDirectory(Path.Combine(_dir, "salida")).FullName;
        var data = Directory.CreateDirectory(Path.Combine(_dir, "datos")).FullName;
        await File.WriteAllTextAsync(Path.Combine(data, "settings.json"), System.Text.Json.JsonSerializer.Serialize(new { OutputFolder = output, Language = "es" }), TestContext.Current.CancellationToken);
        StartKumquat(data);

        RunDragScenario(() => VirtualPng("adjunto.png", png), northSector: true);

        var expected = Path.Combine(output, "adjunto.jpg");
        await WaitForFileAsync(expected, TimeSpan.FromSeconds(30));
        FormatSniffer.DetectFile(expected).ShouldBe(KnownFormats.Jpg);
        await Task.Delay(500, TestContext.Current.CancellationToken);
        var leftovers = Directory.Exists(Kumquat.Interaction.DragDrop.VirtualDrops.Root)
            ? Directory.EnumerateFiles(Kumquat.Interaction.DragDrop.VirtualDrops.Root, "adjunto.png", SearchOption.AllDirectories).ToList()
            : [];
        leftovers.ShouldBeEmpty("la copia temporal se borra al terminar");
    }

    /// <summary>FileGroupDescriptorW (un elemento, nombre en el byte 72 del FILEDESCRIPTORW) + FileContents.</summary>
    private static DataObject VirtualPng(string name, byte[] content)
    {
        var descriptor = new byte[4 + 592];
        BitConverter.GetBytes(1u).CopyTo(descriptor, 0);
        System.Text.Encoding.Unicode.GetBytes(name).CopyTo(descriptor, 4 + 72);
        var data = new DataObject();
        data.SetData("FileGroupDescriptorW", new MemoryStream(descriptor));
        data.SetData("FileContents", new MemoryStream(content));
        return data;
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
        => RunDragScenario(() => new DataObject(DataFormats.FileDrop, new[] { file }), northSector);

    private static void RunDragScenario(Func<DataObject> data, bool northSector)
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
            using var dragStarted = new ManualResetEventSlim();
            form.MouseDown += (_, _) =>
            {
                dragStarted.Set(); // el arrastre OLE empieza de verdad (DoDragDrop no vuelve hasta soltar)
                form.DoDragDrop(data(), DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);
            };
            form.Shown += (_, _) =>
            {
                var driver = new Thread(() =>
                {
                    try
                    {
                        // El centro se calcula recién ahora: en Shown la ventana puede no estar en su lugar final.
                        var center = (Point)form.Invoke(() =>
                        {
                            form.Activate();
                            return form.PointToScreen(new Point(form.ClientSize.Width / 2, form.ClientSize.Height / 2));
                        });
                        Drive(center.X, center.Y, northSector, dragStarted);
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
        thread.Join(TimeSpan.FromSeconds(45)).ShouldBeTrue("El escenario de arrastre no terminó.");
        if (failure is not null)
        {
            throw new InvalidOperationException("Falló el escenario de arrastre.", failure);
        }
    }

    private static void Drive(int x, int y, bool northSector, ManualResetEventSlim dragStarted)
    {
        // Si el clic sintético no llega a la ventana de origen (pasa a veces con la primera ventana
        // del proceso), no hay arrastre OLE y la rueda nunca recibiría DragEnter: se reintenta. Es
        // un problema del arnés, no de Kumquat, y así lo dice el mensaje si persiste.
        for (var attempt = 1; !dragStarted.IsSet; attempt++)
        {
            if (attempt > 3)
            {
                throw new InvalidOperationException("Arnés: el clic sintético nunca llegó a la ventana de origen, así que no hubo arrastre OLE.");
            }

            SyntheticInput.MoveTo(x, y);
            Thread.Sleep(250);
            SyntheticInput.LeftDown();
            if (!dragStarted.Wait(TimeSpan.FromMilliseconds(600)))
            {
                SyntheticInput.LeftUp();
                Thread.Sleep(400);
            }
        }

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

    private void StartKumquat(string? dataDirectory = null)
    {
        var exe = Path.Combine(AppContext.BaseDirectory, "Kumquat.exe");
        File.Exists(exe).ShouldBeTrue($"No se encontró {exe}");
        Process.GetProcessesByName("Kumquat").Length.ShouldBe(0, "Cierra Kumquat antes de correr los E2E (instancia única).");

        var start = new ProcessStartInfo(exe) { UseShellExecute = false };
        if (dataDirectory is not null)
        {
            start.Environment["KUMQUAT_DATA_DIR"] = dataDirectory; // configuración propia: no toca la de quien corre los tests
        }

        _app = Process.Start(start);
        Thread.Sleep(2500); // Arranque: hooks, ventana y registro OLE.
        _app!.HasExited.ShouldBeFalse("Kumquat se cerró al arrancar.");
    }
}

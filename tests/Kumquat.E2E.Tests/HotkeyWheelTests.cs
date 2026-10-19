// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Diagnostics;
using System.Drawing.Imaging;
using Kumquat.Core.Formats;

namespace Kumquat.E2E.Tests;

/// <summary>
/// Sin arrastrar: archivo seleccionado en el Explorador → Ctrl+Alt+K → buscador → "jpg" + Enter. Mueve el
/// mouse y presiona teclas reales: requiere escritorio interactivo y no corre en CI.
/// </summary>
[Trait("Category", "E2E")]
public sealed class HotkeyWheelTests : IDisposable
{
    private const ushort VkControl = 0x11;
    private const ushort VkAlt = 0x12;
    private const ushort VkK = 0x4B;
    private const ushort VkEnter = 0x0D;

    private readonly string _dir = Directory.CreateTempSubdirectory("kumquat-atajo-").FullName;
    private Process? _app;

    public HotkeyWheelTests() => SyntheticInput.BecomePerMonitorDpiAware();

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
            // El Explorador puede tener la carpeta abierta un momento más.
        }
    }

    [Fact]
    public async Task SeleccionarPng_CtrlAltK_EscribirJpgYEnter_CreaJpg()
    {
        Assert.SkipUnless(Environment.UserInteractive && Environment.GetEnvironmentVariable("CI") is null, "Requiere un escritorio interactivo.");
        var png = CreatePng(Path.Combine(_dir, "atajo.png"));
        StartKumquat();
        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken); // atajo registrado
        using var explorer = await ExplorerWindow.OpenSelectingAsync(png, TestContext.Current.CancellationToken);

        // La rueda aparece bajo el cursor: se lo deja en un lugar conocido, lejos de los bordes.
        var screen = Screen.PrimaryScreen!.WorkingArea;
        var (cx, cy) = (screen.Left + (screen.Width / 2), screen.Top + (screen.Height / 2));
        SyntheticInput.MoveTo(cx, cy);
        await SelectAsync(explorer, png);

        SyntheticInput.PressChord(VkControl, VkAlt, VkK);
        await Task.Delay(1200, TestContext.Current.CancellationToken); // aparece el buscador, con el foco

        // Se escribe como una persona: el buscador tiene que estar al frente para recibirlo.
        foreach (var key in "JPG")
        {
            SyntheticInput.PressChord(key);
        }

        await Task.Delay(300, TestContext.Current.CancellationToken);
        SyntheticInput.PressChord(VkEnter);

        var expected = Path.Combine(_dir, "atajo.jpg");
        var watch = Stopwatch.StartNew();
        while (!File.Exists(expected) && watch.Elapsed < TimeSpan.FromSeconds(30))
        {
            await Task.Delay(200, TestContext.Current.CancellationToken);
        }

        File.Exists(expected).ShouldBeTrue($"No apareció {expected}. Revisa %LOCALAPPDATA%\\Kumquat\\logs.");
        FormatSniffer.DetectFile(expected).ShouldBe(KnownFormats.Jpg);
        File.Exists(png).ShouldBeTrue("El original nunca debe moverse ni borrarse.");
    }

    [Fact]
    public async Task MenuContextual_KumquatWheel_AbreElBuscador_EscribirJpgYEnter_CreaJpg()
    {
        Assert.SkipUnless(Environment.UserInteractive && Environment.GetEnvironmentVariable("CI") is null, "Requiere un escritorio interactivo.");
        var png = CreatePng(Path.Combine(_dir, "menu.png"));
        StartKumquat();
        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        using var explorer = await ExplorerWindow.OpenSelectingAsync(png, TestContext.Current.CancellationToken);
        await SelectAsync(explorer, png);

        // Lo mismo que ejecuta el Explorador al elegir "Kumquat…": un segundo proceso que le pasa la
        // ruta a la instancia en ejecución y sale.
        var verb = Process.Start(new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "Kumquat.exe")) { ArgumentList = { "--wheel", png }, UseShellExecute = false })!;
        await verb.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        verb.ExitCode.ShouldBe(0);
        verb.Dispose();
        await Task.Delay(1200, TestContext.Current.CancellationToken); // junta pedidos 250 ms y abre el buscador

        foreach (var key in "JPG")
        {
            SyntheticInput.PressChord(key);
        }

        await Task.Delay(300, TestContext.Current.CancellationToken);
        SyntheticInput.PressChord(VkEnter);
        await WaitForAsync(Path.Combine(_dir, "menu.jpg"));
        File.Exists(png).ShouldBeTrue("El original nunca debe moverse ni borrarse.");
    }

    private static async Task WaitForAsync(string expected)
    {
        var watch = Stopwatch.StartNew();
        while (!File.Exists(expected) && watch.Elapsed < TimeSpan.FromSeconds(30))
        {
            await Task.Delay(200, TestContext.Current.CancellationToken);
        }

        File.Exists(expected).ShouldBeTrue($@"No apareció {expected}. Revisa %LOCALAPPDATA%\Kumquat\logs.");
        FormatSniffer.DetectFile(expected).ShouldBe(KnownFormats.Jpg);
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

    /// <summary>
    /// Precondición del uso real: el Explorador al frente con el archivo seleccionado. Con imágenes,
    /// "/select" no deja nada seleccionado (el Explorador recarga la vista con la plantilla de
    /// "Imágenes"): se selecciona con la API del shell, como un clic.
    /// </summary>
    private static async Task SelectAsync(ExplorerWindow explorer, string file)
    {
        IReadOnlyList<string> seen = [];
        for (var i = 0; i < 20; i++)
        {
            explorer.Activate();
            await Task.Delay(250, TestContext.Current.CancellationToken);
            seen = await ExplorerWindow.SelectionAsync();
            if (seen.Count == 0 && await explorer.SelectAsync(file))
            {
                await Task.Delay(300, TestContext.Current.CancellationToken);
                seen = await ExplorerWindow.SelectionAsync();
            }

            if (seen.Contains(file, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }
        }

        throw new ShouldAssertException($"El Explorador no quedó al frente con el archivo seleccionado: al frente '{ExplorerWindow.ForegroundDescription()}', selección [{string.Join(", ", seen)}]. Shell: {ExplorerWindow.LastDiagnostics}");
    }

    private void StartKumquat()
    {
        var exe = Path.Combine(AppContext.BaseDirectory, "Kumquat.exe");
        File.Exists(exe).ShouldBeTrue($"No se encontró {exe}");
        Process.GetProcessesByName("Kumquat").Length.ShouldBe(0, "Cierra Kumquat antes de correr los E2E (instancia única).");
        _app = Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false });
    }
}

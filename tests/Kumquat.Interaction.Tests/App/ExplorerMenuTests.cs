// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Collections.Concurrent;
using Kumquat.App;
using Kumquat.Interaction.Wheel;
using Microsoft.Win32;

namespace Kumquat.Interaction.Tests.App;

/// <summary>Menú contextual: se prueba en una rama propia del registro, nunca en la real.</summary>
[Collection(nameof(ExplorerMenuTests))]
public sealed class ExplorerMenuTests : IDisposable
{
    private readonly string _root = $@"Software\KumquatTests\{Guid.NewGuid():N}";
    private readonly string _previous = ExplorerMenu.ClassesRoot;

    public ExplorerMenuTests() => ExplorerMenu.ClassesRoot = _root + @"\Classes";

    public void Dispose()
    {
        ExplorerMenu.ClassesRoot = _previous;
        Registry.CurrentUser.DeleteSubKeyTree(_root, throwOnMissingSubKey: false);
    }

    [Fact]
    public void Registrar_ArchivosYCarpetas_ConComandoSeguro()
    {
        const string exe = @"C:\Program Files\Kumquat ñ\Kumquat.exe";
        ExplorerMenu.Register(exe);

        foreach (var target in new[] { @"*\shell\Kumquat", @"Directory\shell\Kumquat" })
        {
            using var verb = Registry.CurrentUser.OpenSubKey($@"{_root}\Classes\{target}");
            verb.ShouldNotBeNull(target);
            verb.GetValue("MUIVerb").ShouldBe("Kumquat…");
            using var command = verb.OpenSubKey("command");

            // Ejecutable y archivo entre comillas: rutas con espacios no se parten ni inyectan argumentos.
            command!.GetValue(null).ShouldBe(@"""C:\Program Files\Kumquat ñ\Kumquat.exe"" --wheel ""%1""");
        }

        ExplorerMenu.IsRegisteredFor(exe).ShouldBeTrue();
        ExplorerMenu.IsRegisteredFor(@"D:\otra\Kumquat.exe").ShouldBeFalse("si se movió la carpeta hay que corregir la ruta");
    }

    [Fact]
    public void Quitar_BorraTodo_YNoFallaSiNoEstaba()
    {
        ExplorerMenu.Unregister();
        ExplorerMenu.Register(@"C:\k\Kumquat.exe");
        ExplorerMenu.Unregister();

        Registry.CurrentUser.OpenSubKey($@"{_root}\Classes\*\shell\Kumquat").ShouldBeNull();
        Registry.CurrentUser.OpenSubKey($@"{_root}\Classes\Directory\shell\Kumquat").ShouldBeNull();
    }

    [Fact]
    public void PedidosDeOtroProceso_LleganALaVentanaDeLaRueda()
    {
        string[] paths = [@"C:\fotos\vacaciones ñ.jpg", @"D:\música\canción.mp3"];
        var received = new ConcurrentQueue<string>();
        var thread = new Thread(() =>
        {
            using var ui = new UiThread();
            using var window = new WheelWindow
            {
                PathRequestHandler = path =>
                {
                    received.Enqueue(path);
                    if (received.Count == paths.Length)
                    {
                        ui.Quit();
                    }
                },
            };

            // SendMessage bloquea hasta que la ventana procesa: se envía desde otro hilo.
            _ = Task.Run(() => WheelRequests.Send(window.Handle, paths));
            UiThread.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(10)).ShouldBeTrue();

        received.ShouldBe(paths);
    }
}

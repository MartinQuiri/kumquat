// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.App;
using Kumquat.Dialogs.Settings;
using Microsoft.Win32;

namespace Kumquat.Interaction.Tests;

[Collection(WpfWindows.Name)]
public sealed class SettingsDialogTests
{
    private static readonly (string Id, string Label)[] Triggers = [("Shift", "Shift (recomendado)"), ("Control", "Ctrl"), ("CapsLock", "Bloq Mayús"), ("RightClick", "Clic derecho")];

    private static readonly SettingsValues Values = new("Shift", Hotkey: true, ContextMenu: true, StartWithWindows: false, OutputFolder: @"D:\Resultados");

    [Fact]
    public void SinCambios_DevuelveLoMismo()
        => OnSta(() => SettingsDialog.CreateForTests(Values, Triggers, @"C:\Users\ana\Desktop").Collect()).ShouldBe(Values);

    [Fact]
    public void CambiarGestoYCarpeta_SeRefleja()
    {
        var result = OnSta(() =>
        {
            var dialog = SettingsDialog.CreateForTests(Values, Triggers, @"C:\Users\ana\Desktop");
            dialog.ChooseTriggerForTests("CapsLock");
            dialog.UseDefaultFolderForTests();
            return dialog.Collect();
        });

        result.Trigger.ShouldBe("CapsLock");
        result.OutputFolder.ShouldBe(@"C:\Users\ana\Desktop");
        result.Hotkey.ShouldBeTrue();
    }

    [Fact]
    public void Idioma_AutomaticoPorDefectoYSeElige()
    {
        var (initial, chosen) = OnSta(() =>
        {
            var dialog = SettingsDialog.CreateForTests(Values, Triggers, @"C:\Users\ana\Desktop");
            var before = dialog.Collect().Language;
            dialog.ChooseLanguageForTests("en");
            return (before, dialog.Collect().Language);
        });

        initial.ShouldBe("auto");
        chosen.ShouldBe("en");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void VistaPrevia(bool dark)
    {
        var path = Path.Combine(RepoImages(), dark ? "configuracion-oscuro.png" : "configuracion-claro.png");
        OnSta(() =>
        {
            SettingsDialog.RenderPreview(SettingsDialog.CreateForTests(Values with { OutputFolder = @"C:\Users\ana\Desktop" }, Triggers, @"C:\Users\ana\Desktop", dark), path, dark);
            return 0;
        });
        new FileInfo(path).Length.ShouldBeGreaterThan(1000);
    }

    [Fact]
    public void IniciarConWindows_SeActivaCorrigeYQuita()
    {
        var root = $@"Software\KumquatTests\{Guid.NewGuid():N}\Run";
        var previous = Autostart.RunKey;
        Autostart.RunKey = root;
        try
        {
            Autostart.Set(enabled: true, @"C:\Kumquat ñ\Kumquat.exe");
            Autostart.IsEnabledFor(@"C:\Kumquat ñ\Kumquat.exe").ShouldBeTrue();
            using (var key = Registry.CurrentUser.OpenSubKey(root))
            {
                key!.GetValue("Kumquat").ShouldBe(@"""C:\Kumquat ñ\Kumquat.exe""");
            }

            Autostart.Set(enabled: true, @"D:\movido\Kumquat.exe");
            Autostart.IsEnabledFor(@"D:\movido\Kumquat.exe").ShouldBeTrue("si se movió la carpeta se corrige la ruta");

            Autostart.Set(enabled: false, @"D:\movido\Kumquat.exe");
            Autostart.IsEnabledFor(@"D:\movido\Kumquat.exe").ShouldBeFalse();
            Autostart.Set(enabled: false, @"D:\movido\Kumquat.exe"); // quitar dos veces no falla
        }
        finally
        {
            Autostart.RunKey = previous;
            Registry.CurrentUser.DeleteSubKeyTree(root[..root.LastIndexOf('\\')], throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void CarpetaDeSalida_SiNoExisteUsaElEscritorio()
    {
        new AppSettings { OutputFolder = @"Z:\no\existe" }.ResolvedOutputFolder.ShouldBe(AppSettings.DefaultOutputFolder);
        new AppSettings().ResolvedOutputFolder.ShouldBe(AppSettings.DefaultOutputFolder);
        new AppSettings { OutputFolder = Path.GetTempPath() }.ResolvedOutputFolder.ShouldBe(Path.GetTempPath());
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

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Hosting;
using Kumquat.Interaction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kumquat.App;

internal static partial class Program
{
    /// <summary>
    /// <c>--smoke-test</c>: arranca todo (motores, hooks, rueda, bandeja) y sale con 0.
    /// Lo usa tools/publish.ps1 para verificar que el paquete publicado realmente abre.
    /// </summary>
    private const string SmokeTestFlag = "--smoke-test";

    /// <summary><c>--unregister</c>: quita la entrada del menú contextual y sale (para desinstalar).</summary>
    private const string UnregisterFlag = "--unregister";

    [STAThread]
    private static int Main(string[] args)
    {
        var smokeTest = args.Contains(SmokeTestFlag, StringComparer.OrdinalIgnoreCase);
        if (args.Contains(UnregisterFlag, StringComparer.OrdinalIgnoreCase))
        {
            ExplorerMenu.Unregister();
            return 0;
        }

        // El idioma antes que cualquier texto (rueda, menús y herramientas se arman al arrancar).
        AppSettings.Load(TrayApplication.SettingsPath).ApplyLanguage();

        // Desde el menú contextual: "--wheel archivo". Si Kumquat ya corre, se le pasa y se sale.
        var wheelFiles = WheelFilesFrom(args);

        // Una sola instancia por sesión de usuario: dos hooks y dos ruedas pelearían por el drop.
        var mutexName = smokeTest ? @"Local\Kumquat.SmokeTest" : @"Local\Kumquat.SingleInstance";
        using var mutex = new Mutex(initiallyOwned: true, mutexName, out var isFirst);
        if (!isFirst && args.Contains(EngineInstaller.RestartedFlag, StringComparer.OrdinalIgnoreCase))
        {
            // Reinicio tras instalar un motor: la instancia anterior está saliendo.
            isFirst = WaitForPrevious(mutex);
        }

        if (!isFirst)
        {
            ForwardToRunningInstance(wheelFiles);
            return 0;
        }

        var services = new ServiceCollection().AddKumquat(logToFile: true).BuildServiceProvider();
        try
        {
            return Run(services, smokeTest, wheelFiles);
        }
        finally
        {
            // Liberación asíncrona: JobRunner (IAsyncDisposable) cancela trabajos y espera a sus workers.
            var disposal = services.DisposeAsync().AsTask();
            if (!disposal.Wait(TimeSpan.FromSeconds(5)))
            {
                Environment.Exit(0); // No colgar la salida por un motor que no responde: el Job Object lo mata igual.
            }
        }
    }

    private static bool WaitForPrevious(Mutex mutex)
    {
        try
        {
            return mutex.WaitOne(TimeSpan.FromSeconds(15));
        }
        catch (AbandonedMutexException)
        {
            return true; // Salió sin soltarlo: el mutex queda para esta instancia.
        }
    }

    private static List<string> WheelFilesFrom(string[] args)
    {
        var index = Array.FindIndex(args, a => string.Equals(a, ExplorerMenu.WheelFlag, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? [] : [.. args.Skip(index + 1).Where(a => !a.StartsWith("--", StringComparison.Ordinal))];
    }

    /// <summary>La instancia puede estar arrancando: se reintenta un momento antes de rendirse.</summary>
    private static void ForwardToRunningInstance(List<string> files)
    {
        for (var attempt = 0; files.Count > 0 && attempt < 20; attempt++)
        {
            if (Interaction.Wheel.WheelRequests.TrySend(files))
            {
                return;
            }

            Thread.Sleep(150);
        }
    }

    private static int Run(ServiceProvider services, bool smokeTest, List<string> wheelFiles)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Kumquat");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogFatal(logger, e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogUnobserved(logger, e.Exception);
            e.SetObserved();
        };

        KumquatServices.OnStartup(services);

        using var ui = new UiThread();
        UiThread.ErrorSink = ex => LogUiError(logger, ex);
        using (var app = new TrayApplication(services, ui))
        {
            app.Start();
            if (wheelFiles.Count > 0)
            {
                app.OpenWheel(wheelFiles);
            }

            if (!smokeTest)
            {
                UiThread.Run();
            }
        }

        return 0;
    }

    [LoggerMessage(Level = LogLevel.Critical, Message = "Excepción no controlada")]
    private static partial void LogFatal(ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Tarea con excepción no observada")]
    private static partial void LogUnobserved(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error en el hilo de UI")]
    private static partial void LogUiError(ILogger logger, Exception exception);
}

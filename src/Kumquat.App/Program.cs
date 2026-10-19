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

    [STAThread]
    private static int Main(string[] args)
    {
        var smokeTest = args.Contains(SmokeTestFlag, StringComparer.OrdinalIgnoreCase);

        // Una sola instancia por sesión de usuario: dos hooks y dos ruedas pelearían por el drop.
        var mutexName = smokeTest ? @"Local\Kumquat.SmokeTest" : @"Local\Kumquat.SingleInstance";
        using var mutex = new Mutex(initiallyOwned: true, mutexName, out var isFirst);
        if (!isFirst)
        {
            return 0;
        }

        var services = new ServiceCollection().AddKumquat(logToFile: true).BuildServiceProvider();
        try
        {
            return Run(services, smokeTest);
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

    private static int Run(ServiceProvider services, bool smokeTest)
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

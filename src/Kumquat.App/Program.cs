// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Jobs;
using Kumquat.Hosting;
using Kumquat.Interaction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kumquat.App;

internal static partial class Program
{
    [STAThread]
    private static int Main()
    {
        // Una sola instancia por sesión de usuario: dos hooks y dos ruedas pelearían por el drop.
        using var mutex = new Mutex(initiallyOwned: true, @"Local\Kumquat.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            return 0;
        }

        using var services = new ServiceCollection().AddKumquat(logToFile: true).BuildServiceProvider();
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
            UiThread.Run();
        }

        services.GetRequiredService<JobRunner>().DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
        return 0;
    }

    [LoggerMessage(Level = LogLevel.Critical, Message = "Excepción no controlada")]
    private static partial void LogFatal(ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Tarea con excepción no observada")]
    private static partial void LogUnobserved(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error en el hilo de UI")]
    private static partial void LogUiError(ILogger logger, Exception exception);
}

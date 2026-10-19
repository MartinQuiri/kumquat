// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Conversion;
using Kumquat.Core.Jobs;
using Kumquat.Core.Previews;
using Kumquat.Core.Tools;
using Kumquat.Engines.Archives;
using Kumquat.Engines.FFmpeg;
using Kumquat.Engines.Ocr;
using Kumquat.Engines.Pdf;
using Kumquat.Engines.Vips;
using Kumquat.Infrastructure.Engines;
using Kumquat.Infrastructure.Files;
using Kumquat.Infrastructure.Processes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace Kumquat.Hosting;

/// <summary>Composición de dependencias compartida por la app de bandeja y la CLI.</summary>
public static class KumquatServices
{
    /// <summary>Carpeta de datos de usuario: <c>%LOCALAPPDATA%\Kumquat</c>.</summary>
    public static string DataDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kumquat");

    /// <summary>Carpeta de logs.</summary>
    public static string LogDirectory { get; } = Path.Combine(DataDirectory, "logs");

    /// <summary>Registra el núcleo, los motores y la infraestructura.</summary>
    /// <param name="services">Contenedor.</param>
    /// <param name="logToFile">Escribir logs rotativos en <see cref="LogDirectory"/>.</param>
    public static IServiceCollection AddKumquat(this IServiceCollection services, bool logToFile)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            if (logToFile)
            {
                // Sin contenido de archivos ni datos personales más allá de rutas locales; el log nunca sale del equipo.
                var serilog = new LoggerConfiguration()
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .WriteTo.File(Path.Combine(LogDirectory, "kumquat-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7, formatProvider: System.Globalization.CultureInfo.InvariantCulture)
                    .CreateLogger();
                builder.AddSerilog(serilog, dispose: true);
            }
        });

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton(_ => new EngineLocator());
        services.AddSingleton<TempWorkspaceFactory>(sp => new TempWorkspaceFactory(sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<ITempWorkspaceFactory>(sp => sp.GetRequiredService<TempWorkspaceFactory>());

        services.AddSingleton<FfmpegRunner>();
        services.AddSingleton<IConverter, FfmpegConverter>();
        services.AddSingleton<IConverter, VipsConverter>();

        // Herramientas: el orden de registro es el orden en el anillo (la primera queda arriba).
        services.AddSingleton<PdfEngines>();
        services.AddSingleton<ImagePdfBuilder>();
        services.AddSingleton<TesseractEngine>();
        services.AddSingleton<IConverter, MupdfConverter>();
        services.AddSingleton(sp => new ToolService(OrderForWheel(
        [
            .. PdfTool.CreateAll(sp.GetRequiredService<PdfEngines>(), sp.GetRequiredService<ImagePdfBuilder>(), sp.GetRequiredService<ITempWorkspaceFactory>()),
            .. VipsTool.CreateAll(),
            .. OcrTool.CreateAll(sp.GetRequiredService<TesseractEngine>(), sp.GetRequiredService<PdfEngines>(), sp.GetRequiredService<ITempWorkspaceFactory>()),
            .. FfmpegTool.CreateAll(sp.GetRequiredService<FfmpegRunner>()),
            .. ArchiveTools.CreateAll(),
        ])));
        services.AddSingleton(sp => ConversionGraph.FromConverters(sp.GetServices<IConverter>()));
        services.AddSingleton<ConversionService>();
        services.AddSingleton<IPreviewService, PreviewService>();
        services.AddSingleton(_ => new JobRunner(maxConcurrency: Math.Clamp(Environment.ProcessorCount / 2, 1, 4)));
        return services;
    }

    /// <summary>
    /// Orden en el anillo de herramientas: primero las que combinan (al soltar varios archivos,
    /// unirlos es lo más probable), luego las de cada archivo y al final "Extraer" y "ZIP".
    /// </summary>
    private static IEnumerable<ITool> OrderForWheel(IReadOnlyList<ITool> tools)
        => tools.OrderBy(t => t.Id switch
        {
            _ when t.Mode == ToolMode.Combine && !t.AcceptsDirectories => 0,
            "archive.extract" => 2,
            "archive.zip" => 3,
            _ => 1,
        });

    /// <summary>Resumen de motores para el log de arranque.</summary>
    public static string DescribeEngines(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var converters = services.GetServices<IConverter>();
        var graph = services.GetRequiredService<ConversionGraph>();
        var ocr = services.GetRequiredService<TesseractEngine>().IsAvailable ? "ok" : "ausente";
        return string.Join(", ", converters.Select(c => $"{c.Id}={(c.IsAvailable ? "ok" : "ausente")}({c.Edges.Count})")) + $", tesseract={ocr}; aristas={graph.Edges.Count()}";
    }

    /// <summary>Registra en el log el arranque y limpia restos de sesiones anteriores.</summary>
    public static void OnStartup(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Kumquat");
        var cleaned = services.GetRequiredService<TempWorkspaceFactory>().CleanupStale(TimeSpan.FromDays(1));
#pragma warning disable CA1848, CA1873 // Arranque: una sola línea, sin presión de rendimiento.
        logger.LogInformation("Kumquat {Version} iniciado. Motores: {Engines}. Temporales limpiados: {Cleaned}", typeof(KumquatServices).Assembly.GetName().Version, DescribeEngines(services), cleaned);
#pragma warning restore CA1848, CA1873
    }
}

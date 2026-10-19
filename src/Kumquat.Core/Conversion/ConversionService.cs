// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Globalization;
using Kumquat.Core.Formats;
using Kumquat.Core.Results;
using Microsoft.Extensions.Logging;

namespace Kumquat.Core.Conversion;

/// <summary>
/// Planifica y ejecuta una conversión completa: detecta el formato real, busca la
/// ruta en el grafo, encadena los pasos en una carpeta temporal y publica la salida
/// de forma atómica (nunca deja archivos a medias con el nombre final).
/// </summary>
public sealed partial class ConversionService
{
    private readonly ConversionGraph _graph;
    private readonly Dictionary<string, IConverter> _converters;
    private readonly ITempWorkspaceFactory _workspaces;
    private readonly ILogger<ConversionService> _logger;

    /// <summary>Crea el servicio.</summary>
    public ConversionService(ConversionGraph graph, IEnumerable<IConverter> converters, ITempWorkspaceFactory workspaces, ILogger<ConversionService> logger)
    {
        ArgumentNullException.ThrowIfNull(converters);
        _graph = graph ?? throw new ArgumentNullException(nameof(graph));
        _converters = converters.ToDictionary(c => c.Id, StringComparer.Ordinal);
        _workspaces = workspaces ?? throw new ArgumentNullException(nameof(workspaces));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Grafo que usa el servicio.</summary>
    public ConversionGraph Graph => _graph;

    /// <summary>Convierte un archivo.</summary>
    /// <returns>Ruta del archivo creado.</returns>
    public async Task<Result<string>> ConvertAsync(ConversionRequest request, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var plan = Plan(request);
        if (!plan.IsSuccess)
        {
            return plan.Error;
        }

        var path = plan.Value;
        var finalDirectory = request.OutputDirectory ?? Path.GetDirectoryName(Path.GetFullPath(request.InputPath))!;
        LogPlan(request.InputPath, string.Join(" -> ", path.Edges.Select(e => $"{e.From}->{e.To}[{e.ConverterId}]")));

        using var workspace = _workspaces.Create();
        var stagingPath = Path.Combine(finalDirectory, string.Create(CultureInfo.InvariantCulture, $".kumquat-{Guid.NewGuid():N}{request.Target.DefaultExtension}"));
        try
        {
            var current = request.InputPath;
            for (var i = 0; i < path.Edges.Count; i++)
            {
                var edge = path.Edges[i];
                var isLast = i == path.Edges.Count - 1;
                var output = isLast ? stagingPath : workspace.NewFilePath(edge.To.DefaultExtension);
                var stepProgress = progress is null ? null : new StepProgress(progress, i, path.Edges.Count);

                var result = await RunStepAsync(new ConversionStep(current, output, edge), stepProgress, cancellationToken);
                if (!result.IsSuccess)
                {
                    return result.Error;
                }

                if (!File.Exists(output))
                {
                    return new Error("no-output", $"El motor '{edge.ConverterId}' terminó sin producir archivo.");
                }

                current = output;
            }

            var finalPath = Publish(stagingPath, request.InputPath, request.Target, finalDirectory);
            progress?.Report(1.0);
            return finalPath;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Error.Cancelled;
        }
        catch (UnauthorizedAccessException ex)
        {
            return new Error("access-denied", ex.Message);
        }
        catch (IOException ex)
        {
            return new Error("io", ex.Message);
        }
        finally
        {
            TryDelete(stagingPath);
        }
    }

    /// <summary>Valida el pedido y busca la ruta sin ejecutar nada.</summary>
    public Result<ConversionPath> Plan(ConversionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!File.Exists(request.InputPath))
        {
            return new Error("input-missing", $"No existe el archivo '{request.InputPath}'.");
        }

        var source = FormatSniffer.DetectFile(request.InputPath);
        if (source is null)
        {
            return new Error("unknown-format", $"No se reconoce el formato de '{Path.GetFileName(request.InputPath)}'.");
        }

        var path = _graph.FindPath(source, request.Target);
        if (path is null)
        {
            return new Error("no-route", $"No hay forma de convertir {source.DisplayName} a {request.Target.DisplayName}.");
        }

        return path;
    }

    private static string Publish(string stagingPath, string inputPath, FileFormat target, string directory)
    {
        // Mover dentro del mismo volumen es atómico; si otro proceso tomó el nombre entre
        // la elección y el movimiento, se elige el siguiente libre.
        for (var attempt = 0; ; attempt++)
        {
            var finalPath = OutputNaming.ChooseOutputPath(inputPath, target, File.Exists, directory);
            try
            {
                File.Move(stagingPath, finalPath, overwrite: false);
                return finalPath;
            }
            catch (IOException) when (attempt < 5 && File.Exists(finalPath))
            {
                // Carrera por el nombre: reintentar.
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Mejor esfuerzo: el archivo oculto .kumquat-* queda y se limpia en el próximo arranque.
        }
        catch (UnauthorizedAccessException)
        {
            // Idem.
        }
    }

    private async Task<Result> RunStepAsync(ConversionStep step, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (!_converters.TryGetValue(step.Edge.ConverterId, out var converter))
        {
            return new Error("converter-missing", $"El convertidor '{step.Edge.ConverterId}' no está registrado.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var result = await converter.ConvertAsync(step, progress, cancellationToken);
        if (!result.IsSuccess)
        {
            LogStepFailed(step.Edge.ConverterId, step.Edge.From.Id, step.Edge.To.Id, result.Error.ToString());
        }

        return result;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Convirtiendo {Input}: {Plan}")]
    private partial void LogPlan(string input, string plan);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falló {Converter} {From}->{To}: {Error}")]
    private partial void LogStepFailed(string converter, string from, string to, string error);

    /// <summary>Reparte el progreso total entre los pasos de una ruta.</summary>
    private sealed class StepProgress(IProgress<double> inner, int index, int count) : IProgress<double>
    {
        public void Report(double value) => inner.Report((index + Math.Clamp(value, 0, 1)) / count);
    }
}

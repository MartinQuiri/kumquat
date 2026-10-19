// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

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
        var result = await StagedOutput.RunAsync(
            request.InputPath,
            request.Target,
            finalDirectory,
            suffix: null,
            stagingPath => RunPathAsync(path, request.InputPath, stagingPath, workspace, progress, cancellationToken),
            cancellationToken);
        if (result.IsSuccess)
        {
            progress?.Report(1.0);
        }

        return result;
    }

    /// <summary>Envuelve una conversión como trabajo encolable en <see cref="Jobs.JobRunner"/>.</summary>
    public Jobs.JobRequest CreateJob(ConversionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new Jobs.JobRequest(request.InputPath, "a " + request.Target.DisplayName, (progress, ct) => ConvertAsync(request, progress, ct));
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

    private async Task<Result> RunPathAsync(ConversionPath path, string input, string finalOutput, ITempWorkspace workspace, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var current = input;
        for (var i = 0; i < path.Edges.Count; i++)
        {
            var edge = path.Edges[i];
            var output = i == path.Edges.Count - 1 ? finalOutput : workspace.NewFilePath(edge.To.DefaultExtension);
            var stepProgress = progress is null ? null : new StepProgress(progress, i, path.Edges.Count);

            var result = await RunStepAsync(new ConversionStep(current, output, edge), stepProgress, cancellationToken);
            if (!result.IsSuccess)
            {
                return result;
            }

            if (!File.Exists(output))
            {
                return new Error("no-output", $"El motor '{edge.ConverterId}' terminó sin producir archivo.");
            }

            current = output;
        }

        return Result.Success;
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

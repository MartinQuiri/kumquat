// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;
using Kumquat.Core.Jobs;
using Kumquat.Core.Results;
using Kumquat.Core.Text;

namespace Kumquat.Core.Tools;

/// <summary>Catálogo y ejecución de herramientas, con la misma salida segura que las conversiones.</summary>
public sealed class ToolService
{
    private readonly List<ITool> _tools;

    /// <summary>Crea el servicio con las herramientas disponibles.</summary>
    public ToolService(IEnumerable<ITool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);
        _tools = [.. tools.Where(t => t.IsAvailable)];
        var duplicate = _tools.GroupBy(t => t.Id, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"Herramienta duplicada: {duplicate.Key}", nameof(tools));
        }
    }

    /// <summary>Detecta el tipo de cada ruta (carpeta o formato); <c>null</c> si alguna no se reconoce.</summary>
    public static IReadOnlyList<ToolInput>? Describe(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var inputs = new List<ToolInput>(paths.Count);
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                inputs.Add(new ToolInput(Path.GetFullPath(path), null));
                continue;
            }

            var format = File.Exists(path) ? FormatSniffer.DetectFile(path) : null;
            if (format is null)
            {
                return null;
            }

            inputs.Add(new ToolInput(Path.GetFullPath(path), format));
        }

        return inputs;
    }

    /// <summary>Herramientas aplicables a estas entradas, en orden de registro.</summary>
    public IReadOnlyList<ITool> ToolsFor(IReadOnlyList<ToolInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        return inputs.Count == 0 ? [] : [.. _tools.Where(t => IsOffered(t, inputs))];
    }

    /// <summary>Atajo para un conjunto de formatos (sin carpetas).</summary>
    public IReadOnlyList<ITool> ToolsForFormats(IReadOnlyCollection<FileFormat> formats)
    {
        ArgumentNullException.ThrowIfNull(formats);
        return ToolsFor([.. formats.Select(f => new ToolInput(string.Empty, f))]);
    }

    /// <summary>Busca una herramienta por id.</summary>
    public ITool? Find(string id) => _tools.Find(t => string.Equals(t.Id, id, StringComparison.Ordinal));

    /// <summary>Aplica una herramienta a un archivo.</summary>
    public Task<Result<string>> ApplyAsync(string inputPath, string toolId, IProgress<double>? progress, CancellationToken cancellationToken)
        => ApplyAsync([inputPath], toolId, progress, cancellationToken);

    /// <summary>
    /// Aplica una herramienta a una o más rutas en una sola salida (modo combinar) o a la
    /// primera ruta (modo uno a uno). El resultado queda junto a la (primera) entrada.
    /// </summary>
    public Task<Result<string>> ApplyAsync(IReadOnlyList<string> paths, string toolId, IProgress<double>? progress, CancellationToken cancellationToken)
        => ApplyAsync(paths, toolId, null, progress, cancellationToken);

    /// <summary>Igual que el anterior, con las opciones elegidas por el usuario (validadas por la herramienta).</summary>
    /// <param name="paths">Entradas.</param>
    /// <param name="toolId">Herramienta.</param>
    /// <param name="options">Opciones elegidas.</param>
    /// <param name="progress">Progreso.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    /// <param name="outputDirectory">
    /// Carpeta de salida; por defecto, la de la (primera) entrada. Se usa para archivos que no tienen
    /// carpeta propia (adjuntos, contenido de un .zip, imágenes de una web).
    /// </param>
    public async Task<Result<string>> ApplyAsync(IReadOnlyList<string> paths, string toolId, IReadOnlyDictionary<string, string>? options, IProgress<double>? progress, CancellationToken cancellationToken, string? outputDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var tool = Find(toolId);
        if (tool is null)
        {
            return new Error("tool-missing", $"La herramienta '{toolId}' no está disponible.");
        }

        var missing = paths.FirstOrDefault(p => !File.Exists(p) && !Directory.Exists(p));
        if (paths.Count == 0 || missing is not null)
        {
            return new Error("input-missing", $"No existe '{missing}'.");
        }

        var inputs = Describe(paths);
        if (inputs is null || !IsOffered(tool, inputs))
        {
            return new Error("tool-not-applicable", $"{tool.Label} no sirve para estos archivos.");
        }

        options ??= new Dictionary<string, string>(StringComparer.Ordinal);
        if (tool.Validate(options) is { } invalid)
        {
            return new Error("invalid-options", invalid);
        }

        if (tool.Mode == ToolMode.Combine)
        {
            inputs = [.. inputs.OrderBy(i => Path.GetFileName(i.Path), NaturalComparer.Instance)];
        }

        var first = inputs[0];
        var output = tool.OutputFormat(first.Format);
        var directory = outputDirectory ?? Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(first.Path))!;
        Directory.CreateDirectory(directory);
        var result = output is null
            ? await StagedOutput.RunDirectoryAsync(first.Path, directory, tool.OutputSuffix, staging => tool.ApplyAsync(new ToolStep(inputs, staging, null, options), progress, cancellationToken), cancellationToken)
            : await StagedOutput.RunAsync(first.Path, output, directory, tool.OutputSuffix, staging => tool.ApplyAsync(new ToolStep(inputs, staging, output, options), progress, cancellationToken), cancellationToken);
        if (result.IsSuccess)
        {
            progress?.Report(1.0);
        }

        return result;
    }

    /// <summary>Parámetros a pedir para estas rutas (vacío si la herramienta no los necesita).</summary>
    public async Task<IReadOnlyList<ToolParameter>> GetParametersAsync(IReadOnlyList<string> paths, string toolId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var tool = Find(toolId);
        var inputs = tool is null ? null : Describe(paths);
        return tool is null || inputs is null || inputs.Count == 0 ? [] : await tool.GetParametersAsync(inputs, cancellationToken);
    }

    /// <summary>Valida opciones antes de encolar (para mostrar el error en la ventana de opciones).</summary>
    public string? Validate(string toolId, IReadOnlyDictionary<string, string> options)
        => Find(toolId)?.Validate(options);

    /// <summary>Trabajos encolables: uno por archivo, o uno solo si la herramienta combina.</summary>
    /// <param name="paths">Entradas.</param>
    /// <param name="toolId">Herramienta.</param>
    /// <param name="options">Opciones elegidas.</param>
    /// <param name="outputDirectory">Carpeta de salida (por defecto, junto a cada entrada).</param>
    public IReadOnlyList<JobRequest> CreateJobs(IReadOnlyList<string> paths, string toolId, IReadOnlyDictionary<string, string>? options = null, string? outputDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var tool = Find(toolId);
        var label = tool?.Label ?? toolId;
        return tool?.Mode == ToolMode.Combine
            ? [new JobRequest(paths[0], label, (progress, ct) => ApplyAsync(paths, toolId, options, progress, ct, outputDirectory))]
            : [.. paths.Select(p => new JobRequest(p, label, (progress, ct) => ApplyAsync([p], toolId, options, progress, ct, outputDirectory)))];
    }

    /// <summary>Trabajo encolable para un archivo.</summary>
    public JobRequest CreateJob(string inputPath, string toolId) => CreateJobs([inputPath], toolId)[0];

    private static bool IsOffered(ITool tool, IReadOnlyList<ToolInput> inputs)
        => inputs.Count >= tool.MinInputs
           && inputs.All(i => i.IsDirectory ? tool.AcceptsDirectories : tool.AppliesTo(i.Format!));
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;
using Kumquat.Core.Jobs;
using Kumquat.Core.Results;

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

    /// <summary>Herramientas que sirven para TODOS los formatos dados, en orden de registro.</summary>
    public IReadOnlyList<ITool> ToolsFor(IReadOnlyCollection<FileFormat> formats)
    {
        ArgumentNullException.ThrowIfNull(formats);
        return formats.Count == 0 ? [] : [.. _tools.Where(t => formats.All(t.AppliesTo))];
    }

    /// <summary>Busca una herramienta por id.</summary>
    public ITool? Find(string id) => _tools.Find(t => string.Equals(t.Id, id, StringComparison.Ordinal));

    /// <summary>Aplica una herramienta a un archivo; el resultado queda junto al original.</summary>
    public async Task<Result<string>> ApplyAsync(string inputPath, string toolId, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(inputPath);
        var tool = Find(toolId);
        if (tool is null)
        {
            return new Error("tool-missing", $"La herramienta '{toolId}' no está disponible.");
        }

        if (!File.Exists(inputPath))
        {
            return new Error("input-missing", $"No existe el archivo '{inputPath}'.");
        }

        var format = FormatSniffer.DetectFile(inputPath);
        if (format is null || !tool.AppliesTo(format))
        {
            return new Error("tool-not-applicable", $"{tool.Label} no sirve para '{Path.GetFileName(inputPath)}'.");
        }

        var output = tool.OutputFormat(format);
        var directory = Path.GetDirectoryName(Path.GetFullPath(inputPath))!;
        var result = await StagedOutput.RunAsync(
            inputPath,
            output,
            directory,
            tool.OutputSuffix,
            staging => tool.ApplyAsync(new ToolStep(inputPath, staging, format, output), progress, cancellationToken),
            cancellationToken);
        if (result.IsSuccess)
        {
            progress?.Report(1.0);
        }

        return result;
    }

    /// <summary>Envuelve la aplicación de una herramienta como trabajo encolable.</summary>
    public JobRequest CreateJob(string inputPath, string toolId)
    {
        var label = Find(toolId)?.Label ?? toolId;
        return new JobRequest(inputPath, label, (progress, ct) => ApplyAsync(inputPath, toolId, progress, ct));
    }
}

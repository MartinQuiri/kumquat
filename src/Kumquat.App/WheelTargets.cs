// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;
using Kumquat.Core.Text;
using Kumquat.Core.Tools;
using Kumquat.Interaction.Wheel;

namespace Kumquat.App;

/// <summary>Traduce lo arrastrado en los anillos de la rueda: convertir, herramientas, acciones.</summary>
internal sealed class WheelTargets(ConversionGraph graph, ToolService tools)
{
    /// <summary>Máximo de rutas que se inspeccionan al entrar a la rueda (el resto se valida al ejecutar).</summary>
    public const int MaxSniffedFiles = 64;

    public IReadOnlyList<WheelRing> For(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count == 0)
        {
            return [];
        }

        // Las acciones sirven para todo, incluidas carpetas y formatos desconocidos.
        var actions = new WheelRing(WheelRingKind.Actions, FileActions.Items);
        var inputs = SafeDescribe([.. paths.Take(MaxSniffedFiles)]);
        if (inputs is null)
        {
            return [actions];
        }

        var toolItems = tools.ToolsFor(inputs).Take(WheelLayout.MaxToolSectors).Select(t => WheelItem.Tool(t.Id, t.Label)).ToList();
        var formats = inputs.Where(i => !i.IsDirectory).Select(i => i.Format!).ToList();
        var convert = formats.Count == inputs.Count
            ? WheelItemOrdering.Arrange(formats[0].Category, graph.CommonTargets(formats))
            : [];

        return
        [
            new WheelRing(WheelRingKind.Convert, convert),
            new WheelRing(WheelRingKind.Tools, toolItems),
            actions,
        ];
    }

    /// <summary>
    /// Etiqueta bajo la rueda: formato real (por contenido, no por extensión) y tamaño. Un archivo:
    /// "PNG · 2,4 MB"; varios del mismo formato: "3 JPG · 12 MB"; mezclados: "3 archivos · 12 MB".
    /// </summary>
    public static string? Caption(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count == 0)
        {
            return null;
        }

        if (paths.Count == 1 && Directory.Exists(paths[0]))
        {
            return "Carpeta";
        }

        var files = paths.Where(File.Exists).ToList();
        var size = FileSizeText.Format(files.Sum(f => new FileInfo(f).Length));
        if (paths.Count == 1)
        {
            return files.Count == 1 ? $"{FormatName(files[0])} · {size}" : null;
        }

        var formats = files.Take(MaxSniffedFiles).Select(FormatName).Distinct(StringComparer.Ordinal).ToList();
        var label = files.Count < paths.Count ? $"{paths.Count} elementos"
            : formats.Count == 1 ? $"{paths.Count} {formats[0]}"
            : $"{paths.Count} archivos";
        return $"{label} · {size}";
    }

    private static string FormatName(string path)
    {
        var extension = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        return FormatSniffer.DetectFile(path)?.DisplayName ?? (extension.Length is > 0 and <= 5 ? extension : "Archivo");
    }

    private static IReadOnlyList<ToolInput>? SafeDescribe(IReadOnlyList<string> paths)
    {
        try
        {
            return ToolService.Describe(paths);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}

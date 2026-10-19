// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;
using Kumquat.Core.Tools;
using Kumquat.Interaction.Wheel;

namespace Kumquat.App;

/// <summary>Traduce archivos arrastrados en los anillos de la rueda: convertir, herramientas, acciones.</summary>
internal sealed class WheelTargets(ConversionGraph graph, ToolService tools)
{
    /// <summary>Máximo de archivos que se inspeccionan al entrar a la rueda (el resto se valida al ejecutar).</summary>
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
        var formats = DetectAll(paths);
        if (formats is null)
        {
            return [actions];
        }

        var convert = WheelItemOrdering.Arrange(formats[0].Category, graph.CommonTargets(formats));
        var toolItems = tools.ToolsFor(formats).Take(WheelLayout.MaxSectors).Select(t => WheelItem.Tool(t.Id, t.Label)).ToList();
        return
        [
            new WheelRing(WheelRingKind.Convert, convert),
            new WheelRing(WheelRingKind.Tools, toolItems),
            actions,
        ];
    }

    /// <summary>Formatos de todos los archivos, o <c>null</c> si hay carpetas o archivos irreconocibles.</summary>
    private static List<FileFormat>? DetectAll(IReadOnlyList<string> paths)
    {
        var formats = new List<FileFormat>();
        foreach (var path in paths.Take(MaxSniffedFiles))
        {
            if (Directory.Exists(path) || TrySniff(path) is not { } format)
            {
                return null;
            }

            formats.Add(format);
        }

        return formats;
    }

    private static FileFormat? TrySniff(string file)
    {
        try
        {
            return FormatSniffer.DetectFile(file);
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

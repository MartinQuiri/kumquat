// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Conversion;
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

        var toolItems = tools.ToolsFor(inputs).Take(WheelLayout.MaxSectors).Select(t => WheelItem.Tool(t.Id, t.Label)).ToList();
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

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;
using Kumquat.Interaction.Wheel;

namespace Kumquat.App;

/// <summary>Traduce archivos arrastrados en sectores de la rueda usando el grafo.</summary>
internal sealed class WheelTargets(ConversionGraph graph)
{
    /// <summary>Máximo de archivos que se inspeccionan al entrar a la rueda (el resto se valida al convertir).</summary>
    public const int MaxSniffedFiles = 64;

    public IReadOnlyList<WheelItem> For(IReadOnlyList<string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var formats = new List<FileFormat>();
        foreach (var file in files.Take(MaxSniffedFiles))
        {
            if (Directory.Exists(file))
            {
                return []; // Carpetas: la rueda Acciones llega en la fase 3.
            }

            var format = TrySniff(file);
            if (format is null)
            {
                return [];
            }

            formats.Add(format);
        }

        if (formats.Count == 0)
        {
            return [];
        }

        var targets = graph.CommonTargets(formats);
        return WheelItemOrdering.Arrange(formats[0].Category, targets);
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

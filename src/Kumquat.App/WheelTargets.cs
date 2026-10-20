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

    /// <summary>Carpeta donde van los resultados de archivos sin carpeta (la de Configuración).</summary>
    public Func<string> OutputFolder { get; set; } = () => AppSettings.DefaultOutputFolder;

    /// <summary>Si hay winget para ofrecer instalar motores (reemplazable en pruebas).</summary>
    public Func<bool> WingetAvailable { get; set; } = () => EngineOffers.Winget is not null;

    /// <summary>Nombre visible de esa carpeta ("Escritorio" o el nombre de la carpeta elegida).</summary>
    public string OutputFolderName
    {
        get
        {
            var folder = OutputFolder();
            return string.Equals(folder, AppSettings.DefaultOutputFolder, StringComparison.OrdinalIgnoreCase)
                ? "Escritorio"
                : Path.GetFileName(Path.TrimEndingDirectorySeparator(folder));
        }
    }

    /// <summary>
    /// Lo arrastrado no existe en disco: son nombres de archivos virtuales (adjuntos, contenido de
    /// un .zip, imágenes de una web), que se copian recién al soltar.
    /// </summary>
    public static bool AreVirtual(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return paths.Count > 0 && paths.All(p => !File.Exists(p) && !Directory.Exists(p));
    }

    public IReadOnlyList<WheelRing> For(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count == 0)
        {
            return [];
        }

        if (AreVirtual(paths))
        {
            return ForVirtual(paths);
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
        var targets = formats.Count == inputs.Count ? graph.CommonTargets(formats) : [];
        var convert = targets.Count > 0 ? WheelItemOrdering.Arrange(formats[0].Category, targets) : [];

        // Nada puede convertirlo pero un programa gratuito sí: se ofrece instalarlo.
        if (formats.Count == inputs.Count && WingetAvailable() && EngineOffers.For(formats, targets) is { } offer)
        {
            actions = new WheelRing(WheelRingKind.Actions, [EngineOffers.Item(offer), .. FileActions.Items]);
        }

        return
        [
            new WheelRing(WheelRingKind.Convert, convert),
            new WheelRing(WheelRingKind.Tools, toolItems),
            actions,
        ];
    }

    /// <summary>
    /// Anillos para archivos virtuales: el formato sale de la extensión (el contenido todavía no
    /// está); se valida de verdad al soltar. Como acción, solo "Guardar en…".
    /// </summary>
    private IReadOnlyList<WheelRing> ForVirtual(IReadOnlyList<string> names)
    {
        var save = new WheelRing(WheelRingKind.Actions, [WheelItem.Action(FileActions.Save, $"Guardar en {OutputFolderName}")]);
        var formats = names.Take(MaxSniffedFiles).Select(n => KnownFormats.FindByExtension(Path.GetExtension(n))).ToList();
        if (formats.Any(f => f is null))
        {
            return [save];
        }

        var known = formats.Select(f => f!).ToList();
        var toolItems = tools.ToolsForFormats(known)
            .Where(t => known.Count >= t.MinInputs)
            .Take(WheelLayout.MaxToolSectors)
            .Select(t => WheelItem.Tool(t.Id, t.Label))
            .ToList();
        return
        [
            new WheelRing(WheelRingKind.Convert, WheelItemOrdering.Arrange(known[0].Category, graph.CommonTargets(known))),
            new WheelRing(WheelRingKind.Tools, toolItems),
            save,
        ];
    }

    /// <summary>Etiqueta bajo la rueda; para archivos virtuales dice dónde se guardará el resultado.</summary>
    public string? CaptionFor(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (!AreVirtual(paths))
        {
            return Caption(paths);
        }

        var formats = paths.Select(p => KnownFormats.FindByExtension(Path.GetExtension(p))?.DisplayName).Distinct(StringComparer.Ordinal).ToList();
        var what = paths.Count == 1 ? formats[0] ?? "Archivo"
            : formats.Count == 1 && formats[0] is { } single ? $"{paths.Count} {single}"
            : $"{paths.Count} archivos";
        return $"{what} · se guarda en {OutputFolderName}";
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

    /// <summary>Ícono de la familia del primer archivo (o de carpeta).</summary>
    public static string? CategoryIcon(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count == 0)
        {
            return null;
        }

        if (Directory.Exists(paths[0]))
        {
            return "show-in-folder";
        }

        try
        {
            return FormatSniffer.DetectFile(paths[0]) is { } format ? Kumquat.Design.Icons.ForCategory(format.Category) : null;
        }
        catch (IOException)
        {
            return null;
        }
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

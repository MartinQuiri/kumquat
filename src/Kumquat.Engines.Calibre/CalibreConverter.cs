// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Globalization;
using System.Runtime.Versioning;
using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;
using Kumquat.Core.Localization;
using Kumquat.Core.Results;
using Kumquat.Infrastructure.Engines;
using Kumquat.Infrastructure.Processes;
using Microsoft.Win32;

namespace Kumquat.Engines.Calibre;

/// <summary>
/// Libros electrónicos con <c>ebook-convert</c> de Calibre, si está instalado: EPUB, MOBI, AZW3 y
/// FB2 entre sí y a PDF, y Word a libro. Calibre corre con una carpeta de configuración temporal:
/// no toca la biblioteca ni las preferencias de la persona.
/// </summary>
public sealed class CalibreConverter : IConverter
{
    /// <summary>Identificador del convertidor.</summary>
    public const string ConverterId = "calibre";

    /// <summary>Formatos que Calibre lee.</summary>
    public static readonly FileFormat[] Inputs = [KnownFormats.Epub, KnownFormats.Mobi, KnownFormats.Azw3, KnownFormats.Fb2, KnownFormats.Docx];

    /// <summary>Libros que Calibre escribe.</summary>
    public static readonly FileFormat[] Books = [KnownFormats.Epub, KnownFormats.Azw3, KnownFormats.Mobi];

    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(10);
    private const long MaxMemoryBytes = 4L * 1024 * 1024 * 1024;

    private readonly IProcessRunner _runner;
    private readonly ITempWorkspaceFactory _workspaces;

    /// <summary>Crea el convertidor.</summary>
    /// <param name="ebookConvert">Ruta de <c>ebook-convert.exe</c>, o <c>null</c> si Calibre no está.</param>
    /// <param name="runner">Ejecutor de procesos.</param>
    /// <param name="workspaces">Carpetas temporales.</param>
    public CalibreConverter(string? ebookConvert, IProcessRunner runner, ITempWorkspaceFactory workspaces)
    {
        EbookConvert = ebookConvert;
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _workspaces = workspaces ?? throw new ArgumentNullException(nameof(workspaces));
        Edges = ebookConvert is null ? [] : [.. BuildEdges()];
    }

    /// <inheritdoc />
    public string Id => ConverterId;

    /// <summary>Ruta de <c>ebook-convert</c>, o <c>null</c>.</summary>
    public string? EbookConvert { get; }

    /// <inheritdoc />
    public bool IsAvailable => EbookConvert is not null;

    /// <inheritdoc />
    public IReadOnlyCollection<ConversionEdge> Edges { get; }

    /// <summary>
    /// Busca <c>ebook-convert</c>. En Windows, primero donde lo registra el instalador (App Paths, por
    /// usuario o para todos) y después en la carpeta de siempre de Calibre; en Linux, en el <c>PATH</c>;
    /// en macOS, dentro de <c>calibre.app</c> y en el <c>PATH</c>.
    /// </summary>
    public static string? Find()
    {
        if (OperatingSystem.IsWindows())
        {
            return FindOnWindows();
        }

        string[] bundled = OperatingSystem.IsMacOS()
            ? ["/Applications/calibre.app/Contents/MacOS/ebook-convert", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Applications", "calibre.app", "Contents", "MacOS", "ebook-convert")]
            : [];
        return bundled.FirstOrDefault(File.Exists) ?? new EngineLocator().FindExecutable("calibre", "ebook-convert");
    }

    [SupportedOSPlatform("windows")]
    private static string? FindOnWindows()
    {
        foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            using var key = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\ebook-convert.exe");
            if (key?.GetValue(null) is string path && File.Exists(path.Trim('"')))
            {
                return path.Trim('"');
            }
        }

        return new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 }
            .Select(folder => Path.Combine(Environment.GetFolderPath(folder), "Calibre2", "ebook-convert.exe"))
            .FirstOrDefault(File.Exists);
    }

    /// <inheritdoc />
    public async Task<Result> ConvertAsync(ConversionStep step, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (EbookConvert is null || !Edges.Any(e => e.From == step.Edge.From && e.To == step.Edge.To))
        {
            return new Error("no-route", Strings.ErrorNoRoute(step.Edge.From.DisplayName, step.Edge.To.DisplayName));
        }

        using var workspace = _workspaces.Create();

        // Calibre deduce los formatos por la extensión: entrada y salida con la extensión correcta.
        var input = Path.Combine(workspace.DirectoryPath, "entrada" + step.Edge.From.DefaultExtension);
        File.Copy(step.InputPath, input);
        var output = Path.Combine(workspace.DirectoryPath, "salida" + step.Edge.To.DefaultExtension);
        string? problem = null;
        var outcome = await _runner.RunAsync(
            new ProcessSpec
            {
                FileName = EbookConvert,
                Arguments = [input, output],
                Environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["CALIBRE_CONFIG_DIRECTORY"] = Directory.CreateDirectory(Path.Combine(workspace.DirectoryPath, "config")).FullName,
                    ["CALIBRE_TEMP_DIR"] = Directory.CreateDirectory(Path.Combine(workspace.DirectoryPath, "tmp")).FullName,
                },
                Timeout = Timeout,
                MaxMemoryBytes = MaxMemoryBytes,
                OnStandardOutputLine = line => Report(line, progress),
                OnStandardErrorLine = line => problem ??= line.Contains("DRM", StringComparison.Ordinal) ? "drm" : null,
            },
            cancellationToken);

        if (outcome.TimedOut)
        {
            return new Error("engine-timeout", Strings.ErrorEngineNoResponse("Calibre"));
        }

        if (problem is "drm")
        {
            return new Error("engine-failed", Strings.ErrorBookDrm);
        }

        if (!outcome.Succeeded || !File.Exists(output))
        {
            return new Error("engine-failed", Strings.ErrorCalibreFailed);
        }

        File.Move(output, step.OutputPath, overwrite: false);
        return Result.Success;
    }

    /// <summary>Progreso de ebook-convert: líneas como <c>34% Convirtiendo entrada a HTML...</c>.</summary>
    internal static double? ParseProgress(string line)
    {
        var percent = line.IndexOf('%', StringComparison.Ordinal);
        return percent is > 0 and <= 3 && int.TryParse(line.AsSpan(0, percent), NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value <= 100
            ? value / 100.0
            : null;
    }

    private static void Report(string line, IProgress<double>? progress)
    {
        if (progress is not null && ParseProgress(line) is { } value)
        {
            progress.Report(value);
        }
    }

    private static IEnumerable<ConversionEdge> BuildEdges()
    {
        foreach (var from in Inputs)
        {
            foreach (var to in Books.Where(b => b != from))
            {
                yield return new ConversionEdge(from, to, ConverterId, 2.5);
            }

            // EPUB → PDF lo hace MuPDF (más rápido); Word → PDF, Office.
            if (from != KnownFormats.Epub && from != KnownFormats.Docx)
            {
                yield return new ConversionEdge(from, KnownFormats.Pdf, ConverterId, 3.0);
            }
        }
    }
}

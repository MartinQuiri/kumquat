// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Results;
using Kumquat.Infrastructure.Engines;
using Kumquat.Infrastructure.Processes;

namespace Kumquat.Engines.Pdf;

/// <summary>
/// Ejecuta <c>mutool</c> (MuPDF: renderizar, convertir, comprimir) y <c>qpdf</c> (unir, dividir,
/// rotar) fuera de proceso. Todas las rutas se pasan absolutas: nunca empiezan con '-' y no
/// pueden confundirse con opciones.
/// </summary>
public sealed class PdfEngines
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(30);
    private const long MaxMemoryBytes = 2L * 1024 * 1024 * 1024;

    private readonly IProcessRunner _runner;

    /// <summary>Localiza los binarios.</summary>
    public PdfEngines(IProcessRunner runner, EngineLocator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        Mutool = locator.Find("mupdf", "mutool.exe");
        Qpdf = locator.Find("qpdf", Path.Combine("bin", "qpdf.exe"));
    }

    /// <summary>Ruta de mutool, si está instalado.</summary>
    public string? Mutool { get; }

    /// <summary>Ruta de qpdf, si está instalado.</summary>
    public string? Qpdf { get; }

    /// <summary>Ejecuta mutool.</summary>
    public Task<Result> RunMutoolAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        => RunAsync(Mutool, "MuPDF", arguments, okExitCodes: [0], cancellationToken);

    /// <summary>Ejecuta qpdf. El código 3 significa "terminó con advertencias" (PDF algo dañado pero recuperado).</summary>
    public Task<Result> RunQpdfAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        => RunAsync(Qpdf, "qpdf", arguments, okExitCodes: [0, 3], cancellationToken);

    /// <summary>Ejecuta mutool y devuelve su salida estándar (UTF-8), o <c>null</c> si falló.</summary>
    public async Task<string?> RunMutoolForOutputAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (Mutool is null)
        {
            return null;
        }

        var output = new System.Text.StringBuilder();
        var outcome = await _runner.RunAsync(
            new ProcessSpec { FileName = Mutool, Arguments = arguments, Timeout = Timeout, MaxMemoryBytes = MaxMemoryBytes, OnStandardOutputLine = line => output.AppendLine(line) },
            cancellationToken);
        return outcome.Succeeded ? output.ToString() : null;
    }

    /// <summary>Ruta absoluta normalizada para pasar a un motor.</summary>
    internal static string Abs(string path) => Path.GetFullPath(path);

    private async Task<Result> RunAsync(string? executable, string name, IReadOnlyList<string> arguments, int[] okExitCodes, CancellationToken cancellationToken)
    {
        if (executable is null)
        {
            return new Error("engine-missing", $"{name} no está instalado.");
        }

        var outcome = await _runner.RunAsync(
            new ProcessSpec { FileName = executable, Arguments = arguments, Timeout = Timeout, MaxMemoryBytes = MaxMemoryBytes },
            cancellationToken);

        if (outcome.TimedOut)
        {
            return new Error("engine-timeout", $"{name} superó el tiempo máximo.");
        }

        if (okExitCodes.Contains(outcome.ExitCode))
        {
            return Result.Success;
        }

        var detail = outcome.StandardErrorTail.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
        return outcome.StandardErrorTail.Contains("password", StringComparison.OrdinalIgnoreCase)
            ? new Error("pdf-encrypted", "El PDF está protegido con contraseña.")
            : new Error("engine-failed", $"{name} terminó con código {outcome.ExitCode}: {detail}");
    }
}

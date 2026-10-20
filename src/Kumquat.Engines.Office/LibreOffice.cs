// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Conversion;
using Kumquat.Core.Results;
using Kumquat.Infrastructure.Processes;

namespace Kumquat.Engines.Office;

/// <summary>
/// LibreOffice sin interfaz (<c>soffice --headless --convert-to</c>) con un perfil temporal propio:
/// así funciona aunque la persona tenga LibreOffice abierto y no le toca su configuración.
/// </summary>
internal sealed class LibreOffice(string soffice, IProcessRunner runner, ITempWorkspaceFactory workspaces)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);
    private const long MaxMemoryBytes = 4L * 1024 * 1024 * 1024;

    /// <summary>Convierte con el filtro de salida de LibreOffice (p. ej. <c>pdf</c>).</summary>
    /// <param name="input">Documento de entrada.</param>
    /// <param name="output">Ruta final del resultado.</param>
    /// <param name="convertTo">Argumento de <c>--convert-to</c>.</param>
    /// <param name="inputFilter">Filtro de importación forzado (p. ej. para abrir un PDF en Writer), o <c>null</c>.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    public async Task<Result> ConvertAsync(string input, string output, string convertTo, string? inputFilter, CancellationToken cancellationToken)
    {
        using var workspace = workspaces.Create();
        var profile = Path.Combine(workspace.DirectoryPath, "perfil");
        var produced = Directory.CreateDirectory(Path.Combine(workspace.DirectoryPath, "salida")).FullName;
        List<string> arguments =
        [
            $"-env:UserInstallation={new Uri(profile).AbsoluteUri}",
            "--headless", "--invisible", "--norestore", "--nolockcheck", "--nodefault", "--nologo",
        ];
        if (inputFilter is not null)
        {
            arguments.Add($"--infilter={inputFilter}");
        }

        arguments.AddRange(["--convert-to", convertTo, "--outdir", produced, Path.GetFullPath(input)]);
        var outcome = await runner.RunAsync(
            new ProcessSpec { FileName = soffice, Arguments = arguments, Timeout = Timeout, MaxMemoryBytes = MaxMemoryBytes },
            cancellationToken);
        if (outcome.TimedOut)
        {
            return new Error("engine-timeout", "LibreOffice no respondió a tiempo.");
        }

        var result = Directory.EnumerateFiles(produced).FirstOrDefault();
        if (result is null)
        {
            return new Error("engine-failed", "LibreOffice no pudo convertir el documento (¿está protegido con contraseña o dañado?).");
        }

        File.Move(result, output, overwrite: false);
        return Result.Success;
    }
}

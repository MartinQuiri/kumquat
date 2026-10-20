// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Kumquat.Core.Conversion;
using Kumquat.Core.Localization;
using Kumquat.Core.Results;
using Kumquat.Infrastructure.Processes;

namespace Kumquat.Engines.Office;

/// <summary>
/// Microsoft Office por automatización COM, en un proceso de PowerShell aparte (si Office se cuelga
/// o muestra algo, Kumquat no se traba). Una conversión a la vez: Office no está hecho para trabajar
/// en paralelo. Si se pasa del tiempo, se cierra solo la instancia de Office que abrió la conversión.
/// </summary>
internal sealed class MicrosoftOffice(IProcessRunner runner, ITempWorkspaceFactory workspaces)
{
    /// <summary>Turno de Office: una conversión (o tarea de automatización) a la vez.</summary>
    internal static readonly SemaphoreSlim OneAtATime = new(1, 1);
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);
    private static readonly Lazy<string> Script = new(LoadScript);

    private static string PowerShell => Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");

    /// <summary>Convierte <paramref name="input"/> a <paramref name="format"/> (<c>pdf</c> o <c>docx</c>) con la aplicación dada.</summary>
    public async Task<Result> ConvertAsync(string app, string input, string output, string format, CancellationToken cancellationToken)
    {
        await OneAtATime.WaitAsync(cancellationToken);
        try
        {
            using var workspace = workspaces.Create();
            var script = workspace.NewFilePath(".ps1");
            await File.WriteAllTextAsync(script, Script.Value, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), cancellationToken);

            // Office abre una copia: si hay que cerrarlo a la fuerza, recuerda el archivo como "provocó
            // un error grave" y la próxima vez pregunta; así eso nunca le pasa a un archivo de la persona.
            var copy = workspace.NewFilePath(Path.GetExtension(input));
            File.Copy(input, copy);
            return await RunAsync(script, app, copy, output, format, cancellationToken);
        }
        finally
        {
            OneAtATime.Release();
        }
    }

    private static string LoadScript()
    {
        using var stream = typeof(MicrosoftOffice).Assembly.GetManifestResourceStream("Kumquat.OfficeConvert.ps1")
            ?? throw new InvalidOperationException("Falta el script de conversión de Office.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>Cierra la instancia de Office abierta por esta conversión (nunca una que ya estuviera abierta).</summary>
    private static void KillOurs(int? processId)
    {
        if (processId is not { } id)
        {
            return;
        }

        try
        {
            using var process = Process.GetProcessById(id);
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
        {
            // Ya terminó.
        }
    }

    private async Task<Result> RunAsync(string script, string app, string input, string output, string format, CancellationToken cancellationToken)
    {
        int? officeProcess = null;
        string? error = null;
        var done = false;
        var spec = new ProcessSpec
        {
            FileName = PowerShell,
            Arguments =
            [
                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script,
                "-App", app, "-InputPath", Path.GetFullPath(input), "-OutputPath", Path.GetFullPath(output), "-Format", format,
            ],
            Timeout = Timeout,
            OnStandardOutputLine = line =>
            {
                if (line.StartsWith("pid:", StringComparison.Ordinal) && int.TryParse(line.AsSpan(4), NumberStyles.None, CultureInfo.InvariantCulture, out var id))
                {
                    officeProcess = id;
                }
                else if (line.StartsWith("error:", StringComparison.Ordinal))
                {
                    error = line[6..].Trim();
                }
                else if (string.Equals(line.Trim(), "ok", StringComparison.Ordinal))
                {
                    done = true;
                }
            },
        };

        ProcessOutcome outcome;
        try
        {
            outcome = await runner.RunAsync(spec, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            KillOurs(officeProcess);
            throw;
        }

        if (outcome.TimedOut)
        {
            KillOurs(officeProcess);
            return new Error("engine-timeout", Strings.ErrorEngineNoResponse($"Microsoft {app}"));
        }

        return done && File.Exists(output)
            ? Result.Success
            : new Error("engine-failed", error is null ? Strings.ErrorMicrosoftFailed(app) : Strings.ErrorMicrosoftDetail(app, error));
    }
}

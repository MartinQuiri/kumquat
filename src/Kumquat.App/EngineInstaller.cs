// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Diagnostics;

namespace Kumquat.App;

/// <summary>
/// Instala un motor con winget en una consola visible (se ve el progreso y lo que pida el
/// instalador) y, si salió bien, reinicia Kumquat para que lo detecte.
/// </summary>
internal static class EngineInstaller
{
    /// <summary>Argumento con el que Kumquat se relanza: espera a que la instancia anterior termine.</summary>
    public const string RestartedFlag = "--restarted";

    /// <summary>winget: "ya está instalado y no hay actualización" (cuenta como éxito).</summary>
    private const int AlreadyInstalled = unchecked((int)0x8A15002B);

    /// <summary>Argumentos de winget para instalar el paquete exacto desde el repositorio oficial.</summary>
    public static IReadOnlyList<string> Arguments(EngineOffer offer)
    {
        ArgumentNullException.ThrowIfNull(offer);
        return ["install", "--id", offer.WingetId, "--exact", "--source", "winget", "--accept-package-agreements", "--accept-source-agreements"];
    }

    /// <summary>Indica si el código de salida de winget significa que el programa quedó instalado.</summary>
    public static bool Succeeded(int exitCode) => exitCode is 0 or AlreadyInstalled;

    /// <summary>Corre winget y espera a que termine; devuelve su código de salida.</summary>
    public static async Task<int> InstallAsync(string winget, EngineOffer offer)
    {
        // Kumquat no tiene consola: winget abre la suya, que se cierra sola al terminar.
        var startInfo = new ProcessStartInfo(winget) { UseShellExecute = false, CreateNoWindow = false };
        foreach (var argument in Arguments(offer))
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("No se pudo abrir winget.");
        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    /// <summary>Lanza una instancia nueva que espera a que esta salga.</summary>
    public static void Relaunch()
    {
        var startInfo = new ProcessStartInfo(Environment.ProcessPath ?? throw new InvalidOperationException("Ruta de Kumquat desconocida.")) { UseShellExecute = false };
        startInfo.ArgumentList.Add(RestartedFlag);
        using var _ = Process.Start(startInfo);
    }
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Diagnostics;
using Kumquat.Interaction.Native;
using Kumquat.Interaction.Wheel;

namespace Kumquat.App;

/// <summary>
/// Acciones del anillo exterior: inmediatas, sin motores y válidas para cualquier archivo o carpeta.
/// Todos los procesos se lanzan con ruta absoluta y argumentos separados.
/// </summary>
internal static class FileActions
{
    public const string CopyPath = "action.copy-path";
    public const string CopyTo = "action.copy-to";
    public const string MoveTo = "action.move-to";
    public const string ShowInFolder = "action.show-in-folder";
    public const string Terminal = "action.terminal";

    /// <summary>Acciones disponibles, en orden de la rueda (la primera queda arriba).</summary>
    public static IReadOnlyList<WheelItem> Items { get; } =
    [
        WheelItem.Action(CopyPath, "Copiar ruta"),
        WheelItem.Action(CopyTo, "Copiar a…"),
        WheelItem.Action(MoveTo, "Mover a…"),
        WheelItem.Action(ShowInFolder, "Ver en carpeta"),
        WheelItem.Action(Terminal, "Terminal aquí"),
    ];

    /// <summary>Ejecuta una acción. Devuelve un mensaje para notificar, o <c>null</c> si no hace falta.</summary>
    public static string? Execute(string actionId, IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count == 0)
        {
            return null;
        }

        switch (actionId)
        {
            case CopyPath:
                return ClipboardText.TrySet(string.Join(Environment.NewLine, paths))
                    ? (paths.Count == 1 ? "Ruta copiada" : $"{paths.Count} rutas copiadas")
                    : "No se pudo acceder al portapapeles";
            case ShowInFolder:
                Launch(WindowsTool("explorer.exe"), ["/select,", paths[0]], workingDirectory: null);
                return null;
            case Terminal:
                OpenTerminal(Directory.Exists(paths[0]) ? paths[0] : Path.GetDirectoryName(paths[0])!);
                return null;
            default:
                throw new ArgumentOutOfRangeException(nameof(actionId), actionId, "Acción desconocida.");
        }
    }

    private static void OpenTerminal(string directory)
    {
        // Windows Terminal si está instalado (alias de ejecución de la Store); si no, PowerShell.
        var windowsTerminal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "wt.exe");
        if (File.Exists(windowsTerminal))
        {
            Launch(windowsTerminal, ["-d", directory], workingDirectory: directory);
            return;
        }

        Launch(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"), ["-NoExit"], workingDirectory: directory);
    }

    private static string WindowsTool(string name) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), name);

    private static void Launch(string executable, IReadOnlyList<string> arguments, string? workingDirectory)
    {
        var startInfo = new ProcessStartInfo(executable) { UseShellExecute = false };
        if (workingDirectory is not null)
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var _ = Process.Start(startInfo);
    }
}

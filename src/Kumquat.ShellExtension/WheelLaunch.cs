// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Diagnostics;

namespace Kumquat.ShellExtension;

/// <summary>
/// Abre Kumquat con los archivos elegidos: <c>Kumquat.exe --wheel a b c</c>. Si la línea de
/// comandos no alcanza (miles de archivos), se reparte en varios lanzamientos: la instancia que
/// ya corre junta los pedidos que llegan seguidos y abre el buscador una sola vez.
/// </summary>
internal static class WheelLaunch
{
    /// <summary>Lo que acepta el menú clásico y la app (<c>ExplorerMenu.WheelFlag</c>).</summary>
    public const string WheelFlag = "--wheel";

    /// <summary>Margen bajo el límite de CreateProcess (32 767 caracteres).</summary>
    public const int MaxCommandLine = 30_000;

    /// <summary>Reparte las rutas en grupos que entren en una línea de comandos.</summary>
    public static List<List<string>> Batches(IReadOnlyList<string> paths, string executable, int maxLength = MaxCommandLine)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(executable);
        var fixedLength = executable.Length + 3 + WheelFlag.Length + 1;
        var batches = new List<List<string>>();
        var current = new List<string>();
        var length = fixedLength;
        foreach (var path in paths)
        {
            var cost = path.Length + 3; // comillas y espacio
            if (current.Count > 0 && length + cost > maxLength)
            {
                batches.Add(current);
                current = [];
                length = fixedLength;
            }

            current.Add(path);
            length += cost;
        }

        if (current.Count > 0)
        {
            batches.Add(current);
        }

        return batches;
    }

    /// <summary>Lanza Kumquat (sin shell: cada ruta es un argumento aparte).</summary>
    public static void Start(string executable, IReadOnlyList<string> paths)
    {
        foreach (var batch in Batches(paths, executable))
        {
            var startInfo = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(executable)! };
            startInfo.ArgumentList.Add(WheelFlag);
            foreach (var path in batch)
            {
                startInfo.ArgumentList.Add(path);
            }

            using var _ = Process.Start(startInfo);
        }
    }
}

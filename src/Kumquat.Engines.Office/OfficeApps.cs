// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Microsoft.Win32;

namespace Kumquat.Engines.Office;

/// <summary>Qué hay instalado para convertir documentos de oficina.</summary>
/// <param name="Word">Microsoft Word (automatización COM registrada).</param>
/// <param name="Excel">Microsoft Excel.</param>
/// <param name="PowerPoint">Microsoft PowerPoint.</param>
/// <param name="LibreOffice">Ruta de <c>soffice.exe</c>, o <c>null</c>.</param>
public sealed record OfficeApps(bool Word, bool Excel, bool PowerPoint, string? LibreOffice)
{
    /// <summary>Indica si se puede convertir algún documento de oficina.</summary>
    public bool Any => Word || Excel || PowerPoint || LibreOffice is not null;

    /// <summary>Busca Microsoft Office (por su registro COM) y LibreOffice (por su registro o carpeta).</summary>
    public static OfficeApps Detect() => new(
        HasComServer("Word.Application"),
        HasComServer("Excel.Application"),
        HasComServer("PowerPoint.Application"),
        FindLibreOffice());

    private static bool HasComServer(string progId)
    {
        using var key = Registry.ClassesRoot.OpenSubKey($@"{progId}\CLSID");
        return key?.GetValue(null) is string { Length: > 0 };
    }

    private static string? FindLibreOffice()
    {
        foreach (var root in new[] { @"SOFTWARE\LibreOffice\UNO\InstallPath", @"SOFTWARE\WOW6432Node\LibreOffice\UNO\InstallPath" })
        {
            using var key = Registry.LocalMachine.OpenSubKey(root);
            if (key?.GetValue(null) is string folder && File.Exists(Path.Combine(folder, "soffice.exe")))
            {
                return Path.Combine(folder, "soffice.exe");
            }
        }

        var programFiles = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LibreOffice", "program", "soffice.exe");
        return File.Exists(programFiles) ? programFiles : null;
    }
}

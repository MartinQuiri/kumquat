// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.Versioning;
using Kumquat.Infrastructure.Engines;
using Microsoft.Win32;

namespace Kumquat.Engines.Office;

/// <summary>Qué hay instalado para convertir documentos de oficina.</summary>
/// <param name="Word">Microsoft Word (automatización COM registrada).</param>
/// <param name="Excel">Microsoft Excel.</param>
/// <param name="PowerPoint">Microsoft PowerPoint.</param>
/// <param name="LibreOffice">Ruta de <c>soffice</c>, o <c>null</c>.</param>
public sealed record OfficeApps(bool Word, bool Excel, bool PowerPoint, string? LibreOffice)
{
    /// <summary>Indica si se puede convertir algún documento de oficina.</summary>
    public bool Any => Word || Excel || PowerPoint || LibreOffice is not null;

    /// <summary>
    /// Busca Microsoft Office (por su registro COM, solo en Windows) y LibreOffice (en Windows por su
    /// registro o carpeta; en macOS dentro de <c>LibreOffice.app</c>; en Linux y macOS, en el <c>PATH</c>).
    /// </summary>
    public static OfficeApps Detect() => OperatingSystem.IsWindows()
        ? new(HasComServer("Word.Application"), HasComServer("Excel.Application"), HasComServer("PowerPoint.Application"), FindLibreOffice())
        : new(Word: false, Excel: false, PowerPoint: false, FindLibreOfficeOnUnix());

    private static string? FindLibreOfficeOnUnix()
    {
        if (OperatingSystem.IsMacOS() && File.Exists("/Applications/LibreOffice.app/Contents/MacOS/soffice"))
        {
            return "/Applications/LibreOffice.app/Contents/MacOS/soffice";
        }

        // Los paquetes de Linux instalan "soffice"; algunos (Arch, Flatpak exportado) solo "libreoffice".
        var locator = new EngineLocator();
        return locator.FindExecutable("libreoffice", "soffice") ?? locator.FindExecutable("libreoffice", "libreoffice");
    }

    [SupportedOSPlatform("windows")]
    private static bool HasComServer(string progId)
    {
        using var key = Registry.ClassesRoot.OpenSubKey($@"{progId}\CLSID");
        return key?.GetValue(null) is string { Length: > 0 };
    }

    [SupportedOSPlatform("windows")]
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

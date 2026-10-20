// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Text;
using Kumquat.Core.Formats;
using Kumquat.Engines.Office;
using Kumquat.Infrastructure.Processes;

namespace Kumquat.Engines.Tests.Integration;

/// <summary>
/// Muestras reales de oficina hechas con el Microsoft Office instalado (Word, Excel, PowerPoint),
/// guardadas en cada formato: así se prueba la conversión con documentos que Office acepta. Sin
/// Office no hay muestra y esos tests se omiten.
/// </summary>
internal static class OfficeSamples
{
    public const string Text = "Kumquat convierte documentos";

    private static readonly OfficeApps Apps = OfficeApps.Detect();

    /// <summary>Aplicación y código de formato de SaveAs para cada formato.</summary>
    private static readonly Dictionary<string, (string App, int Code, Func<OfficeApps, bool> Installed)> Formats = new(StringComparer.Ordinal)
    {
        ["docx"] = ("Word", 16, a => a.Word),
        ["doc"] = ("Word", 0, a => a.Word),
        ["odt"] = ("Word", 23, a => a.Word),
        ["rtf"] = ("Word", 6, a => a.Word),
        ["xlsx"] = ("Excel", 51, a => a.Excel),
        ["xls"] = ("Excel", 56, a => a.Excel),
        ["ods"] = ("Excel", 60, a => a.Excel),
        ["pptx"] = ("PowerPoint", 24, a => a.PowerPoint),
        ["ppt"] = ("PowerPoint", 1, a => a.PowerPoint),
        ["odp"] = ("PowerPoint", 35, a => a.PowerPoint),
    };

    private const string Script = """
        param([string]$App, [string]$Path, [int]$Code, [string]$Text)
        $ErrorActionPreference = 'Stop'
        $name = @{ Word = 'WINWORD'; Excel = 'EXCEL'; PowerPoint = 'POWERPNT' }[$App]
        $before = @(Get-Process $name -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
        $o = New-Object -ComObject "$App.Application"
        $ours = @(Get-Process $name -ErrorAction SilentlyContinue | Where-Object { $before -notcontains $_.Id }).Count -ge 1
        try {
            $o.AutomationSecurity = 3
            switch ($App) {
                'Word' { $o.Visible = $false; $o.DisplayAlerts = 0; $d = $o.Documents.Add(); $d.Content.Text = $Text; $d.SaveAs2($Path, $Code); $d.Close(0) }
                'Excel' { $o.Visible = $false; $o.DisplayAlerts = $false; $b = $o.Workbooks.Add(); $b.Worksheets.Item(1).Range('A1').Value2 = $Text; $b.SaveAs($Path, $Code); $b.Close($false) }
                'PowerPoint' { $o.DisplayAlerts = 1; $p = $o.Presentations.Add(0); $s = $p.Slides.Add(1, 1); $s.Shapes.Item(1).TextFrame.TextRange.Text = $Text; $p.SaveAs($Path, $Code); $p.Close() }
            }
        }
        finally {
            if ($ours) { $o.Quit() }
        }
        """;

    /// <summary>Indica si hay Office para generar este formato.</summary>
    public static bool CanCreate(FileFormat format)
        => Formats.TryGetValue(format.Id, out var info) && info.Installed(Apps);

    /// <summary>Crea la muestra con Office; <c>null</c> si no se puede.</summary>
    public static async Task<string?> CreateAsync(FileFormat format, string path)
    {
        if (!Formats.TryGetValue(format.Id, out var info) || !info.Installed(Apps))
        {
            return null;
        }

        // Una cosa a la vez con Office (comparte el turno con el convertidor).
        await MicrosoftOffice.OneAtATime.WaitAsync();
        try
        {
            var script = Path.Combine(Path.GetDirectoryName(path)!, $"muestra-{format.Id}.ps1");
            await File.WriteAllTextAsync(script, Script, new UTF8Encoding(true));
            var outcome = await TestEngines.Runner.RunAsync(
                new ProcessSpec
                {
                    FileName = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
                    Arguments = ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script, "-App", info.App, "-Path", path, "-Code", info.Code.ToString(System.Globalization.CultureInfo.InvariantCulture), "-Text", Text],
                    Timeout = TimeSpan.FromMinutes(2),
                },
                CancellationToken.None);
            return outcome.Succeeded && File.Exists(path) ? path : null;
        }
        finally
        {
            MicrosoftOffice.OneAtATime.Release();
        }
    }
}

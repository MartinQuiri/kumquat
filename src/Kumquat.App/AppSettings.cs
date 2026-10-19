// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Text.Json;
using Kumquat.Interaction.Input;

namespace Kumquat.App;

/// <summary>Preferencias del usuario, persistidas en <c>%LOCALAPPDATA%\Kumquat\settings.json</c>.</summary>
internal sealed record AppSettings
{
    /// <summary>Gesto que abre la rueda.</summary>
    public TriggerKey Trigger { get; init; } = TriggerKey.Shift;

    /// <summary>Atajo global (Ctrl+Alt+K) para abrir la rueda sobre la selección del Explorador.</summary>
    public bool HotkeyEnabled { get; init; } = true;

    /// <summary>Entrada "Kumquat…" en el menú del clic derecho del Explorador.</summary>
    public bool ContextMenuEnabled { get; init; } = true;

    /// <summary>Abrir Kumquat al iniciar sesión (desactivado: lo decide la persona).</summary>
    public bool StartWithWindows { get; init; }

    /// <summary>
    /// Carpeta para los resultados de archivos sin carpeta (adjuntos de Outlook, archivos de dentro
    /// de un .zip, imágenes de una web). <c>null</c>: el Escritorio.
    /// </summary>
    public string? OutputFolder { get; init; }

    /// <summary>El Escritorio del usuario (la carpeta por defecto).</summary>
    public static string DefaultOutputFolder => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    /// <summary>Carpeta efectiva: la elegida si todavía existe; si no, el Escritorio.</summary>
    public string ResolvedOutputFolder => OutputFolder is { } folder && Directory.Exists(folder) ? folder : DefaultOutputFolder;

    /// <summary>Carga desde disco; ante cualquier problema devuelve los valores por defecto.</summary>
    public static AppSettings Load(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize(File.ReadAllText(path), AppSettingsJson.Default.AppSettings) ?? new AppSettings()
                : new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings(); // Archivo corrupto: no impedir el arranque.
        }
        catch (IOException)
        {
            return new AppSettings();
        }
    }

    /// <summary>Guarda de forma atómica (temporal + reemplazo).</summary>
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, AppSettingsJson.Default.AppSettings));
        File.Move(temp, path, overwrite: true);
    }
}

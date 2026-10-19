// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Microsoft.Win32;

namespace Kumquat.App;

/// <summary>Abrir Kumquat al iniciar sesión: la clave Run del usuario (sin administrador).</summary>
internal static class Autostart
{
    private const string ValueName = "Kumquat";

    /// <summary>Clave Run (los tests usan otra).</summary>
    public static string RunKey { get; set; } = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>Indica si está activo para este ejecutable.</summary>
    public static bool IsEnabledFor(string executable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string value && string.Equals(value, Command(executable), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Activa (corrigiendo la ruta si se movió la carpeta) o desactiva.</summary>
    public static void Set(bool enabled, string executable)
    {
        ArgumentException.ThrowIfNullOrEmpty(executable);
        if (!enabled)
        {
            using var existing = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            existing?.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        if (!IsEnabledFor(executable))
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            key.SetValue(ValueName, Command(executable));
        }
    }

    private static string Command(string executable) => $"\"{executable}\"";
}

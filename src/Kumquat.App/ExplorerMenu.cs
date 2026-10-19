// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Microsoft.Win32;

namespace Kumquat.App;

/// <summary>
/// Entrada "Kumquat" en el menú del clic derecho del Explorador, para archivos y carpetas. Se
/// registra por usuario (HKCU, sin permisos de administrador). En Windows 11 aparece en "Mostrar más
/// opciones"; el menú nuevo exige una app empaquetada y firmada (llegará con el instalador).
/// </summary>
internal static class ExplorerMenu
{
    /// <summary>Argumento con el que el Explorador lanza a Kumquat.</summary>
    public const string WheelFlag = "--wheel";

    private const string VerbName = "Kumquat";

    /// <summary>Tipos a los que se agrega: todos los archivos y las carpetas.</summary>
    private static readonly string[] Targets = [@"*\shell", @"Directory\shell"];

    /// <summary>Raíz de las clases del usuario (los tests usan otra).</summary>
    public static string ClassesRoot { get; set; } = @"Software\Classes";

    /// <summary>Indica si la entrada está registrada y apunta a este ejecutable.</summary>
    public static bool IsRegisteredFor(string executable)
    {
        using var command = Registry.CurrentUser.OpenSubKey($@"{ClassesRoot}\{Targets[0]}\{VerbName}\command");
        return command?.GetValue(null) is string value && string.Equals(value, Command(executable), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Crea o actualiza la entrada (si se movió el ejecutable, la corrige).</summary>
    public static void Register(string executable)
    {
        ArgumentException.ThrowIfNullOrEmpty(executable);
        foreach (var target in Targets)
        {
            using var verb = Registry.CurrentUser.CreateSubKey($@"{ClassesRoot}\{target}\{VerbName}");
            verb.SetValue("MUIVerb", "Kumquat…");
            verb.SetValue("Icon", $"\"{executable}\",0");

            // "Player": se muestra con cualquier cantidad de elementos seleccionados.
            verb.SetValue("MultiSelectModel", "Player");
            using var command = verb.CreateSubKey("command");
            command.SetValue(null, Command(executable));
        }
    }

    /// <summary>Quita la entrada (no falla si no estaba).</summary>
    public static void Unregister()
    {
        foreach (var target in Targets)
        {
            Registry.CurrentUser.DeleteSubKeyTree($@"{ClassesRoot}\{target}\{VerbName}", throwOnMissingSubKey: false);
        }
    }

    /// <summary>Línea de comandos que ejecuta el Explorador: ruta entre comillas y el archivo como argumento aparte.</summary>
    public static string Command(string executable) => $"\"{executable}\" {WheelFlag} \"%1\"";
}

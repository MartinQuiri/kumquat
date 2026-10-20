// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Globalization;
using System.Resources;

namespace Kumquat.Core.Localization;

/// <summary>
/// Textos de la interfaz en el idioma actual (<see cref="CultureInfo.CurrentUICulture"/>). El
/// español está en el ensamblado y los demás idiomas en satélites (<c>Strings.&lt;idioma&gt;.resx</c>).
/// Los miembros los genera <c>tools/build-strings.py</c> en <c>Strings.g.cs</c>.
/// </summary>
public static partial class Strings
{
    private static readonly ResourceManager Resources = new("Kumquat.Core.Localization.Strings", typeof(Strings).Assembly);

    /// <summary>Idiomas de la interfaz (código, nombre en su propio idioma).</summary>
    public static IReadOnlyList<(string Code, string Name)> Languages { get; } = [("es", "Español"), ("en", "English")];

    /// <summary>
    /// Idioma a usar: el elegido, o con "auto" el de Windows si está traducido (si no, inglés).
    /// </summary>
    /// <param name="choice">"auto" o un código de <see cref="Languages"/>.</param>
    /// <param name="system">Idioma de Windows.</param>
    public static CultureInfo Resolve(string? choice, CultureInfo system)
    {
        ArgumentNullException.ThrowIfNull(system);
        var code = Languages.Any(l => string.Equals(l.Code, choice, StringComparison.OrdinalIgnoreCase))
            ? choice!
            : Languages.Any(l => string.Equals(l.Code, system.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase))
                ? system.TwoLetterISOLanguageName
                : "en";

        // Con la cultura de Windows si es del mismo idioma (es-MX conserva su formato de números).
        return string.Equals(system.TwoLetterISOLanguageName, code, StringComparison.OrdinalIgnoreCase) ? system : CultureInfo.GetCultureInfo(code);
    }

    /// <summary>Texto por clave en el idioma actual (pruebas y diagnósticos).</summary>
    public static string Get(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    private static string Format(string key, params object?[] arguments)
        => string.Format(CultureInfo.CurrentCulture, Get(key), arguments);
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Globalization;
using System.Runtime.CompilerServices;

namespace Kumquat.Tests.Shared;

/// <summary>
/// Los tests verifican los textos en español (el idioma base) y con formato de números español,
/// sea cual sea el idioma del equipo o del runner de CI. Los tests de otros idiomas lo cambian a mano.
/// </summary>
internal static class SpanishCulture
{
#pragma warning disable CA2255 // Inicializador de módulo a propósito: antes de cualquier test.
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Apply()
    {
        var spanish = CultureInfo.GetCultureInfo("es-ES");
        CultureInfo.DefaultThreadCurrentCulture = spanish;
        CultureInfo.DefaultThreadCurrentUICulture = spanish;
        CultureInfo.CurrentCulture = spanish;
        CultureInfo.CurrentUICulture = spanish;
    }
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Design;

/// <summary>Acceso a los trazados generados (ver <c>tools/build-design-assets.py</c>).</summary>
public static partial class IconPaths
{
    /// <summary>Nombres lógicos de los íconos.</summary>
    public static IEnumerable<string> Names => Data.Keys;

    /// <summary>Atributo <c>d</c> del ícono, o <c>null</c> si no existe.</summary>
    public static string? Get(string name) => Data.GetValueOrDefault(name);
}

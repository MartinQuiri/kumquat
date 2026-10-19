// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Core.Tools;

/// <summary>Un dato que la herramienta necesita del usuario antes de ejecutarse.</summary>
/// <param name="Key">Clave en el diccionario de opciones.</param>
/// <param name="Label">Etiqueta visible.</param>
/// <param name="Kind">Tipo de dato.</param>
/// <param name="DefaultValue">Valor inicial (p. ej. el metadato actual o la duración del video).</param>
/// <param name="Hint">Ayuda breve bajo el campo.</param>
/// <param name="Required">Si no puede quedar vacío.</param>
public sealed record ToolParameter(string Key, string Label, ToolParameterKind Kind, string? DefaultValue = null, string? Hint = null, bool Required = false)
{
    /// <summary>Prefijo de las claves de campos personalizados.</summary>
    public const string CustomPrefix = "custom:";
}

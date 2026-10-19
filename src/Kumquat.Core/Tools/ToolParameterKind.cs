// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Core.Tools;

/// <summary>Tipo de dato que pide un parámetro (decide el control de la ventana de opciones).</summary>
public enum ToolParameterKind
{
    /// <summary>Texto libre.</summary>
    Text,

    /// <summary>Contraseña (no se muestra ni se registra).</summary>
    Password,

    /// <summary>Instante de un medio (<c>1:23</c>, <c>01:02:03.5</c>, segundos).</summary>
    Time,

    /// <summary>Rango de páginas (<c>1-3, 5, 8-z</c>).</summary>
    Pages,

    /// <summary>Marca que permite agregar campos personalizados (<c>custom:Nombre</c>).</summary>
    CustomFields,
}

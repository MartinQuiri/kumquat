// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Formats;

namespace Kumquat.Core.Tools;

/// <summary>Una entrada de herramienta: archivo con su formato real, o carpeta (formato <c>null</c>).</summary>
/// <param name="Path">Ruta absoluta.</param>
/// <param name="Format">Formato detectado, o <c>null</c> si es una carpeta.</param>
public sealed record ToolInput(string Path, FileFormat? Format)
{
    /// <summary>Indica si es una carpeta.</summary>
    public bool IsDirectory => Format is null;
}

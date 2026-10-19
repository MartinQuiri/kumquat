// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Core.Conversion;

/// <summary>Carpeta temporal propia de un trabajo; se borra al liberar.</summary>
public interface ITempWorkspace : IDisposable
{
    /// <summary>Ruta absoluta de la carpeta.</summary>
    string DirectoryPath { get; }

    /// <summary>Ruta nueva y única dentro de la carpeta (el archivo no se crea).</summary>
    /// <param name="extension">Extensión con punto, p. ej. <c>.png</c>.</param>
    string NewFilePath(string extension);
}

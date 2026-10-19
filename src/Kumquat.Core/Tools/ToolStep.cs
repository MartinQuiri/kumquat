// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Formats;

namespace Kumquat.Core.Tools;

/// <summary>Una aplicación concreta de una herramienta.</summary>
/// <param name="Inputs">Entradas (una en modo <see cref="ToolMode.EachFile"/>; ordenadas en modo combinar).</param>
/// <param name="OutputPath">Dónde escribir: un archivo, o una carpeta ya creada y vacía si la salida es carpeta.</param>
/// <param name="OutputFormat">Formato del resultado, o <c>null</c> si es una carpeta.</param>
public sealed record ToolStep(IReadOnlyList<ToolInput> Inputs, string OutputPath, FileFormat? OutputFormat)
{
    /// <summary>Primera (o única) entrada.</summary>
    public string InputPath => Inputs[0].Path;

    /// <summary>Formato de la primera entrada (las herramientas de archivos siempre lo tienen).</summary>
    public FileFormat InputFormat => Inputs[0].Format ?? throw new InvalidOperationException("La entrada es una carpeta.");
}

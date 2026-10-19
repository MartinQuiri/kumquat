// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Core.Conversion;

/// <summary>Un paso concreto a ejecutar: un archivo de entrada y uno de salida.</summary>
/// <param name="InputPath">Ruta del archivo de entrada.</param>
/// <param name="OutputPath">Ruta donde escribir la salida (ya incluye la extensión correcta).</param>
/// <param name="Edge">Arista del grafo que se ejecuta.</param>
public sealed record ConversionStep(string InputPath, string OutputPath, ConversionEdge Edge);

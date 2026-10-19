// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Formats;

namespace Kumquat.Core.Conversion;

/// <summary>Pedido de conversión de un archivo.</summary>
/// <param name="InputPath">Archivo de origen.</param>
/// <param name="Target">Formato deseado.</param>
/// <param name="OutputDirectory">Carpeta de salida; por defecto la del origen.</param>
public sealed record ConversionRequest(string InputPath, FileFormat Target, string? OutputDirectory = null);

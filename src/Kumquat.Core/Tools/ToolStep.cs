// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Formats;

namespace Kumquat.Core.Tools;

/// <summary>Una aplicación concreta de una herramienta.</summary>
/// <param name="InputPath">Archivo de entrada.</param>
/// <param name="OutputPath">Dónde escribir el resultado.</param>
/// <param name="InputFormat">Formato real de la entrada.</param>
/// <param name="OutputFormat">Formato del resultado.</param>
public sealed record ToolStep(string InputPath, string OutputPath, FileFormat InputFormat, FileFormat OutputFormat);

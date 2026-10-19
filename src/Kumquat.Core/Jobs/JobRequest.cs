// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Results;

namespace Kumquat.Core.Jobs;

/// <summary>Un trabajo encolable: una conversión, una herramienta o cualquier operación sobre un archivo.</summary>
/// <param name="InputPath">Archivo de origen (para notificaciones y logs).</param>
/// <param name="Description">Qué se hace, legible (p. ej. "a JPG", "Comprimir").</param>
/// <param name="Work">La operación; devuelve la ruta del archivo creado.</param>
public sealed record JobRequest(string InputPath, string Description, Func<IProgress<double>, CancellationToken, Task<Result<string>>> Work);

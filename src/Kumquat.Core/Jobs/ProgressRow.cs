// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Core.Jobs;

/// <summary>Una fila del panel de progreso.</summary>
/// <param name="Id">Trabajo (para cancelarlo).</param>
/// <param name="FileName">Nombre del archivo.</param>
/// <param name="Description">Qué se le hace ("a PNG", "Comprimir").</param>
/// <param name="IsRunning">En curso (si no, espera en la cola).</param>
/// <param name="Progress">0..1, o <c>null</c> si el motor todavía no informó avance.</param>
/// <param name="Remaining">Tiempo estimado que falta, o <c>null</c> si aún no hay datos para estimarlo.</param>
public sealed record ProgressRow(Guid Id, string FileName, string Description, bool IsRunning, double? Progress, TimeSpan? Remaining);

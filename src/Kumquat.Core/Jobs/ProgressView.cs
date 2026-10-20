// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Core.Jobs;

/// <summary>Lo que muestra el panel en un momento.</summary>
/// <param name="Visible">Si el panel tiene que estar a la vista.</param>
/// <param name="Rows">Las filas: primero los trabajos en curso, después los de la cola (hasta <see cref="ProgressBoard.MaxRows"/>).</param>
/// <param name="Running">Trabajos en curso.</param>
/// <param name="Queued">Trabajos en la cola.</param>
/// <param name="Hidden">Trabajos activos que no entran en las filas.</param>
public sealed record ProgressView(bool Visible, IReadOnlyList<ProgressRow> Rows, int Running, int Queued, int Hidden);

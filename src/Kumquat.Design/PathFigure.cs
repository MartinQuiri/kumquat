// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Design;

/// <summary>Un subtrazado: punto inicial, tramos y si se cierra.</summary>
/// <param name="Start">Punto inicial.</param>
/// <param name="Segments">Tramos, en orden.</param>
/// <param name="Closed">Indica si termina en <c>Z</c>.</param>
public sealed record PathFigure(PathPoint Start, IReadOnlyList<PathSegment> Segments, bool Closed);

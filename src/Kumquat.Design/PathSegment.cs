// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Design;

/// <summary>Tramo de un trazado: una recta hasta <see cref="End"/> o una Bézier cúbica.</summary>
/// <param name="IsCubic">Indica si es una Bézier cúbica (si no, una recta).</param>
/// <param name="Control1">Primer punto de control (solo cúbicas).</param>
/// <param name="Control2">Segundo punto de control (solo cúbicas).</param>
/// <param name="End">Punto final.</param>
public readonly record struct PathSegment(bool IsCubic, PathPoint Control1, PathPoint Control2, PathPoint End)
{
    /// <summary>Recta hasta un punto.</summary>
    public static PathSegment Line(PathPoint end) => new(false, default, default, end);

    /// <summary>Bézier cúbica.</summary>
    public static PathSegment Cubic(PathPoint control1, PathPoint control2, PathPoint end) => new(true, control1, control2, end);
}

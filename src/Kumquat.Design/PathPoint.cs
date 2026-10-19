// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Design;

/// <summary>Punto de un trazado, en unidades del viewBox.</summary>
/// <param name="X">Coordenada horizontal.</param>
/// <param name="Y">Coordenada vertical (hacia abajo).</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct PathPoint(double X, double Y);

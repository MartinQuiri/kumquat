// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Interaction.Wheel;

/// <summary>Un anillo con sus sectores.</summary>
/// <param name="Kind">Tipo de anillo.</param>
/// <param name="Items">Sectores; el 0 queda centrado arriba.</param>
public sealed record WheelRing(WheelRingKind Kind, IReadOnlyList<WheelItem> Items);

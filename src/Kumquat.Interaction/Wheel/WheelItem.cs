// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Formats;

namespace Kumquat.Interaction.Wheel;

/// <summary>Un sector de la rueda.</summary>
/// <param name="Label">Texto corto (p. ej. <c>PNG</c>).</param>
/// <param name="Target">Formato de destino.</param>
public sealed record WheelItem(string Label, FileFormat Target);

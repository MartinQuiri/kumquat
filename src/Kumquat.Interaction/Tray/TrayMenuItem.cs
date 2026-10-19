// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Interaction.Tray;

/// <summary>Entrada del menú contextual de la bandeja. <c>null</c> en <see cref="Text"/> = separador.</summary>
/// <param name="Text">Texto, o <c>null</c> para un separador.</param>
/// <param name="OnClick">Acción; <c>null</c> = deshabilitada.</param>
public sealed record TrayMenuItem(string? Text, Action? OnClick)
{
    /// <summary>Separador.</summary>
    public static TrayMenuItem Separator { get; } = new(null, null);
}

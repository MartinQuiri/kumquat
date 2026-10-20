// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Interaction.Tests;

/// <summary>
/// Tests que crean ventanas WPF: corren en serie. WPF admite varios hilos de UI, pero sus cachés
/// internos de carga de temas (BAML) no toleran que varios hilos los llenen a la vez
/// (KeyNotFoundException en WpfXamlType). En la app hay un solo hilo de UI.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WpfWindows
{
    public const string Name = "Ventanas WPF";
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Diagnostics;

namespace Kumquat.Infrastructure.Processes;

/// <summary>Agrupa el proceso de un motor y sus hijos para limitarlos y terminarlos juntos.</summary>
internal interface IProcessGroup : IDisposable
{
    /// <summary>Agrega el proceso recién lanzado (y, con él, los hijos que cree).</summary>
    void Add(Process process);

    /// <summary>Termina el proceso y todos sus descendientes.</summary>
    void Terminate();
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Core.Jobs;

/// <summary>Argumentos del evento <see cref="JobRunner.JobChanged"/>.</summary>
public sealed class JobChangedEventArgs : EventArgs
{
    /// <summary>Crea los argumentos.</summary>
    public JobChangedEventArgs(JobSnapshot snapshot) => Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));

    /// <summary>Estado del trabajo tras el cambio.</summary>
    public JobSnapshot Snapshot { get; }
}

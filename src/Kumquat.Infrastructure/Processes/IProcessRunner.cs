// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Infrastructure.Processes;

/// <summary>Único punto de entrada permitido para lanzar procesos externos.</summary>
public interface IProcessRunner
{
    /// <summary>Ejecuta un proceso y espera a que termine.</summary>
    /// <exception cref="OperationCanceledException">Si <paramref name="cancellationToken"/> se cancela; el proceso se termina.</exception>
    Task<ProcessOutcome> RunAsync(ProcessSpec spec, CancellationToken cancellationToken);
}

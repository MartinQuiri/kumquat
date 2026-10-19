// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Infrastructure.Processes;

/// <summary>Resultado de un proceso de motor.</summary>
/// <param name="ExitCode">Código de salida (o -1 si se terminó por tiempo).</param>
/// <param name="StandardErrorTail">Últimas líneas de stderr, para diagnósticos.</param>
/// <param name="TimedOut">Indica si se superó el tiempo máximo.</param>
public sealed record ProcessOutcome(int ExitCode, string StandardErrorTail, bool TimedOut)
{
    /// <summary>Terminó con código 0 dentro del tiempo.</summary>
    public bool Succeeded => ExitCode == 0 && !TimedOut;
}

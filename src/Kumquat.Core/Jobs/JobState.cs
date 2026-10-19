// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Core.Jobs;

/// <summary>Estado de un trabajo.</summary>
public enum JobState
{
    /// <summary>En cola.</summary>
    Queued,

    /// <summary>Ejecutándose.</summary>
    Running,

    /// <summary>Terminó bien.</summary>
    Succeeded,

    /// <summary>Terminó con error.</summary>
    Failed,

    /// <summary>Cancelado.</summary>
    Cancelled,
}

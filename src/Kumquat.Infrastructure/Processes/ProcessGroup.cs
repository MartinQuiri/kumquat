// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Infrastructure.Processes;

/// <summary>Elige la implementación del sistema operativo.</summary>
internal static class ProcessGroup
{
    /// <summary>Crea un grupo: Job Object en Windows, árbol de procesos en Linux y macOS.</summary>
    /// <param name="maxProcessMemoryBytes">Memoria máxima de cada proceso, o <c>null</c> sin límite.</param>
    public static IProcessGroup Create(long? maxProcessMemoryBytes)
        => OperatingSystem.IsWindows() ? new JobObject(maxProcessMemoryBytes) : new UnixProcessTree(maxProcessMemoryBytes);
}

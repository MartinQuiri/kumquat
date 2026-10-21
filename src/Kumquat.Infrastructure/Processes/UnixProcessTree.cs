// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Kumquat.Infrastructure.Processes;

/// <summary>
/// Equivalente del Job Object en Linux y macOS: al terminar se mata el árbol completo (incluidos los
/// nietos) y, en Linux, la memoria se acota con <c>prlimit(RLIMIT_DATA)</c>. macOS no permite fijar
/// límites a otro proceso: ahí solo se garantiza la terminación.
/// </summary>
/// <remarks>
/// A diferencia del Job Object, si Kumquat se cae los motores en curso no mueren con él; terminan
/// solos al quedarse sin quien lea su salida.
/// </remarks>
internal sealed partial class UnixProcessTree(long? maxProcessMemoryBytes) : IProcessGroup
{
    private const int RlimitData = 2;
    private const int NoSuchProcess = 3; // ESRCH

    private Process? _process;

    public void Add(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);
        _process = process;
        if (maxProcessMemoryBytes is { } max && OperatingSystem.IsLinux())
        {
            var limit = new RLimit { Current = (ulong)max, Maximum = (ulong)max };
            // ESRCH: el motor ya terminó (los muy rápidos lo hacen antes de llegar aquí); no hay nada que limitar.
            if (Prlimit(process.Id, RlimitData, in limit, IntPtr.Zero) != 0 && Marshal.GetLastPInvokeError() is var error && error != NoSuchProcess)
            {
                throw new Win32Exception(error);
            }
        }
    }

    public void Terminate()
    {
        try
        {
            _process?.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Ya había terminado.
        }
        catch (Win32Exception)
        {
            // Idem: terminó entre la enumeración del árbol y la señal.
        }
    }

    public void Dispose()
    {
        // El Process lo libera quien lo creó.
    }

    [LibraryImport("libc", EntryPoint = "prlimit", SetLastError = true)]
    private static partial int Prlimit(int pid, int resource, in RLimit newLimit, IntPtr oldLimit);

    [StructLayout(LayoutKind.Sequential)]
    private struct RLimit
    {
        public ulong Current;
        public ulong Maximum;
    }
}

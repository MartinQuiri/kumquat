// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;

namespace Kumquat.Interaction.Native;

/// <summary>
/// Le pide a Windows que no frene a Kumquat. En portátiles, Windows 11 baja la velocidad de los
/// procesos en segundo plano (EcoQoS), y Kumquat, que vive en la bandeja, siempre lo está: la rueda
/// tardaría en aparecer y Windows quitaría los hooks de entrada. En reposo Kumquat no consume CPU,
/// así que no cambia la batería.
/// </summary>
internal static unsafe partial class PowerThrottling
{
    private const int ProcessPowerThrottlingClass = 4;
    private const int ThreadPowerThrottlingClass = 3;
    private const uint ExecutionSpeed = 0x1;

    /// <summary>Todo el proceso (la rueda también responde al arrastre).</summary>
    public static bool DisableForProcess()
    {
        var state = new State { Version = 1, ControlMask = ExecutionSpeed, StateMask = 0 };
        return SetProcessInformation(GetCurrentProcess(), ProcessPowerThrottlingClass, &state, (uint)sizeof(State));
    }

    /// <summary>El hilo que llama (el de los hooks).</summary>
    public static bool DisableForCurrentThread()
    {
        var state = new State { Version = 1, ControlMask = ExecutionSpeed, StateMask = 0 };
        return SetThreadInformation(GetCurrentThread(), ThreadPowerThrottlingClass, &state, (uint)sizeof(State));
    }

    [LibraryImport("kernel32.dll")]
    private static partial nint GetCurrentProcess();

    [LibraryImport("kernel32.dll")]
    private static partial nint GetCurrentThread();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetProcessInformation(nint process, int informationClass, void* information, uint size);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetThreadInformation(nint thread, int informationClass, void* information, uint size);

    /// <summary>PROCESS_POWER_THROTTLING_STATE y THREAD_POWER_THROTTLING_STATE (misma forma).</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct State
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }
}

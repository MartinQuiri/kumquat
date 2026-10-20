// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Kumquat.ShellExtension;

/// <summary>
/// Puntos de entrada de la DLL nativa (Native AOT) que llama COM al cargar el comando, y su fábrica.
/// Kumquat.exe está junto a la DLL (la carpeta de instalación).
/// </summary>
[GeneratedComClass]
internal sealed unsafe partial class Exports : IClassFactory
{
    /// <summary>CLSID del comando (lo declara packaging/sparse/AppxManifest.xml).</summary>
    public static readonly Guid Clsid = new("8C6B4A43-0B7E-4A57-9C3B-2C1E5F0D7A21");

    private const int HrOk = 0;
    private const int HrFalse = 1;
    private const int ClassENoAggregation = unchecked((int)0x80040110);
    private const int ClassEClassNotAvailable = unchecked((int)0x80040111);
    private const int HrPointer = unchecked((int)0x80004003);

    private static readonly StrategyBasedComWrappers ComWrappers = new();
    private static readonly Lazy<string> Executable = new(() => Path.Combine(Path.GetDirectoryName(ModulePath())!, "Kumquat.exe"));

    private readonly string _executable;

    internal Exports(string executable) => _executable = executable;

    public int CreateInstance(nint outer, in Guid iid, out nint instance)
    {
        instance = 0;
        return outer != 0 ? ClassENoAggregation : QueryInterface(new KumquatCommand(_executable), iid, out instance);
    }

    public int LockServer(int lockServer) => HrOk;

    /// <summary>La fábrica de COM para un CLSID (aquí hay uno solo).</summary>
    internal static int GetClassObject(in Guid clsid, in Guid iid, string executable, out nint result)
    {
        result = 0;
        return clsid != Clsid ? ClassEClassNotAvailable : QueryInterface(new Exports(executable), iid, out result);
    }

    [UnmanagedCallersOnly(EntryPoint = "DllGetClassObject")]
    private static int DllGetClassObject(Guid* clsid, Guid* iid, nint* result)
    {
        if (result is null || clsid is null || iid is null)
        {
            return HrPointer;
        }

        var hr = GetClassObject(*clsid, *iid, Executable.Value, out var factory);
        *result = factory;
        return hr;
    }

    /// <summary>El runtime de Native AOT no se puede descargar: la DLL queda hasta que se cierre el Explorador.</summary>
    [UnmanagedCallersOnly(EntryPoint = "DllCanUnloadNow")]
    private static int DllCanUnloadNow() => HrFalse;

    private static int QueryInterface(object instance, in Guid iid, out nint result)
    {
        var unknown = ComWrappers.GetOrCreateComInterfaceForObject(instance, CreateComInterfaceFlags.None);
        try
        {
            return Marshal.QueryInterface(unknown, in iid, out result);
        }
        finally
        {
            Marshal.Release(unknown);
        }
    }

    /// <summary>Ruta de esta DLL (no la del proceso: corre dentro de explorer.exe).</summary>
    private static string ModulePath()
    {
        const uint FromAddress = 0x4;
        const uint UnchangedRefCount = 0x2;
        delegate* unmanaged<int> self = &DllCanUnloadNow;
        if (!NativeMethods.GetModuleHandleExW(FromAddress | UnchangedRefCount, (nint)self, out var module))
        {
            throw new InvalidOperationException("No se encontró el módulo de la extensión.");
        }

        var buffer = new char[32768];
        fixed (char* chars = buffer)
        {
            var length = NativeMethods.GetModuleFileNameW(module, chars, (uint)buffer.Length);
            return new string(buffer, 0, (int)length);
        }
    }

    private static partial class NativeMethods
    {
        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetModuleHandleExW(uint flags, nint address, out nint module);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        public static partial uint GetModuleFileNameW(nint module, char* fileName, uint size);
    }
}

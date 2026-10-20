// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Kumquat.ShellExtension;

/// <summary>
/// "Kumquat…" en el menú contextual moderno. Corre dentro de explorer.exe: nunca deja escapar una
/// excepción (se devuelve un HRESULT) y no hace nada lento al dibujar el menú.
/// </summary>
[GeneratedComClass]
internal sealed partial class KumquatCommand(string executable) : IExplorerCommand
{
    /// <summary>Identidad del comando (no cambiar: el Explorador la recuerda).</summary>
    public static readonly Guid CommandName = new("5F2E32C7-9290-4C4E-8B5D-B1AD6F9943BA");

    public const string Title = "Kumquat…";

    private const int HrOk = 0;
    private const int HrNotImplemented = unchecked((int)0x80004001);
    private const int HrFail = unchecked((int)0x80004005);
    private const uint EcsEnabled = 0;
    private const uint EcsHidden = 2;
    private const uint SigdnFileSystemPath = 0x80058000;

    /// <summary>Al dibujar el menú se miran como mucho estos elementos (selecciones enormes).</summary>
    private const int CheckedForState = 16;

    private static readonly StrategyBasedComWrappers ComWrappers = new();

    public int GetTitle(nint itemArray, out nint name)
    {
        name = Marshal.StringToCoTaskMemUni(Title);
        return HrOk;
    }

    public int GetIcon(nint itemArray, out nint icon)
    {
        icon = Marshal.StringToCoTaskMemUni(executable + ",0");
        return HrOk;
    }

    public int GetToolTip(nint itemArray, out nint tooltip)
    {
        tooltip = 0;
        return HrNotImplemented;
    }

    public int GetCanonicalName(out Guid name)
    {
        name = CommandName;
        return HrOk;
    }

    /// <summary>Visible solo para archivos y carpetas reales (no Panel de control, Papelera, etc.).</summary>
    public int GetState(nint itemArray, int okToBeSlow, out uint state)
    {
        state = File.Exists(executable) && Paths(itemArray, CheckedForState) is { Count: > 0 } ? EcsEnabled : EcsHidden;
        return HrOk;
    }

    public int Invoke(nint itemArray, nint bindContext)
    {
#pragma warning disable CA1031 // Frontera COM dentro de explorer.exe: una excepción tiraría el Explorador.
        try
        {
            if (Paths(itemArray, int.MaxValue) is not { Count: > 0 } paths)
            {
                return HrFail;
            }

            WheelLaunch.Start(executable, paths);
            return HrOk;
        }
        catch (Exception)
        {
            return HrFail;
        }
#pragma warning restore CA1031
    }

    public int GetFlags(out uint flags)
    {
        flags = 0; // ECF_DEFAULT: un comando simple, sin submenú.
        return HrOk;
    }

    public int EnumSubCommands(out nint enumerator)
    {
        enumerator = 0;
        return HrNotImplemented;
    }

    /// <summary>Rutas de los elementos (hasta <paramref name="limit"/>); <c>null</c> si alguno no es del disco.</summary>
    internal static List<string>? Paths(nint itemArray, int limit)
    {
        if (itemArray == 0)
        {
            return null;
        }

        var items = (IShellItemArray)ComWrappers.GetOrCreateObjectForComInstance(itemArray, CreateObjectFlags.None);
        if (items.GetCount(out var count) < 0)
        {
            return null;
        }

        var paths = new List<string>((int)Math.Min(count, (uint)Math.Min(limit, 4096)));
        for (uint i = 0; i < count && i < limit; i++)
        {
            if (items.GetItemAt(i, out var pointer) < 0 || pointer == 0)
            {
                return null;
            }

            try
            {
                var item = (IShellItem)ComWrappers.GetOrCreateObjectForComInstance(pointer, CreateObjectFlags.None);
                if (item.GetDisplayName(SigdnFileSystemPath, out var name) < 0 || name == 0)
                {
                    return null;
                }

                paths.Add(Marshal.PtrToStringUni(name)!);
                Marshal.FreeCoTaskMem(name);
            }
            finally
            {
                Marshal.Release(pointer);
            }
        }

        return paths;
    }
}

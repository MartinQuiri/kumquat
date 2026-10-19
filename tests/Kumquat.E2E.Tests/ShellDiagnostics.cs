// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using Kumquat.Interaction.DragDrop;
using Kumquat.Interaction.Shell;

namespace Kumquat.E2E.Tests;

/// <summary>Volcado de las ventanas del shell y su selección, para entender un E2E que falla.</summary>
internal static unsafe partial class ShellDiagnostics
{
    private static readonly Guid TopLevelBrowser = new("4C96BE40-915C-11CF-99D3-00AA004AE837");
    private static readonly Guid DataObject = new("0000010E-0000-0000-C000-000000000046");

    /// <summary>Debe llamarse en un hilo STA.</summary>
    public static string Describe()
    {
        var report = new StringBuilder();
        var foreground = GetForegroundWindow();
        report.Append(CultureInfo.InvariantCulture, $"foreground=0x{foreground:X} primeraPestaña=0x{FindWindowExW(foreground, 0, "ShellTabWindowClass", null):X};");
        if (CoCreateInstance(new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39"), 0, 0x17, typeof(IShellWindows).GUID, out var pointer) != 0)
        {
            return report.Append(" sin IShellWindows").ToString();
        }

        var windows = ComInterfaceMarshaller<IShellWindows>.ConvertToManaged((void*)pointer)!;
        windows.GetCount(out var count);
        for (var i = 0; i < count; i++)
        {
            if (windows.Item(Variant.Int32(i), out var dispatch) != 0 || dispatch == 0)
            {
                continue;
            }

            var provider = ComInterfaceMarshaller<Kumquat.Interaction.Shell.IServiceProvider>.ConvertToManaged((void*)dispatch)!;
            if (provider.QueryService(TopLevelBrowser, typeof(IShellBrowser).GUID, out var raw) != 0)
            {
                continue;
            }

            var browser = ComInterfaceMarshaller<IShellBrowser>.ConvertToManaged((void*)raw)!;
            browser.GetWindow(out var hwnd);
            var selected = "?";
            if (browser.QueryActiveShellView(out var view) == 0)
            {
                var hr = view.GetItemObject(1, DataObject, out var data);
                selected = hr == 0 ? DataObjectReader.ReadFiles(data).Count.ToString(CultureInfo.InvariantCulture) : $"hr=0x{hr:X8}";
            }

            report.Append(CultureInfo.InvariantCulture, $" [{i}] pestaña=0x{hwnd:X} raíz=0x{GetAncestor(hwnd, 2):X} selección={selected};");
        }

        return report.ToString();
    }

    /// <summary>Selecciona un archivo en la ventana del Explorador dada (hilo STA).</summary>
    public static bool Select(nint window, string file)
    {
        if (CoCreateInstance(new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39"), 0, 0x17, typeof(IShellWindows).GUID, out var pointer) != 0)
        {
            return false;
        }

        var windows = ComInterfaceMarshaller<IShellWindows>.ConvertToManaged((void*)pointer)!;
        windows.GetCount(out var count);
        for (var i = 0; i < count; i++)
        {
            if (windows.Item(Variant.Int32(i), out var dispatch) != 0 || dispatch == 0)
            {
                continue;
            }

            var provider = ComInterfaceMarshaller<Kumquat.Interaction.Shell.IServiceProvider>.ConvertToManaged((void*)dispatch)!;
            if (provider.QueryService(TopLevelBrowser, typeof(IShellBrowser).GUID, out var raw) != 0)
            {
                continue;
            }

            var browser = ComInterfaceMarshaller<IShellBrowser>.ConvertToManaged((void*)raw)!;
            if (browser.GetWindow(out var hwnd) != 0 || GetAncestor(hwnd, 2) != window || browser.QueryActiveShellView(out var view) != 0)
            {
                continue;
            }

            var pidl = ILCreateFromPathW(file);
            try
            {
                // SVSI_SELECT | SVSI_DESELECTOTHERS | SVSI_ENSUREVISIBLE | SVSI_FOCUSED
                return pidl != 0 && view.SelectItem(ILFindLastID(pidl), 0x1 | 0x4 | 0x8 | 0x10) == 0;
            }
            finally
            {
                ILFree(pidl);
            }
        }

        return false;
    }

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint ILCreateFromPathW(string path);

    [LibraryImport("shell32.dll")]
    private static partial nint ILFindLastID(nint pidl);

    [LibraryImport("shell32.dll")]
    private static partial void ILFree(nint pidl);

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(in Guid clsid, nint outer, uint context, in Guid iid, out nint instance);

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint FindWindowExW(nint parent, nint after, string? className, string? windowName);

    [LibraryImport("user32.dll")]
    private static partial nint GetAncestor(nint hwnd, uint flags);
}

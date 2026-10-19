// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Kumquat.Interaction.DragDrop;

namespace Kumquat.Interaction.Shell;

/// <summary>
/// Archivos seleccionados en el Explorador que está en primer plano (la pestaña activa en
/// Windows 11) o en el escritorio. Usa las interfaces documentadas del shell, por COM:
/// IShellWindows → IShellBrowser de la pestaña → IShellView → selección como IDataObject.
/// Debe llamarse desde un hilo STA.
/// </summary>
internal static unsafe partial class ExplorerSelection
{
    private const uint ClsctxAll = 0x17;
    private const uint SvgioSelection = 1;
    private const int SwcDesktop = 8;
    private const int SwfoNeedDispatch = 1;
    private const int CsidlDesktop = 0;

    private static readonly Guid ShellWindowsClsid = new("9BA05972-F6A8-11CF-A442-00A0C90A8F39");
    private static readonly Guid TopLevelBrowserService = new("4C96BE40-915C-11CF-99D3-00AA004AE837");
    private static readonly Guid DataObjectIid = new("0000010E-0000-0000-C000-000000000046");

    /// <summary>Qué ventana estaba en primer plano.</summary>
    public enum Source
    {
        /// <summary>Ni el Explorador ni el escritorio.</summary>
        Other,

        /// <summary>Una ventana del Explorador.</summary>
        Explorer,

        /// <summary>El escritorio.</summary>
        Desktop,
    }

    /// <summary>Clasifica la ventana en primer plano.</summary>
    public static Source Foreground(out nint window)
    {
        window = GetForegroundWindow();
        return ClassOf(window) switch
        {
            "CabinetWClass" or "ExploreWClass" => Source.Explorer,
            "Progman" or "WorkerW" => Source.Desktop,
            _ => Source.Other,
        };
    }

    /// <summary>Rutas seleccionadas en primer plano; vacío si no es el Explorador o no hay selección.</summary>
    public static IReadOnlyList<string> Get()
    {
        var source = Foreground(out var window);
        if (source == Source.Other || CoCreateInstance(ShellWindowsClsid, 0, ClsctxAll, typeof(IShellWindows).GUID, out var pointer) != 0)
        {
            return [];
        }

        try
        {
            var windows = ComInterfaceMarshaller<IShellWindows>.ConvertToManaged((void*)pointer)!;
            var browser = source == Source.Desktop ? DesktopBrowser(windows) : ExplorerBrowser(windows, window);
            return browser is null ? [] : SelectionOf(browser);
        }
        catch (COMException)
        {
            // Explorador ocupado o reiniciándose: sin selección.
            return [];
        }
        finally
        {
            Marshal.Release(pointer);
        }
    }

    private static IShellBrowser? ExplorerBrowser(IShellWindows windows, nint window)
    {
        // En Windows 11 todas las pestañas comparten la ventana; la activa es la primera "ShellTabWindowClass".
        var activeTab = FindWindowExW(window, 0, "ShellTabWindowClass", null);
        if (windows.GetCount(out var count) != 0)
        {
            return null;
        }

        IShellBrowser? sameWindow = null;
        for (var i = 0; i < count; i++)
        {
            if (windows.Item(Variant.Int32(i), out var dispatch) != 0 || dispatch == 0)
            {
                continue;
            }

            var browser = BrowserOf(dispatch);
            if (browser is null || browser.GetWindow(out var hwnd) != 0)
            {
                continue;
            }

            if (activeTab != 0 && hwnd == activeTab)
            {
                return browser;
            }

            if (sameWindow is null && (hwnd == window || GetAncestor(hwnd, GaRoot) == window))
            {
                sameWindow = browser;
            }
        }

        // Sin coincidencia con la pestaña (Windows 10, sin pestañas): la ventana misma.
        return sameWindow;
    }

    private static IShellBrowser? DesktopBrowser(IShellWindows windows)
    {
        var location = Variant.Int32(CsidlDesktop);
        var empty = default(Variant);
        return windows.FindWindowSW(location, empty, SwcDesktop, out _, SwfoNeedDispatch, out var dispatch) == 0 && dispatch != 0
            ? BrowserOf(dispatch)
            : null;
    }

    /// <summary>Del IDispatch de una ventana a su IShellBrowser (libera el IDispatch).</summary>
    private static IShellBrowser? BrowserOf(nint dispatch)
    {
        try
        {
            var provider = ComInterfaceMarshaller<IServiceProvider>.ConvertToManaged((void*)dispatch);
            if (provider is null || provider.QueryService(TopLevelBrowserService, typeof(IShellBrowser).GUID, out var browser) != 0)
            {
                return null;
            }

            try
            {
                return ComInterfaceMarshaller<IShellBrowser>.ConvertToManaged((void*)browser);
            }
            finally
            {
                Marshal.Release(browser);
            }
        }
        catch (InvalidCastException)
        {
            return null; // p. ej. una ventana de Internet Explorer antigua
        }
        finally
        {
            Marshal.Release(dispatch);
        }
    }

    private static IReadOnlyList<string> SelectionOf(IShellBrowser browser)
    {
        if (browser.QueryActiveShellView(out var view) != 0 || view is null
            || view.GetItemObject(SvgioSelection, DataObjectIid, out var data) != 0 || data == 0)
        {
            return [];
        }

        try
        {
            return DataObjectReader.ReadFiles(data);
        }
        finally
        {
            Marshal.Release(data);
        }
    }

    private static string ClassOf(nint window)
    {
        if (window == 0)
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[64];
        fixed (char* chars = buffer)
        {
            var length = GetClassNameW(window, chars, buffer.Length);
            return length > 0 ? new string(chars, 0, length) : string.Empty;
        }
    }

    private const uint GaRoot = 2;

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(in Guid clsid, nint outer, uint context, in Guid iid, out nint instance);

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial int GetClassNameW(nint window, char* name, int capacity);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint FindWindowExW(nint parent, nint childAfter, string? className, string? windowName);

    [LibraryImport("user32.dll")]
    private static partial nint GetAncestor(nint window, uint flags);
}

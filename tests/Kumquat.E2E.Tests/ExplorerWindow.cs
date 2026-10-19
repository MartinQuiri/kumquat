// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Kumquat.E2E.Tests;

/// <summary>Una ventana real del Explorador con un archivo seleccionado, al frente (y se cierra al desechar).</summary>
internal sealed partial class ExplorerWindow : IDisposable
{
    private ExplorerWindow(nint handle) => Handle = handle;

    public nint Handle { get; }

    /// <summary>Abre la carpeta del archivo con el archivo seleccionado y espera la ventana.</summary>
    public static async Task<ExplorerWindow> OpenSelectingAsync(string file, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe")) { ArgumentList = { "/select,", file } };
        using (Process.Start(start))
        {
        }

        var folder = Path.GetFileName(Path.GetDirectoryName(file)!);
        for (var i = 0; i < 40; i++)
        {
            await Task.Delay(250, cancellationToken);
            var window = Find(folder);
            if (window != 0)
            {
                await Task.Delay(500, cancellationToken); // que termine de seleccionar
                var explorer = new ExplorerWindow(window);
                explorer.BringToFront();
                return explorer;
            }
        }

        throw new InvalidOperationException($"No apareció la ventana del Explorador de {folder}.");
    }

    public void BringToFront() => SetForegroundWindow(Handle);

    /// <summary>
    /// Trae la ventana al frente aunque otra tenga el foco (p. ej. la bandeja tras el aviso de Kumquat).
    /// Windows solo deja cambiar el foco a quien produjo la última entrada: se pulsa Alt (sin efecto
    /// sobre la ventana ni su selección) y enseguida se pide el foco. Un clic en la barra de título no
    /// sirve: en Windows 11 es la barra de pestañas y deja la vista sin selección.
    /// </summary>
    public void Activate()
    {
        SyntheticInput.PressChord(0x12); // Alt
        SetForegroundWindow(Handle);
        Thread.Sleep(200);
    }

    /// <summary>
    /// Selecciona el archivo en esta ventana con la API del shell (IShellView.SelectItem), como un
    /// clic. "/select" no basta con imágenes: el Explorador recarga la vista y pierde la selección.
    /// </summary>
    public Task<bool> SelectAsync(string file)
    {
        var completion = new TaskCompletionSource<bool>();
        var window = Handle;
        var thread = new Thread(() =>
        {
            try
            {
                completion.SetResult(ShellDiagnostics.Select(window, file));
            }
#pragma warning disable CA1031 // Se entrega al test.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                completion.SetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    /// <summary>Ventanas del shell vistas en la última lectura de la selección (diagnóstico).</summary>
    public static string LastDiagnostics { get; private set; } = string.Empty;

    /// <summary>Selección del Explorador en primer plano, leída como la lee Kumquat (en un hilo STA).</summary>
    public static Task<IReadOnlyList<string>> SelectionAsync()
    {
        var completion = new TaskCompletionSource<IReadOnlyList<string>>();
        var thread = new Thread(() =>
        {
            try
            {
                var selection = Kumquat.Interaction.Shell.ExplorerSelection.Get();
                LastDiagnostics = ShellDiagnostics.Describe();
                completion.SetResult(selection);
            }
#pragma warning disable CA1031 // Se entrega al test.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                completion.SetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    /// <summary>Clase y título de la ventana en primer plano (diagnóstico).</summary>
    public static string ForegroundDescription()
    {
        var window = GetForegroundWindow();
        var text = new char[256];
        var className = new char[256];
        var length = GetWindowTextW(window, text, text.Length);
        var classLength = GetClassNameW(window, className, className.Length);
        return $"{new string(className, 0, Math.Max(0, classLength))}: {new string(text, 0, Math.Max(0, length))}";
    }

    public void Dispose() => PostMessageW(Handle, 0x0010 /* WM_CLOSE */, 0, 0);

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();



    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int GetClassNameW(nint hwnd, [Out] char[] text, int capacity);

    private static nint Find(string folderName)
    {
        var found = (nint)0;
        var title = new char[512];
        EnumWindows((hwnd, _) =>
        {
            var length = GetWindowTextW(hwnd, title, title.Length);
            if (length > 0 && new string(title, 0, length).Contains(folderName, StringComparison.OrdinalIgnoreCase))
            {
                found = hwnd;
                return false;
            }

            return true;
        }, 0);
        return found;
    }

    private delegate bool EnumWindowsProc(nint hwnd, nint param);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumWindows(EnumWindowsProc callback, nint param);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int GetWindowTextW(nint hwnd, [Out] char[] text, int capacity);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessageW(nint hwnd, uint message, nint wParam, nint lParam);
}

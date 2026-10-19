// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Kumquat.E2E.Tests;

/// <summary>
/// Abre el Explorador con un archivo seleccionado y comprueba que Kumquat lee esa selección
/// (lo que usa el atajo Ctrl+Alt+K). Abre una ventana real: requiere escritorio interactivo.
/// </summary>
[Trait("Category", "E2E")]
public sealed partial class ExplorerSelectionTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("kumquat-sel-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // El Explorador puede tener la carpeta abierta un momento más.
        }
    }

    [Fact]
    public async Task ArchivoSeleccionado_EnElExploradorAlFrente_SeLee()
    {
        Assert.SkipUnless(Environment.UserInteractive && Environment.GetEnvironmentVariable("CI") is null, "Requiere un escritorio interactivo.");
        var file = Path.Combine(_dir, "elegido ñ.txt");
        await File.WriteAllTextAsync(file, "Kumquat", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_dir, "otro.txt"), "no", TestContext.Current.CancellationToken);

        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe")) { ArgumentList = { "/select,", file } };
        using var explorer = Process.Start(start);
        IReadOnlyList<string> selection = [];
        var window = (nint)0;
        for (var i = 0; i < 40 && selection.Count == 0; i++)
        {
            await Task.Delay(250, TestContext.Current.CancellationToken);
            window = window != 0 ? window : FindExplorerFor(Path.GetFileName(_dir));
            if (window != 0)
            {
                SetForegroundWindow(window);
                selection = await OnStaAsync(Kumquat.Interaction.Shell.ExplorerSelection.Get);
            }
        }

        try
        {
            selection.ShouldBe([file]);
        }
        finally
        {
            if (window != 0)
            {
                PostMessageW(window, 0x0010 /* WM_CLOSE */, 0, 0);
            }
        }
    }

    private static nint FindExplorerFor(string folderName)
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

    private static Task<T> OnStaAsync<T>(Func<T> action)
    {
        var completion = new TaskCompletionSource<T>();
        var thread = new Thread(() =>
        {
            try
            {
                completion.SetResult(action());
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

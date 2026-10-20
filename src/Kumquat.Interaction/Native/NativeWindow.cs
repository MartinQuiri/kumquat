// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Kumquat.Interaction.Native;

/// <summary>
/// Ventana Win32 mínima con despacho a métodos de instancia. Cada ventana recibe sus mensajes en
/// el hilo que la creó; el registro de ventanas y de clases es seguro entre hilos (en la app todas
/// viven en el hilo de UI, pero los tests y futuros hilos de UI no deben poder corromperlo).
/// </summary>
internal abstract unsafe class NativeWindow : IDisposable
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<nint, NativeWindow> Windows = new();
    private static readonly HashSet<string> RegisteredClasses = new(StringComparer.Ordinal);
    private static readonly Lock ClassGate = new();

    [ThreadStatic]
    private static NativeWindow? _creating;

    private bool _disposed;

    /// <summary>Handle de la ventana.</summary>
    public nint Handle { get; private set; }

    /// <summary>Crea la ventana Win32.</summary>
    protected void Create(string className, uint style, uint exStyle, nint parent = 0)
    {
        var instance = Win32.GetModuleHandleW(null);
        RegisterClass(className, instance);

        _creating = this;
        try
        {
            Handle = Win32.CreateWindowExW(exStyle, className, "Kumquat", style, 0, 0, 0, 0, parent, 0, instance, 0);
        }
        finally
        {
            _creating = null;
        }

        if (Handle == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        Windows[Handle] = this;
    }

    /// <summary>Procesa un mensaje. Devuelve <c>null</c> para delegar en DefWindowProc.</summary>
    protected virtual nint? WndProc(uint message, nint wParam, nint lParam) => null;

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Destruye la ventana.</summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (Handle != 0)
        {
            Windows.TryRemove(Handle, out _);
            Win32.DestroyWindow(Handle);
            Handle = 0;
        }
    }

    private static void RegisterClass(string className, nint instance)
    {
        lock (ClassGate)
        {
            if (RegisteredClasses.Contains(className))
            {
                return;
            }

            Register(className, instance);
            RegisteredClasses.Add(className);
        }
    }

    private static void Register(string className, nint instance)
    {
        fixed (char* name = className)
        {
            var wc = new WNDCLASSEXW
            {
                CbSize = (uint)sizeof(WNDCLASSEXW),
                LpfnWndProc = &StaticWndProc,
                HInstance = instance,
                LpszClassName = name,
            };
            if (Win32.RegisterClassExW(&wc) == 0)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static nint StaticWndProc(nint hwnd, uint message, nint wParam, nint lParam)
    {
        if (!Windows.TryGetValue(hwnd, out var window) && _creating is not null)
        {
            window = _creating;
            window.Handle = hwnd;
            Windows[hwnd] = window;
        }

        try
        {
            if (window?.WndProc(message, wParam, lParam) is { } handled)
            {
                return handled;
            }
        }
#pragma warning disable CA1031 // Una excepción no puede cruzar el límite nativo: tumbaría el proceso sin log.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            ErrorSink?.Invoke(ex);
        }

        return Win32.DefWindowProcW(hwnd, message, wParam, lParam);
    }

    /// <summary>Recibe excepciones lanzadas en callbacks de ventana (para el log).</summary>
    public static Action<Exception>? ErrorSink { get; set; }
}

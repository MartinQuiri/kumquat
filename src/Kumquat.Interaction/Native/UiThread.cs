// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Collections.Concurrent;
using Kumquat.Interaction.Native;

namespace Kumquat.Interaction;

/// <summary>
/// Hilo de UI: inicializa OLE (STA), bombea mensajes y ejecuta acciones encoladas desde
/// otros hilos (p. ej. eventos del JobRunner).
/// </summary>
public sealed unsafe class UiThread : IDisposable
{
    private const uint WmInvoke = Win32.WM_APP + 100;

    private readonly ConcurrentQueue<Action> _queue = new();
    private readonly MessageWindow _window;
    private readonly uint _threadId;

    /// <summary>Debe crearse en el hilo principal marcado <c>[STAThread]</c>.</summary>
    public UiThread()
    {
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            throw new InvalidOperationException("UiThread requiere un hilo STA ([STAThread] en Main).");
        }

        var hr = Win32.OleInitialize(0);
        if (hr < 0)
        {
            throw new InvalidOperationException($"OleInitialize falló: 0x{hr:X8}");
        }

        _threadId = Win32.GetCurrentThreadId();
        _window = new MessageWindow(this);
    }

    /// <summary>Recibe excepciones de acciones encoladas y callbacks nativos.</summary>
    public static Action<Exception>? ErrorSink
    {
        get => NativeWindow.ErrorSink;
        set => NativeWindow.ErrorSink = value;
    }

    /// <summary>Encola una acción para el hilo de UI. Seguro desde cualquier hilo.</summary>
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _queue.Enqueue(action);
        Win32.PostMessageW(_window.Handle, WmInvoke, 0, 0);
    }

    /// <summary>Bombea mensajes hasta <see cref="Quit"/>.</summary>
    public static void Run()
    {
        MSG msg;
        while (Win32.GetMessageW(&msg, 0, 0, 0) > 0)
        {
            Win32.TranslateMessage(&msg);
            Win32.DispatchMessageW(&msg);
        }
    }

    /// <summary>Termina el bucle de mensajes.</summary>
    public void Quit() => Post(() => Win32.PostQuitMessage(0));

    /// <inheritdoc />
    public void Dispose()
    {
        _window.Dispose();
        if (Win32.GetCurrentThreadId() == _threadId)
        {
            Win32.OleUninitialize();
        }
    }

    private void Drain()
    {
        while (_queue.TryDequeue(out var action))
        {
            try
            {
                action();
            }
#pragma warning disable CA1031 // Una acción de UI que falla no debe tumbar el bucle de mensajes.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                ErrorSink?.Invoke(ex);
            }
        }
    }

    private sealed class MessageWindow : NativeWindow
    {
        private readonly UiThread _owner;

        public MessageWindow(UiThread owner)
        {
            _owner = owner;
            Create("KumquatDispatcher", 0, 0, Win32.HWND_MESSAGE);
        }

        protected override nint? WndProc(uint message, nint wParam, nint lParam)
        {
            if (message == WmInvoke)
            {
                _owner.Drain();
                return 0;
            }

            return null;
        }
    }
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Kumquat.Interaction.Native;

namespace Kumquat.Interaction.Input;

/// <summary>
/// Hooks de bajo nivel de mouse y teclado en un hilo propio con su bucle de mensajes.
/// Reglas:
/// <list type="bullet">
/// <item>El callback solo actualiza el tracker y publica un mensaje: Windows elimina en
/// silencio un hook que tarda más que <c>LowLevelHooksTimeout</c> (máx. 1 s).</item>
/// <item>Nunca se traga un evento: siempre se llama a <c>CallNextHookEx</c>.</item>
/// <item>Nunca registra pulsaciones: solo mira las teclas del gesto y Escape.</item>
/// <item>Si Windows igual los quita, <see cref="HookWatchdog"/> lo nota y se reinstalan.</item>
/// </list>
/// </summary>
internal sealed unsafe class GlobalInputHook : IDisposable
{
    private static GlobalInputHook? _current;

    private readonly DragGestureTracker _tracker;
    private readonly Action<GestureSignal, POINT> _post;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new();
    private Exception? _startupError;
    private uint _threadId;
    private nint _mouseHook;
    private nint _keyboardHook;
    private uint _lastHookEvent;
    private uint _lastReinstall;

    /// <param name="tracker">Máquina de estados del gesto.</param>
    /// <param name="post">
    /// Se invoca en el hilo del hook con cada señal; debe limitarse a <c>PostMessage</c>.
    /// </param>
    public GlobalInputHook(DragGestureTracker tracker, Action<GestureSignal, POINT> post)
    {
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        _post = post ?? throw new ArgumentNullException(nameof(post));
        _thread = new Thread(Run) { IsBackground = true, Name = "Kumquat input hooks", Priority = ThreadPriority.AboveNormal };
    }

    /// <summary>Tick del último evento recibido (pruebas).</summary>
    internal uint LastHookEventForTests => Volatile.Read(ref _lastHookEvent);

    /// <summary>Diagnóstico (se escribe en el registro de Kumquat).</summary>
    public Action<string>? Trace { get; set; }

    public void Start()
    {
        if (Interlocked.CompareExchange(ref _current, this, null) is not null)
        {
            throw new InvalidOperationException("Solo puede haber un GlobalInputHook activo.");
        }

        _thread.Start();
        _ready.Wait();
        if (_startupError is not null)
        {
            _current = null;
            throw new InvalidOperationException("No se pudieron instalar los hooks de entrada.", _startupError);
        }
    }

    public void Dispose()
    {
        if (_threadId != 0)
        {
            Win32.PostThreadMessageW(_threadId, Win32.WM_CLOSE, 0, 0);
            _thread.Join(TimeSpan.FromSeconds(2));
        }

        Interlocked.CompareExchange(ref _current, null, this);
        _ready.Dispose();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static nint MouseProc(int code, nint wParam, nint lParam)
    {
        var self = _current;
        if (code >= 0 && self is not null)
        {
            Volatile.Write(ref self._lastHookEvent, (uint)Environment.TickCount);
            var data = (MSLLHOOKSTRUCT*)lParam;
            var signal = (uint)wParam switch
            {
                Win32.WM_LBUTTONDOWN => self._tracker.LeftDown(data->Pt.X, data->Pt.Y),
                Win32.WM_MOUSEMOVE => self._tracker.Move(data->Pt.X, data->Pt.Y),
                Win32.WM_LBUTTONUP => self._tracker.LeftUp(),
                Win32.WM_RBUTTONDOWN => self._tracker.RightDown(),
                _ => GestureSignal.None,
            };
            if (signal != GestureSignal.None)
            {
                self._post(signal, data->Pt);
            }
        }

        return Win32.CallNextHookEx(0, code, wParam, lParam);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static nint KeyboardProc(int code, nint wParam, nint lParam)
    {
        var self = _current;
        if (code >= 0 && self is not null)
        {
            Volatile.Write(ref self._lastHookEvent, (uint)Environment.TickCount);
            var vk = (int)((KBDLLHOOKSTRUCT*)lParam)->VkCode;
            var signal = (uint)wParam switch
            {
                Win32.WM_KEYDOWN or Win32.WM_SYSKEYDOWN => self._tracker.KeyDown(vk),
                Win32.WM_KEYUP or Win32.WM_SYSKEYUP => self._tracker.KeyUp(vk),
                _ => GestureSignal.None,
            };
            if (signal != GestureSignal.None)
            {
                Win32.GetCursorPos(out var cursor);
                self._post(signal, cursor);
            }
        }

        return Win32.CallNextHookEx(0, code, wParam, lParam);
    }

    private void Run()
    {
        try
        {
            _threadId = Win32.GetCurrentThreadId();
            Install();
        }
#pragma warning disable CA1031 // Se re-lanza en el hilo que llamó a Start.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _startupError = ex;
            Unhook();
            _ready.Set();
            return;
        }

        _ready.Set();

        // Vigilante: un timer del hilo (sin ventana) revisa cada segundo que los hooks sigan vivos.
        _ = Win32.SetTimer(0, 0, 1000, 0);
        MSG msg;
        while (Win32.GetMessageW(&msg, 0, 0, 0) > 0)
        {
            if (msg.Message == Win32.WM_CLOSE && msg.Hwnd == 0)
            {
                break;
            }

            if (msg.Message == WmTestUnhook && msg.Hwnd == 0)
            {
                Unhook();
                continue;
            }

            if (msg.Message == Win32.WM_TIMER && msg.Hwnd == 0)
            {
                CheckAlive();
                continue;
            }

            Win32.TranslateMessage(&msg);
            Win32.DispatchMessageW(&msg);
        }

        Unhook();
    }

    /// <summary>Quita los hooks como lo haría Windows, sin avisar (pruebas del vigilante).</summary>
    internal void SimulateSystemRemovalForTests() => Win32.PostThreadMessageW(_threadId, WmTestUnhook, 0, 0);

    private const uint WmTestUnhook = Win32.WM_APP + 77;

    private void Install()
    {
        var module = Win32.GetModuleHandleW(null);
        _mouseHook = Win32.SetWindowsHookExW(Win32.WH_MOUSE_LL, &MouseProc, module, 0);
        if (_mouseHook == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        _keyboardHook = Win32.SetWindowsHookExW(Win32.WH_KEYBOARD_LL, &KeyboardProc, module, 0);
        if (_keyboardHook == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        var now = (uint)Environment.TickCount;
        Volatile.Write(ref _lastHookEvent, now);
        _lastReinstall = now;
    }

    private void CheckAlive()
    {
        var info = new LASTINPUTINFO { CbSize = (uint)sizeof(LASTINPUTINFO) };
        if (!Win32.GetLastInputInfo(&info)
            || !HookWatchdog.ShouldReinstall(info.DwTime, Volatile.Read(ref _lastHookEvent), _lastReinstall, (uint)Environment.TickCount))
        {
            return;
        }

        Unhook();
        try
        {
            Install();
            Trace?.Invoke("Windows había quitado los hooks de entrada (equipo muy cargado): reinstalados.");
        }
        catch (Win32Exception ex)
        {
            _lastReinstall = (uint)Environment.TickCount; // se reintenta en el próximo intervalo
            Trace?.Invoke($"No se pudieron reinstalar los hooks de entrada: {ex.Message}");
        }
    }

    private void Unhook()
    {
        if (_mouseHook != 0)
        {
            Win32.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = 0;
        }

        if (_keyboardHook != 0)
        {
            Win32.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = 0;
        }
    }
}

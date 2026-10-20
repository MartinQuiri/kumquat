// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Kumquat.Interaction.DragDrop;
using Kumquat.Interaction.Input;
using Kumquat.Interaction.Native;
using Kumquat.Interaction.Shell;

namespace Kumquat.Interaction.Wheel;

/// <summary>
/// Conecta la rueda con Windows: hooks globales, destino OLE, atajo de teclado, pedidos de otros
/// procesos y la ventana en capas. La lógica vive en <see cref="WheelSession"/>; esta clase solo
/// traduce mensajes de Win32. Vive en el hilo de UI.
/// </summary>
public sealed unsafe class WheelController : IDisposable, IDropHandler
{
    private const uint WmGesture = Win32.WM_APP + 1;
    private const int HotkeyId = 1;

    private readonly DragGestureTracker _tracker;
    private readonly WheelWindow _window;
    private readonly WheelSession _session;
    private readonly DropTarget _dropTarget;
    private readonly GlobalInputHook _hook;
    private bool _registered;
    private bool _hotkeyRegistered;

    /// <summary>Crea el controlador. Debe llamarse en el hilo de UI tras crear <see cref="UiThread"/>.</summary>
    /// <param name="ringsForFiles">Anillos para los archivos arrastrados (vacío si no se puede hacer nada).</param>
    /// <param name="onChosen">Se invoca al soltar (o hacer clic) sobre un sector.</param>
    /// <param name="trigger">Gesto que abre la rueda.</param>
    public WheelController(Func<IReadOnlyList<string>, IReadOnlyList<WheelRing>> ringsForFiles, Action<IReadOnlyList<string>, WheelItem> onChosen, TriggerKey trigger)
    {
        ArgumentNullException.ThrowIfNull(ringsForFiles);
        ArgumentNullException.ThrowIfNull(onChosen);
        _tracker = new DragGestureTracker(trigger, Win32.GetSystemMetrics(Win32.SM_CXDRAG), Win32.GetSystemMetrics(Win32.SM_CYDRAG));
        _window = new WheelWindow { AppMessageHandler = OnAppMessage };
        _session = new WheelSession(_window, _tracker, ringsForFiles, onChosen)
        {
            Selection = ExplorerSelection.Get,
            ExplorerInFront = () => ExplorerSelection.Foreground(out _) != ExplorerSelection.Source.Other,
            Cursor = () => Win32.GetCursorPos(out var cursor) ? cursor : default,
        };
        _window.TimerHandler = _session.OnTimer;
        _window.HotkeyHandler = id =>
        {
            if (id == HotkeyId)
            {
                _session.OnHotkey();
            }
        };
        _window.PathRequestHandler = _session.OnPathRequested;
        _dropTarget = new DropTarget(this);
        _hook = new GlobalInputHook(_tracker, PostGesture) { Trace = message => Trace?.Invoke(message) };
    }

    /// <summary>Texto bajo la rueda para estos archivos (formato y tamaño), o <c>null</c> para no mostrarlo.</summary>
    public Func<IReadOnlyList<string>, string?>? CaptionForFiles
    {
        get => _session.CaptionForFiles;
        set => _session.CaptionForFiles = value;
    }

    /// <summary>Recibe una línea por cada paso de la interacción (diagnóstico; nunca incluye teclas).</summary>
    public Action<string>? Trace
    {
        get => _session.Trace;
        set => _session.Trace = value;
    }

    /// <summary>
    /// Se invoca si el atajo se presiona sin archivos seleccionados en el Explorador (para
    /// avisar al usuario). Recibe <c>true</c> si el Explorador estaba en primer plano.
    /// </summary>
    public Action<bool>? NothingSelected
    {
        get => _session.NothingSelected;
        set => _session.NothingSelected = value;
    }

    /// <summary>Buscador: lo abren el atajo y el menú contextual (la rueda queda para arrastrar).</summary>
    public Action<IReadOnlyList<string>>? KeyboardPicker
    {
        get => _session.KeyboardPicker;
        set => _session.KeyboardPicker = value;
    }

    /// <summary>Gesto configurado.</summary>
    public TriggerKey Trigger
    {
        get => _tracker.Trigger;
        set => _tracker.Trigger = value;
    }

    /// <summary>Indica si el atajo global está registrado.</summary>
    public bool HasHotkey => _hotkeyRegistered;

    /// <summary>Activa (o desactiva, con <c>null</c>) el atajo global. <c>false</c> si otra app ya lo usa.</summary>
    public bool SetHotkey(Hotkey? hotkey)
    {
        if (_hotkeyRegistered)
        {
            Win32.UnregisterHotKey(_window.Handle, HotkeyId);
            _hotkeyRegistered = false;
        }

        if (hotkey is null)
        {
            return true;
        }

        _hotkeyRegistered = Win32.RegisterHotKey(_window.Handle, HotkeyId, hotkey.Modifiers | Win32.MOD_NOREPEAT, hotkey.VirtualKey);
        Trace?.Invoke(_hotkeyRegistered ? $"Atajo {hotkey.Display} activo." : $"El atajo {hotkey.Display} ya lo usa otra app.");
        return _hotkeyRegistered;
    }

    /// <summary>Abre la rueda en modo clic con estos archivos (Kumquat se abrió desde el menú contextual).</summary>
    public void OpenForFiles(IReadOnlyList<string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        foreach (var file in files)
        {
            _session.OnPathRequested(file);
        }
    }

    /// <summary>Registra el destino OLE e instala los hooks.</summary>
    public void Start()
    {
        var pointer = ComInterfaceMarshaller<IDropTarget>.ConvertToUnmanaged(_dropTarget);
        try
        {
            var hr = Win32.RegisterDragDrop(_window.Handle, (nint)pointer);
            Marshal.ThrowExceptionForHR(hr);

            _registered = true;
        }
        finally
        {
            // RegisterDragDrop toma su propia referencia.
            ComInterfaceMarshaller<IDropTarget>.Free(pointer);
        }

        _hook.Start();
        Trace?.Invoke($"Listo: ventana 0x{_window.Handle:X}, destino OLE registrado, hooks instalados, gesto {Trigger}.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        SetHotkey(null);
        _hook.Dispose();
        if (_registered)
        {
            _ = Win32.RevokeDragDrop(_window.Handle);
            _registered = false;
        }

        _window.Dispose();
    }

    bool IDropHandler.OnDragEnter(IReadOnlyList<string> files, POINT screenPoint) => _session.OnDragEnter(files, screenPoint);

    bool IDropHandler.OnDragOver(POINT screenPoint) => _session.OnDragOver(screenPoint);

    void IDropHandler.OnDragLeave() => _session.OnDragLeave();

    void IDropHandler.OnDrop(IReadOnlyList<string> files, POINT screenPoint) => _session.OnDrop(files, screenPoint);

    /// <summary>En el hilo del hook: solo publica un mensaje (el hook no puede demorarse).</summary>
    private void PostGesture(GestureSignal signal, POINT point)
        => Win32.PostMessageW(_window.Handle, WmGesture + (uint)signal, point.X, point.Y);

    private bool OnAppMessage(uint message, nint wParam, nint lParam)
    {
        if (message < WmGesture || message > WmGesture + (uint)GestureSignal.PointerMoved)
        {
            return false;
        }

        _session.OnSignal((GestureSignal)(message - WmGesture), new POINT((int)wParam, (int)lParam));
        return true;
    }
}

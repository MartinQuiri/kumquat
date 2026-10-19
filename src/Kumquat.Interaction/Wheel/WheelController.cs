// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Kumquat.Interaction.DragDrop;
using Kumquat.Interaction.Input;
using Kumquat.Interaction.Native;

namespace Kumquat.Interaction.Wheel;

/// <summary>
/// Orquesta la interacción: hooks → mostrar la rueda bajo el cursor → OLE DragEnter/Over/Drop
/// → callback con los archivos y el destino elegido. Vive en el hilo de UI.
/// </summary>
public sealed unsafe class WheelController : IDisposable, IDropHandler
{
    private const uint WmGesture = Win32.WM_APP + 1;

    private readonly Func<IReadOnlyList<string>, IReadOnlyList<WheelItem>> _itemsForFiles;
    private readonly Action<IReadOnlyList<string>, WheelItem> _onChosen;
    private readonly DragGestureTracker _tracker;
    private readonly WheelWindow _window;
    private readonly DropTarget _dropTarget;
    private readonly GlobalInputHook _hook;
    private WheelVisualState _state = WheelVisualState.Waiting;
    private bool _registered;

    /// <summary>Crea el controlador. Debe llamarse en el hilo de UI tras crear <see cref="UiThread"/>.</summary>
    /// <param name="itemsForFiles">Destinos para los archivos arrastrados (vacío si no hay conversión).</param>
    /// <param name="onChosen">Se invoca al soltar sobre un sector.</param>
    /// <param name="trigger">Gesto que abre la rueda.</param>
    public WheelController(Func<IReadOnlyList<string>, IReadOnlyList<WheelItem>> itemsForFiles, Action<IReadOnlyList<string>, WheelItem> onChosen, TriggerKey trigger)
    {
        _itemsForFiles = itemsForFiles ?? throw new ArgumentNullException(nameof(itemsForFiles));
        _onChosen = onChosen ?? throw new ArgumentNullException(nameof(onChosen));
        _tracker = new DragGestureTracker(trigger, Win32.GetSystemMetrics(Win32.SM_CXDRAG), Win32.GetSystemMetrics(Win32.SM_CYDRAG));
        _window = new WheelWindow { AppMessageHandler = OnAppMessage };
        _dropTarget = new DropTarget(this);
        _hook = new GlobalInputHook(_tracker, PostGesture);
    }

    /// <summary>Gesto configurado.</summary>
    public TriggerKey Trigger
    {
        get => _tracker.Trigger;
        set => _tracker.Trigger = value;
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
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _hook.Dispose();
        if (_registered)
        {
            _ = Win32.RevokeDragDrop(_window.Handle);
            _registered = false;
        }

        _window.Dispose();
    }

    bool IDropHandler.OnDragEnter(IReadOnlyList<string> files, POINT screenPoint)
    {
        if (!_window.IsShown)
        {
            return false; // Arrastre que pasó por la zona de estacionamiento: ignorar.
        }

        var items = _itemsForFiles(files);
        _state = items.Count == 0
            ? new WheelVisualState([], -1, "Sin conversiones para este archivo")
            : new WheelVisualState(items, -1, null);
        _window.Render(_state);
        return items.Count > 0 && UpdateHighlight(screenPoint);
    }

    bool IDropHandler.OnDragOver(POINT screenPoint) => _window.IsShown && UpdateHighlight(screenPoint);

    void IDropHandler.OnDragLeave()
    {
        // La rueda se queda (como en Tangerine) hasta soltar o cancelar; solo se quita el resaltado.
        if (_state.Highlighted >= 0)
        {
            _state = _state with { Highlighted = -1 };
            _window.Render(_state);
        }
    }

    void IDropHandler.OnDrop(IReadOnlyList<string> files, POINT screenPoint)
    {
        var hit = HitTest(screenPoint);
        var item = hit.Zone == WheelZone.Sector ? _state.Items[hit.SectorIndex] : null;
        Close();
        if (item is not null)
        {
            _onChosen(files, item);
        }
    }

    private void PostGesture(GestureSignal signal, POINT point)
        => Win32.PostMessageW(_window.Handle, WmGesture + (uint)signal, point.X, point.Y);

    private bool OnAppMessage(uint message, nint wParam, nint lParam)
    {
        if (message < WmGesture || message > WmGesture + (uint)GestureSignal.Cancel)
        {
            return false;
        }

        switch ((GestureSignal)(message - WmGesture))
        {
            case GestureSignal.Trigger when !_window.IsShown:
                _state = WheelVisualState.Waiting;
                _window.ShowAt(new POINT((int)wParam, (int)lParam), _state);
                _tracker.WheelOpen = true;
                break;
            case GestureSignal.DragEnded or GestureSignal.Cancel when _window.IsShown:
                Close();
                break;
        }

        return true;
    }

    private bool UpdateHighlight(POINT screenPoint)
    {
        var hit = HitTest(screenPoint);
        var index = hit.Zone == WheelZone.Sector ? hit.SectorIndex : -1;
        if (index != _state.Highlighted)
        {
            _state = _state with { Highlighted = index };
            _window.Render(_state);
        }

        return index >= 0;
    }

    private WheelHit HitTest(POINT screenPoint)
    {
        var center = _window.Center;
        return _window.LayoutFor(_state.Items.Count).HitTest(screenPoint.X - center.X, screenPoint.Y - center.Y);
    }

    private void Close()
    {
        _tracker.WheelOpen = false;
        _state = WheelVisualState.Waiting;
        _window.Park();
    }
}

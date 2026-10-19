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
/// Orquesta la interacción: hooks → mostrar la rueda bajo el cursor → OLE DragEnter/Over/Drop
/// → callback con los archivos y el destino elegido. Vive en el hilo de UI.
/// </summary>
public sealed unsafe class WheelController : IDisposable, IDropHandler
{
    private const uint WmGesture = Win32.WM_APP + 1;

    /// <summary>
    /// El hook global ve el botón soltado ANTES de que el bucle OLE de la fuente entregue el drop.
    /// Si la rueda se ocultara en ese instante, el drop caería en lo que esté debajo. Se espera
    /// este margen; si el drop llega, cierra antes.
    /// </summary>
    private const uint DropGraceMilliseconds = 600;
    private const nuint CloseTimerId = 1;
    private const int HotkeyId = 1;
    private const nuint RequestTimerId = 2;

    /// <summary>
    /// El Explorador lanza un proceso por archivo seleccionado: se juntan los pedidos que llegan en
    /// este margen y la rueda se abre una sola vez con todos.
    /// </summary>
    private const uint RequestGatherMilliseconds = 250;

    private readonly Func<IReadOnlyList<string>, IReadOnlyList<WheelRing>> _ringsForFiles;
    private readonly Action<IReadOnlyList<string>, WheelItem> _onChosen;
    private readonly DragGestureTracker _tracker;
    private readonly WheelWindow _window;
    private readonly DropTarget _dropTarget;
    private readonly GlobalInputHook _hook;
    private WheelVisualState _state = WheelVisualState.Waiting;
    private bool _registered;
    private bool _hotkeyRegistered;
    private IReadOnlyList<string>? _clickFiles;
    private readonly List<string> _requested = [];

    /// <summary>Crea el controlador. Debe llamarse en el hilo de UI tras crear <see cref="UiThread"/>.</summary>
    /// <param name="ringsForFiles">Anillos para los archivos arrastrados (vacío si no se puede hacer nada).</param>
    /// <param name="onChosen">Se invoca al soltar sobre un sector.</param>
    /// <param name="trigger">Gesto que abre la rueda.</param>
    public WheelController(Func<IReadOnlyList<string>, IReadOnlyList<WheelRing>> ringsForFiles, Action<IReadOnlyList<string>, WheelItem> onChosen, TriggerKey trigger)
    {
        _ringsForFiles = ringsForFiles ?? throw new ArgumentNullException(nameof(ringsForFiles));
        _onChosen = onChosen ?? throw new ArgumentNullException(nameof(onChosen));
        _tracker = new DragGestureTracker(trigger, Win32.GetSystemMetrics(Win32.SM_CXDRAG), Win32.GetSystemMetrics(Win32.SM_CYDRAG));
        _window = new WheelWindow { AppMessageHandler = OnAppMessage, TimerHandler = OnTimer, HotkeyHandler = OnHotkey, PathRequestHandler = OnPathRequested };
        _dropTarget = new DropTarget(this);
        _hook = new GlobalInputHook(_tracker, PostGesture);
    }

    /// <summary>Texto bajo la rueda para estos archivos (formato y tamaño), o <c>null</c> para no mostrarlo.</summary>
    public Func<IReadOnlyList<string>, string?>? CaptionForFiles { get; set; }

    /// <summary>Recibe una línea por cada paso de la interacción (diagnóstico; nunca incluye teclas).</summary>
    public Action<string>? Trace { get; set; }

    /// <summary>Gesto configurado.</summary>
    public TriggerKey Trigger
    {
        get => _tracker.Trigger;
        set => _tracker.Trigger = value;
    }

    /// <summary>
    /// Se invoca si el atajo se presiona sin archivos seleccionados en el Explorador (para
    /// avisar al usuario). Recibe <c>true</c> si el Explorador estaba en primer plano.
    /// </summary>
    public Action<bool>? NothingSelected { get; set; }

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

    bool IDropHandler.OnDragEnter(IReadOnlyList<string> files, POINT screenPoint)
    {
        if (!_window.IsShown)
        {
            Trace?.Invoke("DragEnter con la rueda estacionada: ignorado.");
            return false; // Arrastre que pasó por la zona de estacionamiento: ignorar.
        }

        var rings = _ringsForFiles(files).Where(r => r.Items.Count > 0).ToList();
        Trace?.Invoke($"DragEnter: {files.Count} archivo(s) ({Path.GetExtension(files[0])}), anillos: {string.Join(", ", rings.Select(r => $"{r.Kind}={r.Items.Count}"))}.");
        _state = rings.Count == 0
            ? new WheelVisualState([], WheelHit.Center, "Nada que hacer con este archivo")
            : new WheelVisualState(rings, WheelHit.Center, null);
        _state = _state with { Caption = CaptionFor(files) };
        _window.Render(_state);
        return rings.Count > 0 && UpdateHighlight(screenPoint);
    }

    bool IDropHandler.OnDragOver(POINT screenPoint) => _window.IsShown && UpdateHighlight(screenPoint);

    void IDropHandler.OnDragLeave()
    {
        // La rueda se queda (como en Tangerine) hasta soltar o cancelar; solo se quita el resaltado.
        if (_state.HasHighlight)
        {
            _state = _state with { Highlight = WheelHit.Center };
            _window.Render(_state);
        }
    }

    void IDropHandler.OnDrop(IReadOnlyList<string> files, POINT screenPoint)
    {
        var item = _state.ItemAt(HitTest(screenPoint));
        Trace?.Invoke($"Drop -> {(item is null ? "(nada)" : $"{item.Ring}:{item.Id}")}.");
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
        if (message < WmGesture || message > WmGesture + (uint)GestureSignal.PointerMoved)
        {
            return false;
        }

        var signal = (GestureSignal)(message - WmGesture);
        var point = new POINT((int)wParam, (int)lParam);
        if (_clickFiles is not null)
        {
            OnClickModeSignal(signal, point);
            return true;
        }

        Trace?.Invoke($"Señal {signal} en ({(int)wParam}, {(int)lParam}); rueda {(_window.IsShown ? "visible" : "oculta")}.");
        switch (signal)
        {
            case GestureSignal.Trigger when !_window.IsShown:
                _state = WheelVisualState.Waiting;
                _window.ShowAt(new POINT((int)wParam, (int)lParam), _state);
                _tracker.WheelOpen = true;
                Trace?.Invoke($"Rueda mostrada en ({_window.Center.X}, {_window.Center.Y}), escala {_window.Scale:0.##}.");
                break;
            case GestureSignal.DragEnded when _window.IsShown && _state.HasHighlight:
                // Sobre un sector: OLE entregará el drop en unos milisegundos; esperarlo.
                _window.StartTimer(CloseTimerId, DropGraceMilliseconds);
                break;
            case GestureSignal.DragEnded when _window.IsShown:
                // Centro, fuera o sin destinos: OLE nunca llamará a Drop. Cerrar ya, sin demora.
                Close();
                break;
            case GestureSignal.Cancel when _window.IsShown:
                Close();
                break;
        }

        return true;
    }

    /// <summary>
    /// Atajo de teclado: la rueda aparece bajo el cursor con lo seleccionado en el Explorador y
    /// se elige con un clic (no hay arrastre ni OLE).
    /// </summary>
    private void OnHotkey(int id)
    {
        if (id != HotkeyId || _window.IsShown)
        {
            return;
        }

        var files = ExplorerSelection.Get();
        Trace?.Invoke($"Atajo: {files.Count} archivo(s) seleccionado(s).");
        if (files.Count == 0)
        {
            NothingSelected?.Invoke(ExplorerSelection.Foreground(out _) != ExplorerSelection.Source.Other);
            return;
        }

        OpenForClick(files);
    }

    /// <summary>Abre la rueda en modo clic con estos archivos (menú contextual, atajo).</summary>
    public void OpenForFiles(IReadOnlyList<string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        foreach (var file in files)
        {
            OnPathRequested(file);
        }
    }

    private void OnPathRequested(string path)
    {
        if (_window.IsShown || (!File.Exists(path) && !Directory.Exists(path)))
        {
            return;
        }

        _requested.Add(path);
        _window.StartTimer(RequestTimerId, RequestGatherMilliseconds);
    }

    /// <summary>
    /// Fin de la recolección. Si los pedidos son parte de la selección del Explorador se usa la
    /// selección completa (el Explorador lanza como mucho 15 procesos); si no, lo pedido.
    /// </summary>
    private void OpenRequested()
    {
        _window.StopTimer(RequestTimerId);
        var requested = _requested.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        _requested.Clear();
        if (requested.Count == 0 || _window.IsShown)
        {
            return;
        }

        var selection = ExplorerSelection.Get();
        var files = selection.Count >= requested.Count && requested.All(r => selection.Contains(r, StringComparer.OrdinalIgnoreCase)) ? selection : requested;
        Trace?.Invoke($"Menú contextual: {requested.Count} pedido(s), {files.Count} archivo(s).");
        OpenForClick(files);
    }

    private void OpenForClick(IReadOnlyList<string> files)
    {
        var rings = _ringsForFiles(files).Where(r => r.Items.Count > 0).ToList();
        _state = rings.Count == 0
            ? new WheelVisualState([], WheelHit.Center, "Nada que hacer con este archivo")
            : new WheelVisualState(rings, WheelHit.Center, null);
        _state = _state with { Caption = CaptionFor(files) };
        Win32.GetCursorPos(out var cursor);
        _clickFiles = files;
        _tracker.ClickMode = true;
        _tracker.WheelOpen = true;
        _window.ShowAt(cursor, _state);
        UpdateHighlight(cursor);
    }

    private void OnClickModeSignal(GestureSignal signal, POINT point)
    {
        switch (signal)
        {
            case GestureSignal.PointerMoved:
                UpdateHighlight(point);
                break;
            case GestureSignal.DragEnded:
                // Clic: sobre un sector lo aplica; en el centro o fuera, cierra.
                var files = _clickFiles!;
                var item = _state.ItemAt(HitTest(point));
                Trace?.Invoke($"Clic -> {(item is null ? "(nada)" : $"{item.Ring}:{item.Id}")}.");
                Close();
                if (item is not null)
                {
                    _onChosen(files, item);
                }

                break;
            case GestureSignal.Cancel:
                Close();
                break;
        }
    }

    private string? CaptionFor(IReadOnlyList<string> files)
    {
        try
        {
            return CaptionForFiles?.Invoke(files);
        }
        catch (IOException)
        {
            return null; // la etiqueta es una ayuda: nunca debe impedir abrir la rueda
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private bool UpdateHighlight(POINT screenPoint)
    {
        var hit = HitTest(screenPoint);
        var highlight = hit.Zone == WheelZone.Sector ? hit : WheelHit.Center;
        if (highlight != _state.Highlight)
        {
            _state = _state with { Highlight = highlight };
            _window.Render(_state);
        }

        return highlight.Zone == WheelZone.Sector;
    }

    private WheelHit HitTest(POINT screenPoint)
    {
        var center = _window.Center;
        return _window.LayoutFor(_state).HitTest(screenPoint.X - center.X, screenPoint.Y - center.Y);
    }

    private void OnTimer(nuint id)
    {
        if (id == RequestTimerId)
        {
            OpenRequested();
            return;
        }

        if (id == CloseTimerId)
        {
            Trace?.Invoke("Sin drop tras soltar: rueda cerrada.");
            Close();
        }
    }

    private void Close()
    {
        _window.StopTimer(CloseTimerId);
        _tracker.WheelOpen = false;
        _tracker.ClickMode = false;
        _clickFiles = null;
        _state = WheelVisualState.Waiting;
        _window.Park();
    }
}

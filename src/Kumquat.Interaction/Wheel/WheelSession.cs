// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Localization;
using Kumquat.Interaction.Input;
using Kumquat.Interaction.Native;

namespace Kumquat.Interaction.Wheel;

/// <summary>
/// Lógica de la rueda, sin Win32: qué hacer con cada gesto, drop, clic, atajo o pedido del menú
/// contextual. <see cref="WheelController"/> la conecta con los hooks, OLE y la ventana real; los
/// tests la manejan con una superficie falsa.
/// </summary>
internal sealed class WheelSession
{
    /// <summary>Timer de espera del drop tras soltar sobre un sector.</summary>
    public const nuint CloseTimerId = 1;

    /// <summary>Timer de recolección de pedidos del menú contextual.</summary>
    public const nuint RequestTimerId = 2;

    /// <summary>
    /// El hook global ve el botón soltado ANTES de que el bucle OLE de la fuente entregue el drop.
    /// Si la rueda se ocultara en ese instante, el drop caería en lo que esté debajo. Se espera
    /// este margen; si el drop llega, cierra antes.
    /// </summary>
    public const uint DropGraceMilliseconds = 600;

    /// <summary>
    /// El Explorador lanza un proceso por archivo seleccionado: se juntan los pedidos que llegan en
    /// este margen y la rueda se abre una sola vez con todos.
    /// </summary>
    public const uint RequestGatherMilliseconds = 250;

    private readonly IWheelSurface _surface;
    private readonly DragGestureTracker _tracker;
    private readonly Func<IReadOnlyList<string>, IReadOnlyList<WheelRing>> _ringsForFiles;
    private readonly Action<IReadOnlyList<string>, WheelItem> _onChosen;
    private readonly List<string> _requested = [];
    private WheelVisualState _state = WheelVisualState.Waiting;
    private IReadOnlyList<string>? _clickFiles;

    public WheelSession(
        IWheelSurface surface,
        DragGestureTracker tracker,
        Func<IReadOnlyList<string>, IReadOnlyList<WheelRing>> ringsForFiles,
        Action<IReadOnlyList<string>, WheelItem> onChosen)
    {
        _surface = surface;
        _tracker = tracker;
        _ringsForFiles = ringsForFiles;
        _onChosen = onChosen;
    }

    /// <summary>Etiqueta bajo la rueda para estos archivos.</summary>
    public Func<IReadOnlyList<string>, string?>? CaptionForFiles { get; set; }

    /// <summary>Diagnóstico de cada paso (nunca incluye teclas).</summary>
    public Action<string>? Trace { get; set; }

    /// <summary>El atajo se presionó sin selección; <c>true</c> si el Explorador estaba al frente.</summary>
    public Action<bool>? NothingSelected { get; set; }

    /// <summary>Archivos seleccionados en el Explorador en primer plano.</summary>
    public Func<IReadOnlyList<string>> Selection { get; set; } = () => [];

    /// <summary>Indica si el Explorador (o el escritorio) está en primer plano.</summary>
    public Func<bool> ExplorerInFront { get; set; } = () => false;

    /// <summary>Posición del cursor en pantalla.</summary>
    public Func<POINT> Cursor { get; set; } = () => default;

    /// <summary>Indica si una ruta existe (archivo o carpeta).</summary>
    public Func<string, bool> PathExists { get; set; } = path => File.Exists(path) || Directory.Exists(path);

    /// <summary>
    /// Elegir con el teclado (el buscador): si está, el atajo y el menú contextual lo abren con los
    /// archivos en lugar de la rueda, que queda para el arrastre con el mouse.
    /// </summary>
    public Action<IReadOnlyList<string>>? KeyboardPicker { get; set; }

    /// <summary>Lo que se muestra ahora (pruebas y diagnóstico).</summary>
    public WheelVisualState State => _state;

    /// <summary>Indica si la rueda está abierta para elegir con un clic (atajo o menú contextual).</summary>
    public bool InClickMode => _clickFiles is not null;

    /// <summary>Señal de los hooks globales.</summary>
    public void OnSignal(GestureSignal signal, POINT point)
    {
        if (_clickFiles is not null)
        {
            OnClickModeSignal(signal, point);
            return;
        }

        Trace?.Invoke($"Señal {signal} en ({point.X}, {point.Y}); rueda {(_surface.IsShown ? "visible" : "oculta")}.");
        switch (signal)
        {
            case GestureSignal.Trigger when !_surface.IsShown:
                _state = WheelVisualState.Waiting;
                _surface.ShowAt(point, _state);
                _tracker.WheelOpen = true;
                Trace?.Invoke($"Rueda mostrada en ({_surface.Center.X}, {_surface.Center.Y}).");
                break;
            case GestureSignal.DragEnded when _surface.IsShown && _state.HasHighlight:
                // Sobre un sector: OLE entregará el drop en unos milisegundos; esperarlo.
                _surface.StartTimer(CloseTimerId, DropGraceMilliseconds);
                break;
            case GestureSignal.DragEnded when _surface.IsShown:
                // Centro, fuera o sin destinos: OLE nunca llamará a Drop. Cerrar ya, sin demora.
                Close();
                break;
            case GestureSignal.Cancel when _surface.IsShown:
                Close();
                break;
        }
    }

    /// <summary>OLE: el arrastre entró a la rueda. <c>true</c> si acepta el drop en ese punto.</summary>
    public bool OnDragEnter(IReadOnlyList<string> files, POINT point)
    {
        if (!_surface.IsShown)
        {
            Trace?.Invoke("DragEnter con la rueda estacionada: ignorado.");
            return false; // Arrastre que pasó por la zona de estacionamiento: ignorar.
        }

        _state = StateFor(files);
        Trace?.Invoke($"DragEnter: {files.Count} archivo(s) ({Path.GetExtension(files[0])}), anillos: {string.Join(", ", _state.Rings.Select(r => $"{r.Kind}={r.Items.Count}"))}.");
        _surface.Render(_state);
        AnnounceOpened();
        return _state.Rings.Count > 0 && UpdateHighlight(point);
    }

    /// <summary>OLE: el arrastre se mueve sobre la rueda.</summary>
    public bool OnDragOver(POINT point) => _surface.IsShown && UpdateHighlight(point);

    /// <summary>OLE: el arrastre salió. La rueda se queda (como en Tangerine); solo se quita el resaltado.</summary>
    public void OnDragLeave()
    {
        if (_state.HasHighlight)
        {
            _state = _state with { Highlight = WheelHit.Center };
            _surface.Render(_state);
        }
    }

    /// <summary>OLE: se soltó sobre la rueda.</summary>
    public void OnDrop(IReadOnlyList<string> files, POINT point)
    {
        var item = files.Count > 0 ? _state.ItemAt(HitTest(point)) : null;
        Trace?.Invoke($"Drop -> {Describe(item)}.");
        Close();
        if (item is not null)
        {
            _onChosen(files, item);
        }
    }

    /// <summary>Atajo de teclado: rueda bajo el cursor con lo seleccionado, para elegir con un clic.</summary>
    public void OnHotkey()
    {
        if (_surface.IsShown)
        {
            return;
        }

        var files = Selection();
        Trace?.Invoke($"Atajo: {files.Count} archivo(s) seleccionado(s).");
        if (files.Count == 0)
        {
            NothingSelected?.Invoke(ExplorerInFront());
            return;
        }

        OpenForClick(files);
    }

    /// <summary>Pedido de otro proceso (menú contextual) o del arranque: se junta con los que lleguen enseguida.</summary>
    public void OnPathRequested(string path)
    {
        if (_surface.IsShown || !PathExists(path))
        {
            return;
        }

        _requested.Add(path);
        _surface.StartTimer(RequestTimerId, RequestGatherMilliseconds);
    }

    /// <summary>Venció un timer de la superficie.</summary>
    public void OnTimer(nuint id)
    {
        if (id == RequestTimerId)
        {
            OpenRequested();
        }
        else if (id == CloseTimerId)
        {
            Trace?.Invoke("Sin drop tras soltar: rueda cerrada.");
            Close();
        }
    }

    private static string Describe(WheelItem? item) => item is null ? "(nada)" : $"{item.Ring}:{item.Id}";

    /// <summary>
    /// Fin de la recolección. Si los pedidos son parte de la selección del Explorador se usa la
    /// selección completa (el Explorador lanza como mucho 15 procesos); si no, lo pedido.
    /// </summary>
    private void OpenRequested()
    {
        _surface.StopTimer(RequestTimerId);
        var requested = _requested.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        _requested.Clear();
        if (requested.Count == 0 || _surface.IsShown)
        {
            return;
        }

        var selection = Selection();
        var files = selection.Count >= requested.Count && requested.All(r => selection.Contains(r, StringComparer.OrdinalIgnoreCase)) ? selection : requested;
        Trace?.Invoke($"Menú contextual: {requested.Count} pedido(s), {files.Count} archivo(s).");
        OpenForClick(files);
    }

    private void OpenForClick(IReadOnlyList<string> files)
    {
        if (KeyboardPicker is { } picker)
        {
            picker(files);
            return;
        }

        _state = StateFor(files);
        var cursor = Cursor();
        _clickFiles = files;
        _tracker.ClickMode = true;
        _tracker.WheelOpen = true;
        _surface.ShowAt(cursor, _state);
        AnnounceOpened();
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
                Trace?.Invoke($"Clic -> {Describe(item)}.");
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

    private WheelVisualState StateFor(IReadOnlyList<string> files)
    {
        var rings = _ringsForFiles(files).Where(r => r.Items.Count > 0).ToList();
        var state = rings.Count == 0
            ? new WheelVisualState([], WheelHit.Center, Strings.WheelNothingToDo)
            : new WheelVisualState(rings, WheelHit.Center, null);
        return state with { Caption = CaptionFor(files) };
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

    private bool UpdateHighlight(POINT point)
    {
        var hit = HitTest(point);
        var highlight = hit.Zone == WheelZone.Sector ? hit : WheelHit.Center;
        if (highlight != _state.Highlight)
        {
            _state = _state with { Highlight = highlight };
            _surface.Render(_state);
            _surface.Announce(_state.ItemAt(highlight) is { } item ? Spoken(item) : Strings.WheelCenter);
        }

        return highlight.Zone == WheelZone.Sector;
    }

    /// <summary>Lo que dice el lector al abrir la rueda: qué es lo arrastrado y cuántas opciones hay.</summary>
    private void AnnounceOpened()
    {
        var count = _state.Rings.Sum(r => r.Items.Count);
        _surface.Announce(count == 0 ? _state.Message ?? Strings.WheelNothingToDo
            : _state.Caption is { } caption ? Strings.WheelOpened(caption, count)
            : Strings.WheelOpenedNoCaption(count));
    }

    /// <summary>Un sector dicho en voz alta: "Convertir a JPG", "Comprimir".</summary>
    internal static string Spoken(WheelItem item) => item.Ring == WheelRingKind.Convert ? Strings.PaletteConvertTo(item.Label) : item.Label;

    private WheelHit HitTest(POINT point)
    {
        var center = _surface.Center;
        return _surface.LayoutFor(_state).HitTest(point.X - center.X, point.Y - center.Y);
    }

    private void Close()
    {
        _surface.StopTimer(CloseTimerId);
        _tracker.WheelOpen = false;
        _tracker.ClickMode = false;
        _clickFiles = null;
        _state = WheelVisualState.Waiting;
        _surface.Park();
    }
}

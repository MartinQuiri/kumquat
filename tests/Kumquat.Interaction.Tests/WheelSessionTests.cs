// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Formats;
using Kumquat.Interaction.Input;
using Kumquat.Interaction.Native;
using Kumquat.Interaction.Wheel;

namespace Kumquat.Interaction.Tests;

/// <summary>
/// La lógica de la rueda sin Windows: arrastre + drop, modo clic (atajo y menú contextual),
/// espera del drop y recolección de pedidos. La superficie es falsa pero la geometría es la real.
/// </summary>
public sealed class WheelSessionTests
{
    private static readonly POINT Center = new(500, 500);

    /// <summary>Sector norte del anillo de conversión (mitad del anillo, sobre el centro).</summary>
    private static readonly POINT NorthOfInnerRing = new(500, 500 - (int)(WheelRenderer.CenterRadiusDip + (WheelRenderer.RingThicknessDip / 2)));

    private static readonly string[] Files = [@"C:\fotos\a.png"];

    private readonly FakeSurface _surface = new();
    private readonly DragGestureTracker _tracker = new(TriggerKey.Shift, 4, 4);
    private readonly List<(IReadOnlyList<string> Files, WheelItem Item)> _chosen = [];
    private readonly WheelSession _session;

    public WheelSessionTests()
    {
        _session = new WheelSession(_surface, _tracker, _ => [new WheelRing(WheelRingKind.Convert, [WheelItem.Convert(KnownFormats.Jpg), WheelItem.Convert(KnownFormats.WebP)])], (files, item) => _chosen.Add((files, item)))
        {
            Cursor = () => Center,
            PathExists = _ => true,
            CaptionForFiles = files => $"{files.Count} archivo(s)",
        };
    }

    [Fact]
    public void Arrastre_SoltarSobreUnSector_Elige()
    {
        _session.OnSignal(GestureSignal.Trigger, Center);
        _surface.IsShown.ShouldBeTrue();
        _tracker.WheelOpen.ShouldBeTrue();

        _session.OnDragEnter(Files, Center).ShouldBeFalse("en el centro no se acepta el drop");
        _session.State.Caption.ShouldBe("1 archivo(s)");
        _session.OnDragOver(NorthOfInnerRing).ShouldBeTrue();
        _session.State.Highlight.ShouldBe(WheelHit.At(0, 0));

        // El hook ve soltar antes que OLE: se espera el drop en vez de cerrar.
        _session.OnSignal(GestureSignal.DragEnded, NorthOfInnerRing);
        _surface.Timers.ShouldContainKey(WheelSession.CloseTimerId);
        _surface.IsShown.ShouldBeTrue();

        _session.OnDrop(Files, NorthOfInnerRing);
        _chosen.ShouldHaveSingleItem().Item.Target.ShouldBe(KnownFormats.Jpg);
        _surface.IsShown.ShouldBeFalse();
        _tracker.WheelOpen.ShouldBeFalse();
        _surface.Timers.ShouldBeEmpty();
    }

    [Fact]
    public void LectorDePantalla_DiceQueHayYQueSectorQuedaElegido()
    {
        _session.OnSignal(GestureSignal.Trigger, Center);
        _session.OnDragEnter(Files, Center);
        _session.OnDragOver(NorthOfInnerRing);
        _session.OnDragOver(NorthOfInnerRing); // sin cambio: no se repite
        _session.OnDragOver(Center);

        _surface.Announcements.ShouldBe(
        [
            "Rueda de Kumquat: 1 archivo(s). 2 opciones. Mueve el cursor sobre una y suelta.",
            "Convertir a JPG",
            "Centro: soltar aquí cancela",
        ]);
    }

    [Fact]
    public void Arrastre_SoltarEnElCentro_CierraSinEsperar()
    {
        _session.OnSignal(GestureSignal.Trigger, Center);
        _session.OnDragEnter(Files, Center);

        _session.OnSignal(GestureSignal.DragEnded, Center);

        _surface.IsShown.ShouldBeFalse();
        _surface.Timers.ShouldBeEmpty();
        _chosen.ShouldBeEmpty();
    }

    [Fact]
    public void Arrastre_SinDropTrasLaEspera_CierraSinElegir()
    {
        _session.OnSignal(GestureSignal.Trigger, Center);
        _session.OnDragEnter(Files, NorthOfInnerRing);
        _session.OnSignal(GestureSignal.DragEnded, NorthOfInnerRing);

        _session.OnTimer(WheelSession.CloseTimerId);

        _surface.IsShown.ShouldBeFalse();
        _chosen.ShouldBeEmpty();
    }

    [Fact]
    public void Arrastre_SalirDeLaRueda_QuitaElResaltadoPeroLaDeja()
    {
        _session.OnSignal(GestureSignal.Trigger, Center);
        _session.OnDragEnter(Files, NorthOfInnerRing);

        _session.OnDragLeave();

        _session.State.HasHighlight.ShouldBeFalse();
        _surface.IsShown.ShouldBeTrue();
    }

    [Fact]
    public void DragEnter_ConLaRuedaEstacionada_SeIgnora()
    {
        _session.OnDragEnter(Files, Center).ShouldBeFalse();
        _surface.Renders.ShouldBe(0);
    }

    [Fact]
    public void Escape_Cierra()
    {
        _session.OnSignal(GestureSignal.Trigger, Center);
        _session.OnSignal(GestureSignal.Cancel, Center);
        _surface.IsShown.ShouldBeFalse();
    }

    [Fact]
    public void Atajo_SinSeleccion_Avisa()
    {
        bool? explorer = null;
        _session.NothingSelected = inFront => explorer = inFront;
        _session.ExplorerInFront = () => true;

        _session.OnHotkey();

        explorer.ShouldBe(true);
        _surface.IsShown.ShouldBeFalse();
    }

    [Fact]
    public void Atajo_ConSeleccion_SeEligeConUnClic()
    {
        _session.Selection = () => Files;

        _session.OnHotkey();
        _session.InClickMode.ShouldBeTrue();
        _tracker.ClickMode.ShouldBeTrue();
        _surface.ShownAt.ShouldBe(Center);

        _session.OnSignal(GestureSignal.PointerMoved, NorthOfInnerRing);
        _session.State.HasHighlight.ShouldBeTrue();
        _session.OnSignal(GestureSignal.DragEnded, NorthOfInnerRing);

        _chosen.ShouldHaveSingleItem().Files.ShouldBe(Files);
        _session.InClickMode.ShouldBeFalse();
        _tracker.ClickMode.ShouldBeFalse();
    }

    [Fact]
    public void ConBuscador_ElAtajoYElMenuLoAbren_NoLaRueda()
    {
        var opened = new List<IReadOnlyList<string>>();
        _session.KeyboardPicker = opened.Add;
        _session.Selection = () => Files;

        _session.OnHotkey();
        _session.OnPathRequested(Files[0]);
        _session.OnTimer(WheelSession.RequestTimerId);

        opened.Count.ShouldBe(2);
        opened.ShouldAllBe(files => files.SequenceEqual(Files));
        _surface.IsShown.ShouldBeFalse("la rueda queda para arrastrar con el mouse");
        _session.InClickMode.ShouldBeFalse();
    }

    [Fact]
    public void ConBuscador_ElArrastreSigueUsandoLaRueda()
    {
        _session.KeyboardPicker = _ => throw new InvalidOperationException("el arrastre no abre el buscador");
        _session.OnSignal(GestureSignal.Trigger, Center);
        _session.OnDragEnter(Files, NorthOfInnerRing);
        _session.OnDrop(Files, NorthOfInnerRing);

        _chosen.ShouldHaveSingleItem();
    }

    [Fact]
    public void ModoClic_ClicEnElCentroOEscape_CierraSinElegir()
    {
        _session.Selection = () => Files;
        _session.OnHotkey();
        _session.OnSignal(GestureSignal.DragEnded, Center);
        _chosen.ShouldBeEmpty();
        _surface.IsShown.ShouldBeFalse();

        _session.OnHotkey();
        _session.OnSignal(GestureSignal.Cancel, Center);
        _surface.IsShown.ShouldBeFalse();
    }

    [Fact]
    public void Atajo_ConLaRuedaYaAbierta_NoHaceNada()
    {
        var asked = 0;
        _session.Selection = () =>
        {
            asked++;
            return Files;
        };
        _session.OnSignal(GestureSignal.Trigger, Center);

        _session.OnHotkey();

        asked.ShouldBe(0);
    }

    [Fact]
    public void MenuContextual_PedidosDeVariosProcesos_UnaSolaRuedaConLaSeleccionCompleta()
    {
        string[] selection = [@"C:\a.png", @"C:\b.png", @"C:\c.png"];
        _session.Selection = () => selection;
        IReadOnlyList<string>? shown = null;
        _session.CaptionForFiles = files =>
        {
            shown = files;
            return null;
        };

        _session.OnPathRequested(@"C:\a.png");
        _session.OnPathRequested(@"C:\B.PNG"); // mayúsculas: la misma ruta en Windows
        _session.OnPathRequested(@"C:\a.png");
        _surface.IsShown.ShouldBeFalse("se espera a que lleguen todos los pedidos");

        _session.OnTimer(WheelSession.RequestTimerId);

        _surface.IsShown.ShouldBeTrue();
        _session.InClickMode.ShouldBeTrue();
        shown.ShouldBe(selection);
    }

    [Fact]
    public void MenuContextual_SiNoEsParteDeLaSeleccion_UsaLoPedido()
    {
        _session.Selection = () => [@"C:\otro.png"];
        IReadOnlyList<string>? shown = null;
        _session.CaptionForFiles = files =>
        {
            shown = files;
            return null;
        };

        _session.OnPathRequested(@"C:\a.png");
        _session.OnTimer(WheelSession.RequestTimerId);

        shown.ShouldBe([@"C:\a.png"]);
    }

    [Fact]
    public void MenuContextual_RutasInexistentesOConLaRuedaAbierta_SeIgnoran()
    {
        _session.PathExists = path => !path.Contains("borrado", StringComparison.Ordinal);
        _session.OnPathRequested(@"C:\borrado.png");
        _surface.Timers.ShouldBeEmpty();

        _session.OnSignal(GestureSignal.Trigger, Center);
        _session.OnPathRequested(@"C:\a.png");
        _surface.Timers.ShouldBeEmpty();
    }

    [Fact]
    public void EtiquetaQueFalla_NoImpideAbrirLaRueda()
    {
        _session.CaptionForFiles = _ => throw new IOException("disco desconectado");
        _session.OnSignal(GestureSignal.Trigger, Center);

        _session.OnDragEnter(Files, NorthOfInnerRing).ShouldBeTrue();

        _session.State.Caption.ShouldBeNull();
    }

    [Fact]
    public void SinNadaQueHacer_MuestraElMensaje()
    {
        var session = new WheelSession(_surface, _tracker, _ => [], (_, _) => { });
        session.OnSignal(GestureSignal.Trigger, Center);

        session.OnDragEnter(Files, NorthOfInnerRing).ShouldBeFalse();

        session.State.Message.ShouldNotBeNull();
    }

    private sealed class FakeSurface : IWheelSurface
    {
        public bool IsShown { get; private set; }

        public POINT Center { get; private set; }

        public POINT? ShownAt { get; private set; }

        public int Renders { get; private set; }

        public Dictionary<nuint, uint> Timers { get; } = [];

        public WheelLayout LayoutFor(WheelVisualState state) => WheelRenderer.Layout(state, 1f);

        public void ShowAt(POINT screenPoint, WheelVisualState state)
        {
            IsShown = true;
            Center = screenPoint;
            ShownAt = screenPoint;
            Renders++;
        }

        public void Render(WheelVisualState state) => Renders++;

        public void Park() => IsShown = false;

        public void StartTimer(nuint id, uint milliseconds) => Timers[id] = milliseconds;

        public void StopTimer(nuint id) => Timers.Remove(id);

        public List<string> Announcements { get; } = [];

        public void Announce(string text) => Announcements.Add(text);
    }
}

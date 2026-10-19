// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Interaction.Input;

namespace Kumquat.Interaction.Tests;

public sealed class DragGestureTrackerTests
{
    private readonly DragGestureTracker _tracker = new(TriggerKey.Shift, thresholdX: 4, thresholdY: 4);

    [Fact]
    public void ShiftDuranteArrastre_AbreLaRueda()
    {
        StartDrag();

        _tracker.KeyDown(DragGestureTracker.VkLShift).ShouldBe(GestureSignal.Trigger);
    }

    [Fact]
    public void ShiftSinArrastre_NoHaceNada()
    {
        _tracker.KeyDown(DragGestureTracker.VkLShift).ShouldBe(GestureSignal.None);

        _tracker.LeftDown(0, 0);
        _tracker.Move(2, 2); // bajo el umbral: un clic, no un arrastre
        _tracker.KeyDown(DragGestureTracker.VkRShift).ShouldBe(GestureSignal.None);
    }

    [Fact]
    public void AutoRepeticion_DisparaUnaSolaVez()
    {
        StartDrag();

        _tracker.KeyDown(DragGestureTracker.VkLShift).ShouldBe(GestureSignal.Trigger);
        _tracker.KeyDown(DragGestureTracker.VkLShift).ShouldBe(GestureSignal.None);
        _tracker.KeyDown(DragGestureTracker.VkLShift).ShouldBe(GestureSignal.None);
    }

    [Fact]
    public void ConLaRuedaAbierta_NoSeReabre()
    {
        StartDrag();
        _tracker.WheelOpen = true;

        _tracker.KeyDown(DragGestureTracker.VkShift).ShouldBe(GestureSignal.None);
    }

    [Fact]
    public void SoltarBoton_TerminaElGesto()
    {
        StartDrag();

        _tracker.LeftUp().ShouldBe(GestureSignal.DragEnded);
        _tracker.IsDragging.ShouldBeFalse();
        _tracker.LeftUp().ShouldBe(GestureSignal.None);
    }

    [Fact]
    public void Escape_SoloCancelaConLaRuedaAbierta()
    {
        _tracker.KeyDown(DragGestureTracker.VkEscape).ShouldBe(GestureSignal.None);
        _tracker.WheelOpen = true;
        _tracker.KeyDown(DragGestureTracker.VkEscape).ShouldBe(GestureSignal.Cancel);
    }

    [Fact]
    public void ClicDerecho_ComoGestoConfigurable()
    {
        _tracker.Trigger = TriggerKey.RightClick;
        StartDrag();

        _tracker.KeyDown(DragGestureTracker.VkLShift).ShouldBe(GestureSignal.None);
        _tracker.RightDown().ShouldBe(GestureSignal.Trigger);
    }

    [Theory]
    [InlineData(TriggerKey.Control, DragGestureTracker.VkLControl)]
    [InlineData(TriggerKey.CapsLock, DragGestureTracker.VkCapital)]
    public void OtrasTeclas(TriggerKey trigger, int vk)
    {
        _tracker.Trigger = trigger;
        StartDrag();

        _tracker.KeyDown(vk).ShouldBe(GestureSignal.Trigger);
    }

    private void StartDrag()
    {
        _tracker.LeftDown(100, 100);
        _tracker.Move(110, 100);
        _tracker.IsDragging.ShouldBeTrue();
    }

    [Fact]
    public void ModoClic_InformaMovimientoYElClicCierra()
    {
        _tracker.Move(10, 10).ShouldBe(GestureSignal.None); // sin rueda: el hook no publica nada
        _tracker.WheelOpen = true;
        _tracker.ClickMode = true;

        _tracker.Move(20, 20).ShouldBe(GestureSignal.PointerMoved);
        _tracker.LeftDown(20, 20).ShouldBe(GestureSignal.None);
        _tracker.LeftUp().ShouldBe(GestureSignal.DragEnded);
        _tracker.KeyDown(DragGestureTracker.VkEscape).ShouldBe(GestureSignal.Cancel);
    }

    [Fact]
    public void ModoArrastre_NoInformaMovimiento()
    {
        StartDrag();
        _tracker.WheelOpen = true;

        _tracker.Move(500, 500).ShouldBe(GestureSignal.None);
    }
}

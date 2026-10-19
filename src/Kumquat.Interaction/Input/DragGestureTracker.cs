// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Interaction.Input;

/// <summary>
/// Máquina de estados pura del gesto "arrastrar + tecla". No sabe nada de Win32: la
/// alimentan los hooks y se prueba con eventos sintéticos.
/// </summary>
/// <remarks>
/// Un arrastre "candidato" es botón izquierdo abajo + movimiento mayor al umbral del
/// sistema (SM_CXDRAG/SM_CYDRAG). Puede no ser un arrastre OLE (mover una ventana,
/// seleccionar texto); eso lo confirma después el <c>DragEnter</c> de la rueda.
/// </remarks>
public sealed class DragGestureTracker
{
    /// <summary>Teclas virtuales relevantes.</summary>
    public const int VkShift = 0x10;
    public const int VkLShift = 0xA0;
    public const int VkRShift = 0xA1;
    public const int VkControl = 0x11;
    public const int VkLControl = 0xA2;
    public const int VkRControl = 0xA3;
    public const int VkCapital = 0x14;
    public const int VkEscape = 0x1B;

    private readonly int _thresholdX;
    private readonly int _thresholdY;
    private bool _leftDown;
    private bool _dragging;
    private bool _triggerHeld;
    private volatile bool _wheelOpen;
    private volatile bool _clickMode;
    private int _downX;
    private int _downY;

    /// <summary>Crea el tracker.</summary>
    public DragGestureTracker(TriggerKey trigger, int thresholdX, int thresholdY)
    {
        Trigger = trigger;
        _thresholdX = Math.Max(1, thresholdX);
        _thresholdY = Math.Max(1, thresholdY);
    }

    /// <summary>Gesto configurado.</summary>
    public TriggerKey Trigger { get; set; }

    /// <summary>Indica si hay un arrastre candidato en curso.</summary>
    public bool IsDragging => _dragging;

    /// <summary>Indica si la rueda está abierta (lo escribe el hilo de UI, lo lee el del hook).</summary>
    public bool WheelOpen
    {
        get => _wheelOpen;
        set => _wheelOpen = value;
    }

    /// <summary>
    /// Rueda abierta con el atajo de teclado (sin arrastre): se elige con un clic, así que el
    /// hook también informa el movimiento del puntero. Lo escribe el hilo de UI.
    /// </summary>
    public bool ClickMode
    {
        get => _clickMode;
        set => _clickMode = value;
    }

    /// <summary>Botón izquierdo presionado.</summary>
    public GestureSignal LeftDown(int x, int y)
    {
        _leftDown = true;
        _dragging = false;
        _downX = x;
        _downY = y;
        return GestureSignal.None;
    }

    /// <summary>Movimiento del mouse.</summary>
    public GestureSignal Move(int x, int y)
    {
        if (_leftDown && !_dragging && (Math.Abs(x - _downX) >= _thresholdX || Math.Abs(y - _downY) >= _thresholdY))
        {
            _dragging = true;
            return GestureSignal.DragStarted;
        }

        return WheelOpen && ClickMode ? GestureSignal.PointerMoved : GestureSignal.None;
    }

    /// <summary>Botón izquierdo liberado.</summary>
    public GestureSignal LeftUp()
    {
        var wasActive = _dragging || WheelOpen;
        _leftDown = false;
        _dragging = false;
        return wasActive ? GestureSignal.DragEnded : GestureSignal.None;
    }

    /// <summary>Botón derecho presionado.</summary>
    public GestureSignal RightDown()
        => Trigger == TriggerKey.RightClick && _dragging && !WheelOpen ? GestureSignal.Trigger : GestureSignal.None;

    /// <summary>Tecla presionada (incluye auto-repetición, que se ignora).</summary>
    public GestureSignal KeyDown(int virtualKey)
    {
        if (virtualKey == VkEscape)
        {
            return WheelOpen ? GestureSignal.Cancel : GestureSignal.None;
        }

        if (!IsTriggerKey(virtualKey))
        {
            return GestureSignal.None;
        }

        var firstPress = !_triggerHeld;
        _triggerHeld = true;
        return firstPress && _dragging && !WheelOpen ? GestureSignal.Trigger : GestureSignal.None;
    }

    /// <summary>Tecla liberada.</summary>
    public GestureSignal KeyUp(int virtualKey)
    {
        if (IsTriggerKey(virtualKey))
        {
            _triggerHeld = false;
        }

        return GestureSignal.None;
    }

    private bool IsTriggerKey(int vk) => Trigger switch
    {
        TriggerKey.Shift => vk is VkShift or VkLShift or VkRShift,
        TriggerKey.Control => vk is VkControl or VkLControl or VkRControl,
        TriggerKey.CapsLock => vk == VkCapital,
        _ => false,
    };
}

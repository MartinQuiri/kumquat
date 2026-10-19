// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Core.Previews;

/// <summary>
/// Reproducción para la vista previa, con los mismos motores que convierten (así se puede ver
/// y oír cualquier formato que Kumquat acepte, sin depender de los códecs de Windows).
/// </summary>
public interface IMediaPlayback
{
    /// <summary>Tamaño de los fotogramas (0×0 si es solo audio).</summary>
    (int Width, int Height) FrameSize { get; }

    /// <summary>Fotograma exacto en un instante (BGRA), o <c>null</c> si no hay video o no se pudo leer.</summary>
    Task<VideoFrame?> FrameAtAsync(TimeSpan time, CancellationToken cancellationToken);

    /// <summary>
    /// Reproduce de <paramref name="from"/> a <paramref name="to"/> (audio por la salida
    /// predeterminada). Termina al llegar al final o al cancelar (pausa). Los callbacks llegan
    /// desde otros hilos: <paramref name="onFrame"/> recibe fotogramas a mostrar (quien los recibe
    /// los desecha) y <paramref name="onPosition"/> el instante que está sonando.
    /// </summary>
    Task PlayAsync(TimeSpan from, TimeSpan to, Action<VideoFrame> onFrame, Action<TimeSpan> onPosition, CancellationToken cancellationToken);
}

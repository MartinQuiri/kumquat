// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Core.Previews;

/// <summary>
/// Fotograma BGRA de 32 bits. Su memoria es prestada (se reutiliza durante la reproducción):
/// quien lo recibe lo copia y lo desecha.
/// </summary>
public sealed class VideoFrame : IDisposable
{
    private readonly Action<byte[]>? _release;
    private byte[]? _pixels;

    /// <summary>Crea un fotograma.</summary>
    /// <param name="width">Ancho en píxeles.</param>
    /// <param name="height">Alto en píxeles.</param>
    /// <param name="pixels">BGRA, <c>width × height × 4</c> bytes (puede ser más largo).</param>
    /// <param name="time">Instante del medio.</param>
    /// <param name="release">Devuelve la memoria al dueño al desechar.</param>
    public VideoFrame(int width, int height, byte[] pixels, TimeSpan time, Action<byte[]>? release = null)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (pixels.Length < width * height * 4)
        {
            throw new ArgumentException("El búfer es más chico que el fotograma.", nameof(pixels));
        }

        Width = width;
        Height = height;
        Time = time;
        _pixels = pixels;
        _release = release;
    }

    /// <summary>Ancho en píxeles.</summary>
    public int Width { get; }

    /// <summary>Alto en píxeles.</summary>
    public int Height { get; }

    /// <summary>Instante del medio que muestra.</summary>
    public TimeSpan Time { get; }

    /// <summary>Bytes por fila.</summary>
    public int Stride => Width * 4;

    /// <summary>Píxeles BGRA.</summary>
    public ReadOnlySpan<byte> Pixels => (_pixels ?? throw new ObjectDisposedException(nameof(VideoFrame))).AsSpan(0, Stride * Height);

    /// <inheritdoc />
    public void Dispose()
    {
        var pixels = Interlocked.Exchange(ref _pixels, null);
        if (pixels is not null)
        {
            _release?.Invoke(pixels);
        }
    }
}

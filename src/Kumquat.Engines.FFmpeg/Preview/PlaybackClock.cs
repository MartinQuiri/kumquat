// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Diagnostics;
using Kumquat.Infrastructure.Audio;

namespace Kumquat.Engines.FFmpeg.Preview;

/// <summary>
/// Reloj de la reproducción: el audio que ya sonó (lo que el usuario oye manda) o, sin audio,
/// el tiempo transcurrido desde <see cref="Start"/>. Al terminar el audio continúa sin saltos con
/// el tiempo de pared.
/// </summary>
internal sealed class PlaybackClock
{
    private readonly TimeSpan _from;
    private readonly Stopwatch _wall = new();
    private WaveOutPlayer? _audio;
    private TimeSpan _offset;

    public PlaybackClock(TimeSpan from, WaveOutPlayer? audio)
    {
        _from = from;
        _audio = audio;
    }

    /// <summary>Instante del medio que está sonando.</summary>
    public TimeSpan Now => _from + (_audio is { } audio ? audio.Played : _offset + _wall.Elapsed);

    /// <summary>
    /// Echa a andar el tiempo de pared (idempotente). Se llama con el primer fotograma listo: si
    /// corriera desde antes, lo que FFmpeg tarda en arrancar contaría como atraso y la
    /// reproducción empezaría saltándose ese tramo (en un PC lento, casi un segundo).
    /// </summary>
    public void Start()
    {
        if (!_wall.IsRunning)
        {
            _wall.Start();
        }
    }

    /// <summary>Pasa al tiempo de pared desde el punto actual.</summary>
    public void DetachAudio()
    {
        if (_audio is { } audio)
        {
            _offset = audio.Played;
            _wall.Restart();
            _audio = null;
        }
    }
}

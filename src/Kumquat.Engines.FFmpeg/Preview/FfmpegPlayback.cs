// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Collections.Concurrent;
using Kumquat.Core.Previews;
using Kumquat.Infrastructure.Audio;

namespace Kumquat.Engines.FFmpeg.Preview;

/// <summary>
/// Reproductor de la vista previa hecho con FFmpeg: un proceso decodifica el video a BGRA y otro
/// el audio a PCM, que suena por waveOut. El reloj es el audio que ya sonó (o el reloj de pared
/// si no hay audio); los fotogramas atrasados se descartan en lugar de acumular demora.
/// </summary>
public sealed class FfmpegPlayback : IMediaPlayback
{
    /// <summary>Fotogramas por segundo de la vista previa (suficiente para ver y elegir; liviano).</summary>
    public const int Fps = 25;

    /// <summary>Recuadro máximo de los fotogramas.</summary>
    public const int MaxWidth = 960;

    /// <summary>Recuadro máximo de los fotogramas.</summary>
    public const int MaxHeight = 540;

    private const int SampleRate = 48_000;
    private static readonly TimeSpan LateLimit = TimeSpan.FromMilliseconds(120);

    private readonly FfmpegRunner _ffmpeg;
    private readonly string _path;
    private readonly bool _hasVideo;
    private readonly bool _hasAudio;
    private readonly ConcurrentBag<byte[]> _pool = [];

    /// <summary>Crea el reproductor para un archivo ya analizado con ffprobe.</summary>
    /// <param name="ffmpeg">Motor.</param>
    /// <param name="path">Archivo.</param>
    /// <param name="info">Pistas y resolución.</param>
    public FfmpegPlayback(FfmpegRunner ffmpeg, string path, MediaInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        _ffmpeg = ffmpeg ?? throw new ArgumentNullException(nameof(ffmpeg));
        _path = path ?? throw new ArgumentNullException(nameof(path));
        _hasVideo = info.HasVideo && info.Width > 0 && info.Height > 0;
        _hasAudio = info.HasAudio;
        FrameSize = _hasVideo ? Fit(info.Width, info.Height) : (0, 0);
    }

    /// <inheritdoc />
    public (int Width, int Height) FrameSize { get; }

    private int FrameBytes => FrameSize.Width * FrameSize.Height * 4;

    /// <summary>Recuadro par que conserva la proporción y cabe en <see cref="MaxWidth"/>×<see cref="MaxHeight"/>.</summary>
    public static (int Width, int Height) Fit(int width, int height)
    {
        var scale = Math.Min(1.0, Math.Min((double)MaxWidth / width, (double)MaxHeight / height));
        static int Even(double value) => Math.Max(2, (int)Math.Round(value / 2, MidpointRounding.AwayFromZero) * 2);
        return (Even(width * scale), Even(height * scale));
    }

    /// <inheritdoc />
    public async Task<VideoFrame?> FrameAtAsync(TimeSpan time, CancellationToken cancellationToken)
    {
        if (!_hasVideo)
        {
            return null;
        }

        var buffer = Rent();
        var complete = false;
        var (width, height) = FrameSize;
        await _ffmpeg.StreamAsync(
            FfmpegArguments.RawVideo(_path, time, duration: null, width, height, Fps),
            async (stream, token) => complete = await ReadFullAsync(stream, buffer, FrameBytes, token) == FrameBytes,
            cancellationToken);
        if (complete)
        {
            return new VideoFrame(width, height, buffer, time, Return);
        }

        Return(buffer);
        return null;
    }

    /// <inheritdoc />
    public async Task PlayAsync(TimeSpan from, TimeSpan to, Action<VideoFrame> onFrame, Action<TimeSpan> onPosition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onFrame);
        ArgumentNullException.ThrowIfNull(onPosition);
        if (to <= from)
        {
            return;
        }

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var player = _hasAudio ? TryOpenAudio() : null;
        var clock = new PlaybackClock(from, player);
        var position = ReportPositionAsync(clock, to, onPosition, stop.Token);
        try
        {
            await Task.WhenAll(
                player is null ? Task.CompletedTask : PlayAudioAsync(player, clock, from, to, stop.Token),
                _hasVideo ? PlayVideoAsync(clock, from, to, onFrame, stop.Token) : Task.CompletedTask);

            // Solo audio y sin salida de audio: igual avanza el cabezal hasta el final.
            while (!_hasVideo && player is null && clock.Now < to)
            {
                await Task.Delay(30, stop.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Pausa: no es un error.
        }
        finally
        {
            await stop.CancelAsync();
            await position;
        }

        if (!cancellationToken.IsCancellationRequested)
        {
            onPosition(to);
        }
    }

    private static async Task<int> ReadFullAsync(Stream stream, byte[] buffer, int count, CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < count)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total, count - total), cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    private static WaveOutPlayer? TryOpenAudio()
    {
        try
        {
            return new WaveOutPlayer(SampleRate, channels: 2);
        }
        catch (InvalidOperationException)
        {
            return null; // Sin parlantes: se reproduce el video igual, con el reloj de pared.
        }
    }

    private static async Task ReportPositionAsync(PlaybackClock clock, TimeSpan to, Action<TimeSpan> onPosition, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var now = clock.Now;
                onPosition(now < to ? now : to);
                await Task.Delay(33, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Fin de la reproducción.
        }
    }

    private async Task PlayAudioAsync(WaveOutPlayer player, PlaybackClock clock, TimeSpan from, TimeSpan to, CancellationToken cancellationToken)
    {
        var chunk = new byte[player.BufferBytes];
        await _ffmpeg.StreamAsync(
            FfmpegArguments.RawAudio(_path, from, to - from, SampleRate),
            async (stream, token) =>
            {
                int read;
                while ((read = await ReadFullAsync(stream, chunk, chunk.Length, token)) > 0)
                {
                    await player.WriteAsync(chunk.AsMemory(0, read - (read % 4)), token);
                }
            },
            cancellationToken);
        await player.DrainAsync(cancellationToken);

        // Si el video dura más que el audio, el reloj sigue con el tiempo de pared.
        clock.DetachAudio();
    }

    private async Task PlayVideoAsync(PlaybackClock clock, TimeSpan from, TimeSpan to, Action<VideoFrame> onFrame, CancellationToken cancellationToken)
    {
        var (width, height) = FrameSize;
        await _ffmpeg.StreamAsync(
            FfmpegArguments.RawVideo(_path, from, to - from, width, height, Fps),
            async (stream, token) =>
            {
                for (var index = 0; ; index++)
                {
                    var buffer = Rent();
                    var time = from + TimeSpan.FromSeconds((double)index / Fps);
                    if (time > to || await ReadFullAsync(stream, buffer, FrameBytes, token) < FrameBytes)
                    {
                        Return(buffer);
                        break;
                    }

                    while (clock.Now < time)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(Math.Clamp((time - clock.Now).TotalMilliseconds, 1, 15)), token);
                    }

                    if (clock.Now - time > LateLimit)
                    {
                        Return(buffer); // atrasado: se descarta para no acumular demora
                        continue;
                    }

                    onFrame(new VideoFrame(width, height, buffer, time, Return));
                }
            },
            cancellationToken);
    }

    private byte[] Rent() => _pool.TryTake(out var buffer) && buffer.Length >= FrameBytes ? buffer : new byte[Math.Max(FrameBytes, 4)];

    private void Return(byte[] buffer)
    {
        // Pocos búferes bastan: el que se muestra, el que viaja y el que se decodifica.
        if (_pool.Count < 6)
        {
            _pool.Add(buffer);
        }
    }
}

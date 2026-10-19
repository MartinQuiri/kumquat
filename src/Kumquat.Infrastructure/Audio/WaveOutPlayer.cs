// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;

namespace Kumquat.Infrastructure.Audio;

/// <summary>
/// Reproduce PCM de 16 bits por la salida de audio predeterminada con la API waveOut de
/// Windows (disponible en todas las ediciones, sin componentes opcionales). Expone cuánto se
/// escuchó realmente: es el reloj con el que se sincroniza el video de la vista previa.
/// </summary>
public sealed partial class WaveOutPlayer : IDisposable
{
    private const uint WaveMapper = 0xFFFFFFFF;
    private const uint WhdrDone = 0x1;
    private const uint TimeBytes = 0x4;
    private const int BufferCount = 4;

    private readonly nint _device;
    private readonly unsafe WaveHeader* _headers;
    private readonly unsafe byte* _data;
    private readonly int _bufferBytes;
    private readonly int _bytesPerSecond;
    private readonly bool[] _queued = new bool[BufferCount];
    private bool _disposed;

    /// <summary>Abre la salida de audio.</summary>
    /// <exception cref="InvalidOperationException">No hay dispositivo de salida.</exception>
    public unsafe WaveOutPlayer(int sampleRate, int channels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);
        var format = new WaveFormat
        {
            FormatTag = 1, // PCM
            Channels = (ushort)channels,
            SamplesPerSecond = (uint)sampleRate,
            BlockAlign = (ushort)(channels * 2),
            BitsPerSample = 16,
        };
        format.AverageBytesPerSecond = format.SamplesPerSecond * format.BlockAlign;
        _bytesPerSecond = (int)format.AverageBytesPerSecond;

        var result = waveOutOpen(out _device, WaveMapper, &format, 0, 0, 0);
        if (result != 0)
        {
            throw new InvalidOperationException($"No hay salida de audio disponible (waveOutOpen: {result}).");
        }

        // Búferes de 80 ms: poca latencia al pausar y margen suficiente para no cortarse.
        _bufferBytes = Math.Max(format.BlockAlign, _bytesPerSecond / 1000 * 80 / format.BlockAlign * format.BlockAlign);
        _headers = (WaveHeader*)NativeMemory.AllocZeroed((nuint)(sizeof(WaveHeader) * BufferCount));
        _data = (byte*)NativeMemory.Alloc((nuint)(_bufferBytes * BufferCount));
        for (var i = 0; i < BufferCount; i++)
        {
            _headers[i].Data = (nint)(_data + (i * _bufferBytes));
        }
    }

    /// <summary>Tamaño ideal de cada escritura, en bytes.</summary>
    public int BufferBytes => _bufferBytes;

    /// <summary>Tiempo de audio que ya sonó desde que se abrió.</summary>
    public unsafe TimeSpan Played
    {
        get
        {
            var time = new MmTime { Type = TimeBytes };
            return !_disposed && waveOutGetPosition(_device, &time, sizeof(MmTime)) == 0
                ? TimeSpan.FromSeconds((double)time.Value / _bytesPerSecond)
                : TimeSpan.Zero;
        }
    }

    /// <summary>Encola audio; espera si todos los búferes están sonando.</summary>
    public async Task WriteAsync(ReadOnlyMemory<byte> pcm, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < pcm.Length)
        {
            var index = await FreeBufferAsync(cancellationToken);
            var count = Math.Min(_bufferBytes, pcm.Length - offset);
            Queue(index, pcm.Span.Slice(offset, count));
            offset += count;
        }
    }

    /// <summary>Espera a que termine de sonar todo lo encolado.</summary>
    public async Task DrainAsync(CancellationToken cancellationToken)
    {
        for (var i = 0; i < BufferCount; i++)
        {
            while (_queued[i] && !IsDone(i))
            {
                await Task.Delay(10, cancellationToken);
            }
        }
    }

    /// <inheritdoc />
    public unsafe void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _ = waveOutReset(_device); // corta en seco y marca todo como terminado
        for (var i = 0; i < BufferCount; i++)
        {
            if (_queued[i])
            {
                _ = waveOutUnprepareHeader(_device, &_headers[i], sizeof(WaveHeader));
            }
        }

        _ = waveOutClose(_device);
        NativeMemory.Free(_headers);
        NativeMemory.Free(_data);
    }

    private async Task<int> FreeBufferAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            for (var i = 0; i < BufferCount; i++)
            {
                if (!_queued[i] || IsDone(i))
                {
                    return i;
                }
            }

            await Task.Delay(5, cancellationToken);
        }
    }

    private unsafe bool IsDone(int index) => (_headers[index].Flags & WhdrDone) != 0;

    private unsafe void Queue(int index, ReadOnlySpan<byte> pcm)
    {
        var header = &_headers[index];
        if (_queued[index])
        {
            _ = waveOutUnprepareHeader(_device, header, sizeof(WaveHeader));
        }

        pcm.CopyTo(new Span<byte>((void*)header->Data, pcm.Length));
        header->BufferLength = (uint)pcm.Length;
        header->Flags = 0;
        _ = waveOutPrepareHeader(_device, header, sizeof(WaveHeader));
        _ = waveOutWrite(_device, header, sizeof(WaveHeader));
        _queued[index] = true;
    }

    [LibraryImport("winmm.dll")]
    private static unsafe partial uint waveOutOpen(out nint device, uint deviceId, WaveFormat* format, nint callback, nint instance, uint flags);

    [LibraryImport("winmm.dll")]
    private static unsafe partial uint waveOutPrepareHeader(nint device, WaveHeader* header, int size);

    [LibraryImport("winmm.dll")]
    private static unsafe partial uint waveOutUnprepareHeader(nint device, WaveHeader* header, int size);

    [LibraryImport("winmm.dll")]
    private static unsafe partial uint waveOutWrite(nint device, WaveHeader* header, int size);

    [LibraryImport("winmm.dll")]
    private static partial uint waveOutReset(nint device);

    [LibraryImport("winmm.dll")]
    private static partial uint waveOutClose(nint device);

    [LibraryImport("winmm.dll")]
    private static unsafe partial uint waveOutGetPosition(nint device, MmTime* time, int size);

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    private struct WaveFormat
    {
        public ushort FormatTag;
        public ushort Channels;
        public uint SamplesPerSecond;
        public uint AverageBytesPerSecond;
        public ushort BlockAlign;
        public ushort BitsPerSample;
        public ushort ExtraSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHeader
    {
        public nint Data;
        public uint BufferLength;
        public uint BytesRecorded;
        public nuint User;
        public uint Flags;
        public uint Loops;
        public nint Next;
        public nuint Reserved;
    }

    /// <summary>MMTIME con TIME_BYTES: la unión mide 8 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 12)]
    private struct MmTime
    {
        [FieldOffset(0)]
        public uint Type;

        [FieldOffset(4)]
        public uint Value;
    }
}

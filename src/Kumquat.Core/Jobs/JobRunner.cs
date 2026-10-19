// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Collections.Concurrent;
using System.Threading.Channels;
using Kumquat.Core.Conversion;
using Kumquat.Core.Results;

namespace Kumquat.Core.Jobs;

/// <summary>
/// Cola de conversiones con concurrencia acotada. Los eventos se disparan desde hilos
/// del pool: la UI debe pasarlos a su propio hilo.
/// </summary>
public sealed class JobRunner : IAsyncDisposable
{
    private readonly Func<ConversionRequest, IProgress<double>, CancellationToken, Task<Result<string>>> _convert;
    private readonly Channel<(Guid Id, ConversionRequest Request)> _channel;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _running = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task[] _workers;

    /// <summary>Crea la cola usando un servicio de conversión.</summary>
    public JobRunner(ConversionService service, int maxConcurrency)
        : this((request, progress, ct) => (service ?? throw new ArgumentNullException(nameof(service))).ConvertAsync(request, progress, ct), maxConcurrency)
    {
    }

    /// <summary>Crea la cola con una función de conversión arbitraria (útil en tests).</summary>
    public JobRunner(Func<ConversionRequest, IProgress<double>, CancellationToken, Task<Result<string>>> convert, int maxConcurrency)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrency, 1);
        _convert = convert ?? throw new ArgumentNullException(nameof(convert));
        _channel = Channel.CreateUnbounded<(Guid, ConversionRequest)>(new UnboundedChannelOptions { SingleWriter = false, SingleReader = maxConcurrency == 1 });
        _workers = [.. Enumerable.Range(0, maxConcurrency).Select(_ => Task.Run(WorkerLoopAsync))];
    }

    /// <summary>Se dispara cada vez que cambia el estado o el progreso de un trabajo.</summary>
    public event EventHandler<JobChangedEventArgs>? JobChanged;

    /// <summary>Encola una conversión.</summary>
    /// <returns>Identificador del trabajo.</returns>
    public Guid Enqueue(ConversionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var id = Guid.NewGuid();
        if (!_channel.Writer.TryWrite((id, request)))
        {
            throw new InvalidOperationException("La cola está cerrada.");
        }

        Raise(new JobSnapshot(id, request, JobState.Queued, 0, null, null));
        return id;
    }

    /// <summary>Cancela un trabajo en ejecución. Devuelve <c>false</c> si no estaba corriendo.</summary>
    public bool Cancel(Guid id)
    {
        if (_running.TryGetValue(id, out var cts))
        {
            cts.Cancel();
            return true;
        }

        return false;
    }

    /// <summary>Deja de aceptar trabajos, cancela los activos y espera a los workers.</summary>
    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        await _shutdown.CancelAsync();
        await Task.WhenAll(_workers);
        _shutdown.Dispose();
    }

    private async Task WorkerLoopAsync()
    {
        try
        {
            await foreach (var (id, request) in _channel.Reader.ReadAllAsync(_shutdown.Token))
            {
                await RunAsync(id, request);
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
            // Apagado ordenado.
        }
    }

    private async Task RunAsync(Guid id, ConversionRequest request)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
        _running[id] = cts;
        try
        {
            Raise(new JobSnapshot(id, request, JobState.Running, 0, null, null));
            var progress = new Progress(this, id, request);
            var result = await _convert(request, progress, cts.Token);
            Raise(result.IsSuccess
                ? new JobSnapshot(id, request, JobState.Succeeded, 1, result.Value, null)
                : new JobSnapshot(id, request, result.Error == Error.Cancelled ? JobState.Cancelled : JobState.Failed, 0, null, result.Error));
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            Raise(new JobSnapshot(id, request, JobState.Cancelled, 0, null, Error.Cancelled));
        }
        finally
        {
            _running.TryRemove(id, out _);
        }
    }

    private void Raise(JobSnapshot snapshot) => JobChanged?.Invoke(this, new JobChangedEventArgs(snapshot));

    private sealed class Progress(JobRunner owner, Guid id, ConversionRequest request) : IProgress<double>
    {
        public void Report(double value) => owner.Raise(new JobSnapshot(id, request, JobState.Running, value, null, null));
    }
}

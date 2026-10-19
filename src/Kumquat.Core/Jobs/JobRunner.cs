// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Collections.Concurrent;
using System.Threading.Channels;
using Kumquat.Core.Results;

namespace Kumquat.Core.Jobs;

/// <summary>
/// Cola de trabajos con concurrencia acotada. Los eventos se disparan desde hilos
/// del pool: la UI debe pasarlos a su propio hilo.
/// </summary>
public sealed class JobRunner : IAsyncDisposable
{
    private readonly Channel<(Guid Id, JobRequest Request)> _channel;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _running = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task[] _workers;

    /// <summary>Crea la cola.</summary>
    /// <param name="maxConcurrency">Trabajos simultáneos como máximo.</param>
    public JobRunner(int maxConcurrency)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrency, 1);
        _channel = Channel.CreateUnbounded<(Guid, JobRequest)>(new UnboundedChannelOptions { SingleWriter = false, SingleReader = maxConcurrency == 1 });
        _workers = [.. Enumerable.Range(0, maxConcurrency).Select(_ => Task.Run(WorkerLoopAsync))];
    }

    /// <summary>Se dispara cada vez que cambia el estado o el progreso de un trabajo.</summary>
    public event EventHandler<JobChangedEventArgs>? JobChanged;

    /// <summary>Encola un trabajo.</summary>
    /// <returns>Identificador del trabajo.</returns>
    public Guid Enqueue(JobRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var id = Guid.NewGuid();

        // Se anuncia ANTES de encolar: un worker libre puede tomarlo y terminarlo al instante, y
        // "en cola" no puede llegar después de "terminado" (quien sigue el lote esperaría para siempre).
        Raise(new JobSnapshot(id, request, JobState.Queued, 0, null, null));
        if (!_channel.Writer.TryWrite((id, request)))
        {
            Raise(new JobSnapshot(id, request, JobState.Cancelled, 0, null, Error.Cancelled));
            throw new InvalidOperationException("La cola está cerrada.");
        }

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

    private async Task RunAsync(Guid id, JobRequest request)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
        _running[id] = cts;
        try
        {
            Raise(new JobSnapshot(id, request, JobState.Running, 0, null, null));
            var result = await request.Work(new Progress(this, id, request), cts.Token);
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

    private sealed class Progress(JobRunner owner, Guid id, JobRequest request) : IProgress<double>
    {
        public void Report(double value) => owner.Raise(new JobSnapshot(id, request, JobState.Running, value, null, null));
    }
}

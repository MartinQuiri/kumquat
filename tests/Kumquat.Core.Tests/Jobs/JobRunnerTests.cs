// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Collections.Concurrent;
using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;
using Kumquat.Core.Jobs;
using Kumquat.Core.Results;

namespace Kumquat.Core.Tests.Jobs;

public sealed class JobRunnerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Enqueue_EjecutaYNotificaEstados()
    {
        var events = new ConcurrentQueue<JobSnapshot>();
        var done = new TaskCompletionSource();
        await using var runner = new JobRunner((r, p, _) =>
        {
            p.Report(0.5);
            return Task.FromResult(Result<string>.Success(r.InputPath + ".out"));
        }, maxConcurrency: 1);
        runner.JobChanged += (_, e) =>
        {
            events.Enqueue(e.Snapshot);
            if (e.Snapshot.State == JobState.Succeeded)
            {
                done.TrySetResult();
            }
        };

        var id = runner.Enqueue(new ConversionRequest("a.txt", KnownFormats.Pdf));
        await done.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        var states = events.Where(s => s.Id == id).Select(s => s.State).ToList();
        states.ShouldContain(JobState.Running);
        states[^1].ShouldBe(JobState.Succeeded);
        events.Last().OutputPath.ShouldBe("a.txt.out");
    }

    [Fact]
    public async Task Concurrencia_NoSuperaElMaximo()
    {
        var active = 0;
        var peak = 0;
        var finished = 0;
        var allDone = new TaskCompletionSource();
        await using var runner = new JobRunner(async (r, _, ct) =>
        {
            var now = Interlocked.Increment(ref active);
            InterlockedMax(ref peak, now);
            await Task.Delay(30, ct);
            Interlocked.Decrement(ref active);
            if (Interlocked.Increment(ref finished) == 8)
            {
                allDone.TrySetResult();
            }

            return Result<string>.Success(r.InputPath);
        }, maxConcurrency: 2);

        for (var i = 0; i < 8; i++)
        {
            runner.Enqueue(new ConversionRequest($"{i}.txt", KnownFormats.Pdf));
        }

        await allDone.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        peak.ShouldBeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task Cancel_TerminaElTrabajoComoCancelado()
    {
        var started = new TaskCompletionSource();
        var cancelled = new TaskCompletionSource<JobSnapshot>();
        await using var runner = new JobRunner(async (_, _, ct) =>
        {
            started.TrySetResult();
            await Task.Delay(System.Threading.Timeout.Infinite, ct);
            return Result<string>.Success("nunca");
        }, maxConcurrency: 1);
        runner.JobChanged += (_, e) =>
        {
            if (e.Snapshot.State == JobState.Cancelled)
            {
                cancelled.TrySetResult(e.Snapshot);
            }
        };

        var id = runner.Enqueue(new ConversionRequest("a.txt", KnownFormats.Pdf));
        await started.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        runner.Cancel(id).ShouldBeTrue();
        (await cancelled.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken)).Error.ShouldBe(Error.Cancelled);
    }

    [Fact]
    public async Task Error_SeReportaComoFailed()
    {
        var failed = new TaskCompletionSource<JobSnapshot>();
        await using var runner = new JobRunner((_, _, _) => Task.FromResult(Result<string>.Failure(new Error("x", "y"))), maxConcurrency: 1);
        runner.JobChanged += (_, e) =>
        {
            if (e.Snapshot.State == JobState.Failed)
            {
                failed.TrySetResult(e.Snapshot);
            }
        };

        runner.Enqueue(new ConversionRequest("a.txt", KnownFormats.Pdf));

        (await failed.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken)).Error!.Code.ShouldBe("x");
    }

    [Fact]
    public async Task Dispose_ConTrabajosActivos_TerminaSinColgarse()
    {
        var started = new TaskCompletionSource();
        var runner = new JobRunner(async (_, _, ct) =>
        {
            started.TrySetResult();
            await Task.Delay(System.Threading.Timeout.Infinite, ct);
            return Result<string>.Success("nunca");
        }, maxConcurrency: 1);
        runner.Enqueue(new ConversionRequest("a.txt", KnownFormats.Pdf));
        await started.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        await runner.DisposeAsync().AsTask().WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Should.Throw<InvalidOperationException>(() => runner.Enqueue(new ConversionRequest("b.txt", KnownFormats.Pdf)));
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while ((current = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, current) != current)
        {
        }
    }
}

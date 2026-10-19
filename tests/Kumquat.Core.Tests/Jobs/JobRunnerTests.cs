// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Collections.Concurrent;
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
        await using var runner = new JobRunner(maxConcurrency: 1);
        runner.JobChanged += (_, e) =>
        {
            events.Enqueue(e.Snapshot);
            if (e.Snapshot.State == JobState.Succeeded)
            {
                done.TrySetResult();
            }
        };

        var id = runner.Enqueue(Job("a.txt", (p, _) =>
        {
            p.Report(0.5);
            return Task.FromResult(Result<string>.Success("a.txt.out"));
        }));
        await done.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        var states = events.Where(s => s.Id == id).Select(s => s.State).ToList();
        states[0].ShouldBe(JobState.Queued);
        states.ShouldContain(JobState.Running);
        states[^1].ShouldBe(JobState.Succeeded);
        events.Last().OutputPath.ShouldBe("a.txt.out");
        events.ShouldContain(s => s.State == JobState.Running && s.Progress == 0.5);
    }

    [Fact]
    public async Task Concurrencia_NoSuperaElMaximo()
    {
        var active = 0;
        var peak = 0;
        var finished = 0;
        var allDone = new TaskCompletionSource();
        await using var runner = new JobRunner(maxConcurrency: 2);

        for (var i = 0; i < 8; i++)
        {
            runner.Enqueue(Job($"{i}.txt", async (_, ct) =>
            {
                var now = Interlocked.Increment(ref active);
                InterlockedMax(ref peak, now);
                await Task.Delay(30, ct);
                Interlocked.Decrement(ref active);
                if (Interlocked.Increment(ref finished) == 8)
                {
                    allDone.TrySetResult();
                }

                return Result<string>.Success("ok");
            }));
        }

        await allDone.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        peak.ShouldBeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task Cancel_TerminaElTrabajoComoCancelado()
    {
        var started = new TaskCompletionSource();
        var cancelled = new TaskCompletionSource<JobSnapshot>();
        await using var runner = new JobRunner(maxConcurrency: 1);
        runner.JobChanged += (_, e) =>
        {
            if (e.Snapshot.State == JobState.Cancelled)
            {
                cancelled.TrySetResult(e.Snapshot);
            }
        };

        var id = runner.Enqueue(Job("a.txt", async (_, ct) =>
        {
            started.TrySetResult();
            await Task.Delay(System.Threading.Timeout.Infinite, ct);
            return Result<string>.Success("nunca");
        }));
        await started.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        runner.Cancel(id).ShouldBeTrue();
        (await cancelled.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken)).Error.ShouldBe(Error.Cancelled);
    }

    [Fact]
    public async Task Error_SeReportaComoFailed()
    {
        var failed = new TaskCompletionSource<JobSnapshot>();
        await using var runner = new JobRunner(maxConcurrency: 1);
        runner.JobChanged += (_, e) =>
        {
            if (e.Snapshot.State == JobState.Failed)
            {
                failed.TrySetResult(e.Snapshot);
            }
        };

        runner.Enqueue(Job("a.txt", (_, _) => Task.FromResult(Result<string>.Failure(new Error("x", "y")))));

        (await failed.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken)).Error!.Code.ShouldBe("x");
    }

    [Fact]
    public async Task Dispose_ConTrabajosActivos_TerminaSinColgarse()
    {
        var started = new TaskCompletionSource();
        var runner = new JobRunner(maxConcurrency: 1);
        runner.Enqueue(Job("a.txt", async (_, ct) =>
        {
            started.TrySetResult();
            await Task.Delay(System.Threading.Timeout.Infinite, ct);
            return Result<string>.Success("nunca");
        }));
        await started.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        await runner.DisposeAsync().AsTask().WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Should.Throw<InvalidOperationException>(() => runner.Enqueue(Job("b.txt", (_, _) => Task.FromResult(Result<string>.Success("x")))));
    }

    private static JobRequest Job(string input, Func<IProgress<double>, CancellationToken, Task<Result<string>>> work)
        => new(input, "prueba", work);

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while ((current = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, current) != current)
        {
        }
    }
}

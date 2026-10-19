// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Diagnostics;
using System.Globalization;
using Kumquat.Infrastructure.Processes;

namespace Kumquat.Infrastructure.Tests.Processes;

public sealed class ProcessRunnerTests
{
    private readonly ProcessRunner _runner = new();

    private static string ProbePath
    {
        get
        {
            // tests/Kumquat.Infrastructure.Tests/bin/<cfg>/<tfm>/ -> tests/Kumquat.TestTools.ProcessProbe/bin/<cfg>/net10.0/
            var baseDir = new DirectoryInfo(AppContext.BaseDirectory);
            var configuration = baseDir.Parent!.Name;
            var testsDir = baseDir.Parent!.Parent!.Parent!.Parent!.FullName;
            var path = Path.Combine(testsDir, "Kumquat.TestTools.ProcessProbe", "bin", configuration, "net10.0", "Kumquat.TestTools.ProcessProbe.exe");
            File.Exists(path).ShouldBeTrue($"No se encontró el probe en {path}");
            return path;
        }
    }

    [Theory]
    [InlineData("simple")]
    [InlineData("con espacios")]
    [InlineData("comillas \"dentro\"")]
    [InlineData(@"barra\final\")]
    [InlineData("-y")]
    [InlineData("& calc.exe")]
    [InlineData("%PATH%")]
    [InlineData("ñandú 漢字")]
    public async Task Argumentos_LleganIntactos(string argument)
    {
        var lines = new List<string>();

        var outcome = await _runner.RunAsync(Spec(["echo", argument], lines.Add), TestContext.Current.CancellationToken);

        outcome.Succeeded.ShouldBeTrue(outcome.StandardErrorTail);
        lines.ShouldBe([argument]);
    }

    [Fact]
    public async Task CodigoDeSalida_SePropaga()
    {
        var outcome = await _runner.RunAsync(Spec(["exit", "3"]), TestContext.Current.CancellationToken);

        outcome.ExitCode.ShouldBe(3);
        outcome.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task Stderr_GuardaSoloLaCola()
    {
        var outcome = await _runner.RunAsync(Spec(["stderr", "100"]), TestContext.Current.CancellationToken);

        var lines = outcome.StandardErrorTail.Split(Environment.NewLine);
        lines.Length.ShouldBe(40);
        lines[^1].ShouldBe("line 99");
    }

    [Fact]
    public async Task Timeout_TerminaElProceso()
    {
        var stopwatch = Stopwatch.StartNew();

        var outcome = await _runner.RunAsync(Spec(["sleep", "30000"]) with { Timeout = TimeSpan.FromMilliseconds(300) }, TestContext.Current.CancellationToken);

        outcome.TimedOut.ShouldBeTrue();
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Cancelacion_LanzaYMataAlHijoDelHijo()
    {
        using var cts = new CancellationTokenSource();
        int? grandchildPid = null;
        var spec = Spec(["spawn-sleep", "30000"], line =>
        {
            grandchildPid = int.Parse(line, CultureInfo.InvariantCulture);
            cts.Cancel();
        });

        await Should.ThrowAsync<OperationCanceledException>(() => _runner.RunAsync(spec, cts.Token));

        grandchildPid.ShouldNotBeNull();
        await WaitUntilExitedAsync(grandchildPid.Value);
    }

    [Fact]
    public async Task LimiteDeMemoria_ElProcesoFalla()
    {
        var outcome = await _runner.RunAsync(Spec(["alloc", "512"]) with { MaxMemoryBytes = 128L * 1024 * 1024 }, TestContext.Current.CancellationToken);

        outcome.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task RutaRelativa_SeRechaza()
    {
        await Should.ThrowAsync<ArgumentException>(() => _runner.RunAsync(new ProcessSpec { FileName = "ffmpeg.exe", Arguments = [] }, TestContext.Current.CancellationToken));
    }

    private static ProcessSpec Spec(IReadOnlyList<string> arguments, Action<string>? onStdout = null)
        => new() { FileName = ProbePath, Arguments = arguments, OnStandardOutputLine = onStdout, Timeout = TimeSpan.FromSeconds(60) };

    private static async Task WaitUntilExitedAsync(int pid)
    {
        for (var i = 0; i < 50; i++)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                if (process.HasExited)
                {
                    return;
                }
            }
            catch (ArgumentException)
            {
                return; // Ya no existe.
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        throw new ShouldAssertException($"El nieto {pid} sigue vivo: el Job Object no lo mató.");
    }
}

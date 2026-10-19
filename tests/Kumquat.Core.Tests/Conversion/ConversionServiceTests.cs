// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;
using Kumquat.Core.Results;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kumquat.Core.Tests.Conversion;

public sealed class ConversionServiceTests : IDisposable
{
    private readonly TestDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public async Task ConvertAsync_EncadenaPasosYPublicaJuntoAlOrigen()
    {
        var input = _dir.Write("nota.txt", "hola");
        var converter = new FakeConverter("fake", available: true,
            new ConversionEdge(KnownFormats.Txt, KnownFormats.Markdown, "fake"),
            new ConversionEdge(KnownFormats.Markdown, KnownFormats.Html, "fake"));
        var service = CreateService(converter);
        var reported = new List<double>();

        var result = await service.ConvertAsync(new ConversionRequest(input, KnownFormats.Html), new SyncProgress(reported.Add), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
        result.Value.ShouldBe(Path.Combine(_dir.Path, "nota.html"));
        (await File.ReadAllTextAsync(result.Value, TestContext.Current.CancellationToken)).ShouldBe("hola>md>html");
        converter.Calls.Count.ShouldBe(2);
        reported.ShouldBe([0.25, 0.75, 1.0]);
        _dir.LeftoverStagingFiles().ShouldBeEmpty();
    }

    [Fact]
    public async Task ConvertAsync_NoSobrescribe_EligeNombreLibre()
    {
        var input = _dir.Write("nota.txt", "hola");
        _dir.Write("nota.md", "existente");
        var service = CreateService(new FakeConverter("fake", available: true, new ConversionEdge(KnownFormats.Txt, KnownFormats.Markdown, "fake")));

        var result = await service.ConvertAsync(new ConversionRequest(input, KnownFormats.Markdown), null, TestContext.Current.CancellationToken);

        result.Value.ShouldBe(Path.Combine(_dir.Path, "nota (2).md"));
        (await File.ReadAllTextAsync(Path.Combine(_dir.Path, "nota.md"), TestContext.Current.CancellationToken)).ShouldBe("existente");
    }

    [Fact]
    public async Task ConvertAsync_SinRuta_DevuelveError()
    {
        var input = _dir.Write("nota.txt", "hola");
        var service = CreateService(new FakeConverter("fake", available: true, new ConversionEdge(KnownFormats.Txt, KnownFormats.Markdown, "fake")));

        var result = await service.ConvertAsync(new ConversionRequest(input, KnownFormats.Pdf), null, TestContext.Current.CancellationToken);

        result.Error.ShouldNotBeNull().Code.ShouldBe("no-route");
    }

    [Fact]
    public async Task ConvertAsync_ArchivoInexistente_DevuelveError()
    {
        var service = CreateService(new FakeConverter("fake", available: true));

        var result = await service.ConvertAsync(new ConversionRequest(Path.Combine(_dir.Path, "no.txt"), KnownFormats.Pdf), null, TestContext.Current.CancellationToken);

        result.Error.ShouldNotBeNull().Code.ShouldBe("input-missing");
    }

    [Fact]
    public async Task ConvertAsync_FalloDelMotor_NoDejaArchivosAMedias()
    {
        var input = _dir.Write("nota.txt", "hola");
        var failing = new FakeConverter("roto", available: true, async (step, ct) =>
        {
            await File.WriteAllTextAsync(step.OutputPath, "basura parcial", ct);
            return new Error("engine-failed", "boom");
        }, new ConversionEdge(KnownFormats.Txt, KnownFormats.Markdown, "roto"));
        var service = CreateService(failing);

        var result = await service.ConvertAsync(new ConversionRequest(input, KnownFormats.Markdown), null, TestContext.Current.CancellationToken);

        result.Error.ShouldNotBeNull().Code.ShouldBe("engine-failed");
        File.Exists(Path.Combine(_dir.Path, "nota.md")).ShouldBeFalse();
        _dir.LeftoverStagingFiles().ShouldBeEmpty();
    }

    [Fact]
    public async Task ConvertAsync_MotorSinSalida_DevuelveError()
    {
        var input = _dir.Write("nota.txt", "hola");
        var silent = new FakeConverter("mudo", available: true, (_, _) => Task.FromResult(Result.Success), new ConversionEdge(KnownFormats.Txt, KnownFormats.Markdown, "mudo"));

        var result = await CreateService(silent).ConvertAsync(new ConversionRequest(input, KnownFormats.Markdown), null, TestContext.Current.CancellationToken);

        result.Error.ShouldNotBeNull().Code.ShouldBe("no-output");
    }

    [Fact]
    public async Task ConvertAsync_Cancelado_DevuelveCancelledYLimpia()
    {
        var input = _dir.Write("nota.txt", "hola");
        using var cts = new CancellationTokenSource();
        var slow = new FakeConverter("lento", available: true, async (step, ct) =>
        {
            await File.WriteAllTextAsync(step.OutputPath, "parcial", ct);
            await cts.CancelAsync();
            await Task.Delay(Timeout.Infinite, ct);
            return Result.Success;
        }, new ConversionEdge(KnownFormats.Txt, KnownFormats.Markdown, "lento"));

        var result = await CreateService(slow).ConvertAsync(new ConversionRequest(input, KnownFormats.Markdown), null, cts.Token);

        result.Error.ShouldBe(Error.Cancelled);
        _dir.LeftoverStagingFiles().ShouldBeEmpty();
    }

    [Fact]
    public async Task ConvertAsync_LosIntermediosVanAlWorkspaceYSeBorran()
    {
        var input = _dir.Write("nota.txt", "hola");
        var factory = new TestWorkspaceFactory(_dir.Path);
        var converter = new FakeConverter("fake", available: true,
            new ConversionEdge(KnownFormats.Txt, KnownFormats.Markdown, "fake"),
            new ConversionEdge(KnownFormats.Markdown, KnownFormats.Html, "fake"));
        var service = new ConversionService(ConversionGraph.FromConverters([converter]), [converter], factory, NullLogger<ConversionService>.Instance);

        await service.ConvertAsync(new ConversionRequest(input, KnownFormats.Html), null, TestContext.Current.CancellationToken);

        converter.Calls[0].OutputPath.ShouldStartWith(factory.LastCreated!);
        Directory.Exists(factory.LastCreated).ShouldBeFalse();
    }

    private ConversionService CreateService(params IConverter[] converters)
        => new(ConversionGraph.FromConverters(converters), converters, new TestWorkspaceFactory(_dir.Path), NullLogger<ConversionService>.Instance);

    /// <summary>IProgress síncrono: Progress&lt;T&gt; publica en el pool y haría el test no determinista.</summary>
    private sealed class SyncProgress(Action<double> onReport) : IProgress<double>
    {
        public void Report(double value) => onReport(value);
    }
}

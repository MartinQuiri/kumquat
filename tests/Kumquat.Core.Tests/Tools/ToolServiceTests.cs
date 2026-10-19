// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Formats;
using Kumquat.Core.Results;
using Kumquat.Core.Tools;

namespace Kumquat.Core.Tests.Tools;

public sealed class ToolServiceTests : IDisposable
{
    private readonly TestDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void ToolsFor_SoloLasQueSirvenParaTodosLosFormatos()
    {
        var service = new ToolService([
            new FakeTool("solo-txt", [KnownFormats.Txt]),
            new FakeTool("texto", [KnownFormats.Txt, KnownFormats.Markdown]),
            new FakeTool("ausente", [KnownFormats.Txt], available: false),
        ]);

        service.ToolsFor([KnownFormats.Txt]).Select(t => t.Id).ShouldBe(["solo-txt", "texto"]);
        service.ToolsFor([KnownFormats.Txt, KnownFormats.Markdown]).Select(t => t.Id).ShouldBe(["texto"]);
        service.ToolsFor([]).ShouldBeEmpty();
    }

    [Fact]
    public void IdDuplicado_Lanza()
    {
        Should.Throw<ArgumentException>(() => new ToolService([new FakeTool("x", [KnownFormats.Txt]), new FakeTool("x", [KnownFormats.Txt])]));
    }

    [Fact]
    public async Task Apply_EscribeConSufijoJuntoAlOriginal()
    {
        var input = _dir.Write("nota.txt", "hola");
        var service = new ToolService([new FakeTool("mayus", [KnownFormats.Txt])]);

        var result = await service.ApplyAsync(input, "mayus", null, TestContext.Current.CancellationToken);

        result.Value.ShouldBe(Path.Combine(_dir.Path, "nota (mayus).txt"));
        (await File.ReadAllTextAsync(result.Value!, TestContext.Current.CancellationToken)).ShouldBe("HOLA");
        (await File.ReadAllTextAsync(input, TestContext.Current.CancellationToken)).ShouldBe("hola");
        _dir.LeftoverStagingFiles().ShouldBeEmpty();
    }

    [Fact]
    public async Task Apply_DosVeces_NoSobrescribe()
    {
        var input = _dir.Write("nota.txt", "hola");
        var service = new ToolService([new FakeTool("mayus", [KnownFormats.Txt])]);

        await service.ApplyAsync(input, "mayus", null, TestContext.Current.CancellationToken);
        var second = await service.ApplyAsync(input, "mayus", null, TestContext.Current.CancellationToken);

        second.Value.ShouldBe(Path.Combine(_dir.Path, "nota (mayus) (2).txt"));
    }

    [Fact]
    public async Task Apply_FormatoNoAplicable_DevuelveError()
    {
        var input = _dir.Write("datos.csv", "a,b");
        var service = new ToolService([new FakeTool("mayus", [KnownFormats.Txt])]);

        var result = await service.ApplyAsync(input, "mayus", null, TestContext.Current.CancellationToken);

        result.Error.ShouldNotBeNull().Code.ShouldBe("tool-not-applicable");
    }

    [Fact]
    public async Task Apply_HerramientaInexistente_DevuelveError()
    {
        var input = _dir.Write("nota.txt", "hola");

        var result = await new ToolService([]).ApplyAsync(input, "nada", null, TestContext.Current.CancellationToken);

        result.Error.ShouldNotBeNull().Code.ShouldBe("tool-missing");
    }

    [Fact]
    public async Task CreateJob_EjecutaLaHerramienta()
    {
        var input = _dir.Write("nota.txt", "hola");
        var job = new ToolService([new FakeTool("mayus", [KnownFormats.Txt])]).CreateJob(input, "mayus");

        job.Description.ShouldBe("Mayus");
        var result = await job.Work(new Progress<double>(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    private sealed class FakeTool(string id, FileFormat[] formats, bool available = true) : ITool
    {
        public string Id => id;

        public string Label => char.ToUpperInvariant(id[0]) + id[1..];

        public string OutputSuffix => id;

        public bool IsAvailable => available;

        public bool AppliesTo(FileFormat format) => formats.Contains(format);

        public FileFormat OutputFormat(FileFormat input) => input;

        public async Task<Result> ApplyAsync(ToolStep step, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            var text = await File.ReadAllTextAsync(step.InputPath, cancellationToken);
            await File.WriteAllTextAsync(step.OutputPath, text.ToUpperInvariant(), cancellationToken);
            return Result.Success;
        }
    }
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Formats;
using Kumquat.Core.Previews;
using Kumquat.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NetVips;

namespace Kumquat.Engines.Tests.Integration;

public sealed class PreviewServiceTests
{
    private static readonly PreviewService Service = new(TestEngines.FfmpegRunner, TestEngines.Pdf, TestEngines.Workspaces, NullLogger<PreviewService>.Instance);

    [Fact]
    public async Task Pdf_MiniaturaPorPagina()
    {
        using var preview = await CreateAsync(KnownFormats.Pdf);
        preview.Kind.ShouldBe(PreviewKind.Pages);
        preview.PageCount.ShouldBe(2);
        preview.Pages.Count.ShouldBe(2);
        using var page = Image.NewFromFile(preview.Pages[0]);
        page.Width.ShouldBe(PreviewService.PageWidth);
    }

    [Fact]
    public async Task Video_TiraDeFotogramasYDuracion()
    {
        using var preview = await CreateAsync(KnownFormats.Mp4);
        preview.Kind.ShouldBe(PreviewKind.Video);
        preview.Duration.TotalSeconds.ShouldBe(Corpus.Duration.TotalSeconds, 0.2);
        preview.Image.ShouldNotBeNull();

        // Tira horizontal: 10 cuadros de 90 px de alto (4:3 → 120 px de ancho cada uno).
        using var strip = Image.NewFromFile(preview.Image);
        strip.Height.ShouldBe(90);
        strip.Width.ShouldBe(120 * PreviewService.FilmstripFrames);
    }

    [Theory]
    [InlineData("wav")]
    [InlineData("mp3")]
    public async Task Audio_FormaDeOnda(string formatId)
    {
        using var preview = await CreateAsync(KnownFormats.FindById(formatId)!);
        preview.Kind.ShouldBe(PreviewKind.Audio);
        preview.Duration.TotalSeconds.ShouldBe(Corpus.Duration.TotalSeconds, 0.2);
        using var wave = Image.NewFromFile(preview.Image!);
        (wave.Width, wave.Height).ShouldBe((1600, 240));
        wave.HasAlpha().ShouldBeTrue();
    }

    [Fact]
    public async Task Foto_MiniaturaPng()
    {
        using var preview = await CreateAsync(KnownFormats.Png);
        preview.Kind.ShouldBe(PreviewKind.Image);
        using var image = Image.NewFromFile(preview.Image!);
        image.Width.ShouldBe(Corpus.Width);
    }

    [Fact]
    public async Task SinVistaPrevia_ParaTextoYArchivosDañados()
    {
        using var text = await CreateAsync(KnownFormats.Txt);
        text.Kind.ShouldBe(PreviewKind.None);

        var broken = Path.Combine(Path.GetTempPath(), $"kumquat-roto-{Guid.NewGuid():N}.pdf");
        await File.WriteAllTextAsync(broken, "%PDF-1.7\nbasura", TestContext.Current.CancellationToken);
        try
        {
            using var preview = await Service.CreateAsync(broken, TestContext.Current.CancellationToken);
            preview.Kind.ShouldBe(PreviewKind.None);
        }
        finally
        {
            File.Delete(broken);
        }
    }

    [Fact]
    public async Task AlDesechar_SeBorranLasImagenes()
    {
        var preview = await CreateAsync(KnownFormats.Pdf);
        var page = preview.Pages[0];
        preview.Dispose();
        File.Exists(page).ShouldBeFalse();
    }

    private static async Task<FilePreview> CreateAsync(FileFormat format)
    {
        var sample = await Corpus.GetAsync(format);
        Assert.SkipWhen(sample is null, $"El entorno no puede generar una muestra {format.DisplayName}.");
        return await Service.CreateAsync(sample, TestContext.Current.CancellationToken);
    }
}

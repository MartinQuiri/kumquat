// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;
using Kumquat.Core.Tools;
using Kumquat.Engines.Vips;
using NetVips;

namespace Kumquat.Engines.Tests.Integration;

/// <summary>
/// Casos que la muestra pequeña del corpus no ejercita: fotos grandes (varias franjas de
/// procesamiento) y orientación EXIF de celular. Regresión de "out of order read".
/// </summary>
[Trait("Category", "Integration")]
public sealed class RealisticImageTests : IDisposable
{
    private const int Width = 2400;
    private const int Height = 1600;

    private readonly string _dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "kumquat-real", Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task Convertir_FotoDeCelularConOrientacionExif_SaleDerecha()
    {
        var photo = CreateJpeg("celular.jpg", orientation: 6);
        var edge = TestEngines.Vips.Edges.Single(e => e.From == KnownFormats.Jpg && e.To == KnownFormats.Png);
        var output = Path.Combine(_dir, "celular.png");

        var result = await TestEngines.Vips.ConvertAsync(new ConversionStep(photo, output, edge), null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
        using var image = Image.NewFromFile(output);
        (image.Width, image.Height).ShouldBe((Height, Width), "La orientación EXIF 6 debe aplicarse a los píxeles (foto vertical).");
    }

    [Theory]
    [InlineData("image.rotate", Height, Width)]
    [InlineData("image.half", Width / 2, Height / 2)]
    [InlineData("image.compress", Width, Height)]
    [InlineData("image.strip", Width, Height)]
    public async Task Herramientas_FotoGrande(string toolId, int expectedWidth, int expectedHeight)
    {
        var photo = CreateJpeg("grande.jpg", orientation: 1);
        var service = new ToolService(VipsTool.CreateAll());

        var result = await service.ApplyAsync(photo, toolId, null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
        using var image = Image.NewFromFile(result.Value!);
        (image.Width, image.Height).ShouldBe((expectedWidth, expectedHeight));
    }

    private string CreateJpeg(string name, int orientation)
    {
        var path = Path.Combine(_dir, name);
        using var gradient = Image.Xyz(Width, Height);
        using var red = gradient[0] * (255.0 / Width);
        using var green = gradient[1] * (255.0 / Height);
        using var blue = (gradient[0] * 0) + 128;
        using var bands = red.Bandjoin(green, blue);
        using var rgb = bands.Cast(Enums.BandFormat.Uchar).Copy(interpretation: Enums.Interpretation.Srgb);
        using var tagged = rgb.Mutate(m => m.Set(GValue.GIntType, "orientation", orientation));
        tagged.Jpegsave(path, q: 90);
        return path;
    }
}

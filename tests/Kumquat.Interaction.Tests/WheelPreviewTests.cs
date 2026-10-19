// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Drawing;
using System.Drawing.Imaging;
using Kumquat.Core.Formats;
using Kumquat.Interaction.Wheel;

namespace Kumquat.Interaction.Tests;

/// <summary>
/// Renderiza la rueda a PNG: sirve como prueba de humo del renderizador y genera las
/// imágenes de docs/images (se regeneran al correr el test).
/// </summary>
public sealed class WheelPreviewTests
{
    private static readonly WheelRing[] Rings =
    [
        new(WheelRingKind.Convert, WheelItemOrdering.Arrange(FormatCategory.Image, [KnownFormats.Jpg, KnownFormats.WebP, KnownFormats.Avif, KnownFormats.Gif, KnownFormats.Tiff, KnownFormats.Bmp])),
        new(WheelRingKind.Tools, [WheelItem.Tool("image.compress", "Comprimir"), WheelItem.Tool("image.rotate", "Rotar 90°"), WheelItem.Tool("image.metadata", "Metadatos"), WheelItem.Tool("image.strip", "Sin metadatos")]),
        new(WheelRingKind.Actions, [WheelItem.Action("action.copy-path", "Copiar ruta"), WheelItem.Action("action.copy-to", "Copiar a…"), WheelItem.Action("action.move-to", "Mover a…"), WheelItem.Action("action.show-in-folder", "Ver en carpeta"), WheelItem.Action("action.terminal", "Terminal aquí")]),
    ];

    [Fact]
    public void Render_PdfConDiezHerramientas()
    {
        const float scale = 2f;
        var size = WheelRenderer.CanvasSize(scale);
        var height = WheelRenderer.CanvasHeight(scale);
        (string Id, string Label)[] tools =
        [
            ("pdf.merge", "Unir PDF"), ("pdf.compress", "Comprimir"), ("pdf.rotate", "Rotar 90°"), ("pdf.split", "Dividir páginas"), ("pdf.to-jpg", "Páginas a JPG"),
            ("pdf.extract", "Extraer páginas"), ("pdf.protect", "Proteger"), ("pdf.unlock", "Desbloquear"), ("pdf.metadata", "Metadatos"), ("pdf.ocr", "Reconocer texto"),
            ("archive.zip", "Crear ZIP"),
        ];
        var state = new WheelVisualState(
        [
            new(WheelRingKind.Convert, [WheelItem.Convert(KnownFormats.Txt)]),
            new(WheelRingKind.Tools, [.. tools.Select(t => WheelItem.Tool(t.Id, t.Label))]),
            Rings[2],
        ], WheelHit.At(1, 8), null)
        { Caption = "PDF · 2,4 MB" };

        using var bitmap = new Bitmap(size, height, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            WheelRenderer.Render(g, size, scale, state, WheelTheme.Dark);
        }

        bitmap.Save(Path.Combine(RepoRoot(), "docs", "images", "rueda-pdf.png"), ImageFormat.Png);
    }

    [Theory]
    [InlineData("rueda-oscura", false, 1, 0)]
    [InlineData("rueda-clara", true, 0, 0)]
    public void Render_TresAnillos(string name, bool light, int ring, int sector)
    {
        const float scale = 2f;
        var size = WheelRenderer.CanvasSize(scale);
        var height = WheelRenderer.CanvasHeight(scale);
        var state = new WheelVisualState(Rings, WheelHit.At(ring, sector), null) { Caption = "PNG · 820 KB" };

        using var bitmap = new Bitmap(size, height, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            WheelRenderer.Render(g, size, scale, state, light ? WheelTheme.Light : WheelTheme.Dark);
        }

        // El centro es transparente fuera del disco y opaco dentro.
        bitmap.GetPixel(1, 1).A.ShouldBe((byte)0);
        bitmap.GetPixel(size / 2, size / 2).A.ShouldBeGreaterThan((byte)200);

        // La etiqueta queda pegada bajo el disco, centrada, dentro del lienzo.
        bitmap.GetPixel(size / 2, height - (int)(10 * scale)).A.ShouldBeGreaterThan((byte)200);
        bitmap.GetPixel(size / 4, height - (int)(10 * scale)).A.ShouldBe((byte)0);

        var dir = Path.Combine(RepoRoot(), "docs", "images");
        Directory.CreateDirectory(dir);
        bitmap.Save(Path.Combine(dir, name + ".png"), ImageFormat.Png);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Kumquat.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? Path.GetTempPath();
    }
}

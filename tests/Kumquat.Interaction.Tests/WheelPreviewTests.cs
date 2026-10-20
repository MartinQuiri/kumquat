// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using Kumquat.Core.Formats;
using Kumquat.Core.Localization;
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

        SavePng(bitmap, Path.Combine(RepoRoot(), "docs", "images", "rueda-pdf.png"));
    }

    [Fact]
    public void AltoContraste_SoloLosColoresDelSistema()
    {
        const float scale = 1f;
        var size = WheelRenderer.CanvasSize(scale);
        var height = WheelRenderer.CanvasHeight(scale);
        var highlight = Color.FromArgb(255, 0x1A, 0xEB, 0xFF);
        var theme = WheelTheme.HighContrast(Color.Black, Color.White, highlight, Color.Black);
        var state = new WheelVisualState(Rings, WheelHit.At(1, 0), null) { Caption = "PNG · 820 KB" };

        using var bitmap = new Bitmap(size, height, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            WheelRenderer.Render(g, size, scale, state, theme);
        }

        var highlighted = 0;
        var brand = 0;
        var kumquat = WheelTheme.Kumquat;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var p = bitmap.GetPixel(x, y);
                highlighted += p.ToArgb() == highlight.ToArgb() ? 1 : 0;
                // El logo del centro es una imagen y puede conservar sus colores.
                var inLogo = Math.Pow(x - (size / 2.0), 2) + Math.Pow(y - (size / 2.0), 2) < Math.Pow(WheelRenderer.CenterRadiusDip * scale, 2);
                brand += !inLogo && Math.Abs(p.R - kumquat.R) + Math.Abs(p.G - kumquat.G) + Math.Abs(p.B - kumquat.B) < 30 && p.A > 200 ? 1 : 0;
            }
        }

        highlighted.ShouldBeGreaterThan(500, "el sector resaltado usa el color de selección del sistema");
        brand.ShouldBe(0, "en alto contraste no van los colores de la marca");
        bitmap.GetPixel(size / 2, size / 2).A.ShouldBe((byte)255, "todo opaco, sin vidrio");
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
        SavePng(bitmap, Path.Combine(dir, name + ".png"));
    }

    /// <summary>La rueda con la interfaz en inglés, para la imagen social y el README en inglés.</summary>
    [Fact]
    public void Render_EnIngles()
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        try
        {
            WheelRing[] rings =
            [
                Rings[0],
                new(WheelRingKind.Tools, [WheelItem.Tool("image.compress", Strings.ToolCompress), WheelItem.Tool("image.rotate", Strings.ToolRotate90), WheelItem.Tool("image.metadata", Strings.ToolMetadata), WheelItem.Tool("image.strip", Strings.ToolStripMetadata)]),
                new(WheelRingKind.Actions, [WheelItem.Action("action.copy-path", Strings.ActionCopyPath), WheelItem.Action("action.copy-to", Strings.ActionCopyTo), WheelItem.Action("action.move-to", Strings.ActionMoveTo), WheelItem.Action("action.show-in-folder", Strings.ActionShowInFolder), WheelItem.Action("action.terminal", Strings.ActionTerminal)]),
            ];
            rings[1].Items[0].Label.ShouldBe("Compress");

            const float scale = 2f;
            var size = WheelRenderer.CanvasSize(scale);
            var height = WheelRenderer.CanvasHeight(scale);
            var state = new WheelVisualState(rings, WheelHit.At(1, 0), null) { Caption = "PNG · 820 KB" };

            using var bitmap = new Bitmap(size, height, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                WheelRenderer.Render(g, size, scale, state, WheelTheme.Dark);
            }

            SavePng(bitmap, Path.Combine(RepoRoot(), "docs", "images", "rueda-oscura-en.png"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    /// <summary>
    /// Escribe la imagen de docs/images. Se codifica en memoria y se reintenta un momento si el
    /// archivo está abierto (un antivirus o un visor recién lo leyó): GDI+ fallaría con un error genérico.
    /// </summary>
    private static void SavePng(Bitmap bitmap, string path)
    {
        using var memory = new MemoryStream();
        bitmap.Save(memory, ImageFormat.Png);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.WriteAllBytes(path, memory.ToArray());
                return;
            }
            catch (IOException) when (attempt < 5)
            {
                Thread.Sleep(200);
            }
        }
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

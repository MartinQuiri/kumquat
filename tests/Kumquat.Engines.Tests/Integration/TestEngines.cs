// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Conversion;
using Kumquat.Engines.FFmpeg;
using Kumquat.Engines.Pdf;
using Kumquat.Engines.Vips;
using Kumquat.Infrastructure.Engines;
using Kumquat.Infrastructure.Files;
using Kumquat.Infrastructure.Processes;

namespace Kumquat.Engines.Tests.Integration;

/// <summary>Motores reales compartidos por los tests de integración.</summary>
internal static class TestEngines
{
    public static readonly ProcessRunner Runner = new();
    public static readonly EngineLocator Locator = new();
    public static readonly FfmpegRunner FfmpegRunner = new(Runner, Locator);
    public static readonly FfmpegConverter Ffmpeg = new(FfmpegRunner);
    public static readonly VipsConverter Vips = new();
    public static readonly TempWorkspaceFactory Workspaces = new(TimeProvider.System);
    public static readonly PdfEngines Pdf = new(Runner, Locator);
    public static readonly ImagePdfBuilder ImagePdf = new(Pdf, Workspaces);
    public static readonly MupdfConverter Mupdf = new(Pdf, ImagePdf);
    public static readonly Kumquat.Engines.Ocr.TesseractEngine Tesseract = new(Runner, Locator);
    public static readonly Kumquat.Engines.Office.OfficeConverter Office = new(Kumquat.Engines.Office.OfficeApps.Detect(), Runner, Workspaces);
    /// <summary>Calibre instalado, o el de <c>KUMQUAT_TEST_CALIBRE</c> (ruta a ebook-convert.exe de una copia extraída).</summary>
    public static readonly Kumquat.Engines.Calibre.CalibreConverter Calibre = new(
        Kumquat.Engines.Calibre.CalibreConverter.Find() ?? (Environment.GetEnvironmentVariable("KUMQUAT_TEST_CALIBRE") is { } path && File.Exists(path) ? path : null),
        Runner,
        Workspaces);

    public static readonly IReadOnlyList<IConverter> All = [Ffmpeg, Vips, Mupdf, Office, Calibre];

    public static string? FfmpegExe => Locator.Find("ffmpeg", Path.Combine("bin", "ffmpeg.exe"));

    public static string? FfprobeExe => Locator.Find("ffmpeg", Path.Combine("bin", "ffprobe.exe"));

    public static IConverter ById(string id) => All.Single(c => c.Id == id);
}

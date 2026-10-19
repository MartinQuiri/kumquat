// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Conversion;
using Kumquat.Engines.FFmpeg;
using Kumquat.Engines.Vips;
using Kumquat.Infrastructure.Engines;
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
    public static readonly IReadOnlyList<IConverter> All = [Ffmpeg, Vips];

    public static string? FfmpegExe => Locator.Find("ffmpeg", Path.Combine("bin", "ffmpeg.exe"));

    public static string? FfprobeExe => Locator.Find("ffmpeg", Path.Combine("bin", "ffprobe.exe"));

    public static IConverter ById(string id) => All.Single(c => c.Id == id);
}

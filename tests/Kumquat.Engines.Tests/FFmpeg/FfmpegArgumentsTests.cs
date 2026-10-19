// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Formats;
using Kumquat.Engines.FFmpeg;

namespace Kumquat.Engines.Tests.FFmpeg;

public sealed class FfmpegArgumentsTests
{
    [Theory]
    [InlineData(@"C:\videos\-y.mp4")]
    [InlineData(@"C:\videos\concat:a|b.mp4")]
    [InlineData(@"C:\videos\http:\x.mp4")]
    public void Rutas_SiempreConPrefijoFile_NuncaComoOpcion(string input)
    {
        var args = FfmpegArguments.Build(input, @"C:\out\o.mp3", KnownFormats.Mp3);

        var i = args.ToList().IndexOf("-i");
        args[i + 1].ShouldBe("file:" + Path.GetFullPath(input));
        args[^1].ShouldBe(@"file:C:\out\o.mp3");
        args.Where(a => !a.StartsWith("file:", StringComparison.Ordinal)).ShouldNotContain(input);
    }

    [Fact]
    public void Entrada_RestringeProtocolos()
    {
        var args = FfmpegArguments.Build(@"C:\a.mp4", @"C:\b.mp3", KnownFormats.Mp3).ToList();

        args[args.IndexOf("-protocol_whitelist") + 1].ShouldBe("file,pipe");
        args.IndexOf("-protocol_whitelist").ShouldBeLessThan(args.IndexOf("-i"));
        args.ShouldContain("-nostdin");
    }

    [Fact]
    public void FormatoSinPerfil_Lanza()
    {
        Should.Throw<ArgumentException>(() => FfmpegArguments.Build(@"C:\a.mp4", @"C:\b.pdf", KnownFormats.Pdf));
    }

    [Fact]
    public void Progreso_ReportaFraccionMonotonaYFinal()
    {
        var values = new List<double>();
        var parser = new FfmpegProgressParser(TimeSpan.FromSeconds(10), values.Add);

        foreach (var line in new[] { "frame=1", "out_time_us=1000000", "out_time_us=1001000", "out_time_us=5000000", "bitrate=N/A", "out_time_us=-5", "progress=continue", "out_time_us=12000000", "progress=end" })
        {
            parser.OnLine(line);
        }

        values.ShouldBe([0.1, 0.5, 0.999, 1.0]);
    }

    [Fact]
    public void Progreso_SinDuracion_SoloReportaElFinal()
    {
        var values = new List<double>();
        var parser = new FfmpegProgressParser(null, values.Add);

        parser.OnLine("out_time_us=1000000");
        parser.OnLine("progress=end");

        values.ShouldBe([1.0]);
    }

    [Theory]
    [InlineData("12.345000", 12.345)]
    [InlineData(" 3\r\n", 3.0)]
    [InlineData("N/A", null)]
    [InlineData("", null)]
    [InlineData("-1", null)]
    [InlineData("inf", null)]
    public void ParseDuration(string text, double? expectedSeconds)
    {
        FfmpegProgressParser.ParseDuration(text)?.TotalSeconds.ShouldBe(expectedSeconds!.Value, tolerance: 1e-6);
        (FfmpegProgressParser.ParseDuration(text) is null).ShouldBe(expectedSeconds is null);
    }
}

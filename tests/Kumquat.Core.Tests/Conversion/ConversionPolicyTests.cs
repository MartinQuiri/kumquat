// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;

namespace Kumquat.Core.Tests.Conversion;

public sealed class ConversionPolicyTests
{
    [Theory]
    [InlineData("png", "jpg", true)]
    [InlineData("png", "pdf", true)]
    [InlineData("png", "mp3", false)]
    [InlineData("png", "mp4", false)]
    [InlineData("gif", "mp4", true)]
    [InlineData("mp4", "gif", true)]
    [InlineData("mp4", "mp3", true)]
    [InlineData("mp3", "mp4", false)]
    [InlineData("mp3", "png", false)]
    [InlineData("pdf", "docx", true)]
    [InlineData("zip", "pdf", false)]
    public void Destinos(string from, string to, bool expected)
    {
        ConversionPolicy.IsMeaningful(KnownFormats.FindById(from)!, KnownFormats.FindById(to)!).ShouldBe(expected);
    }

    [Fact]
    public void ElGrafoNoOfreceDestinosSinSentidoAunqueExistaRuta()
    {
        var graph = new ConversionGraph([
            new ConversionEdge(KnownFormats.Png, KnownFormats.Gif, "vips"),
            new ConversionEdge(KnownFormats.Gif, KnownFormats.Mp4, "ffmpeg"),
            new ConversionEdge(KnownFormats.Mp4, KnownFormats.Mp3, "ffmpeg"),
        ]);

        graph.Targets(KnownFormats.Png).Select(p => p.To).ShouldBe([KnownFormats.Gif]);
        graph.FindPath(KnownFormats.Png, KnownFormats.Mp3).ShouldBeNull();
        graph.Targets(KnownFormats.Gif).Select(p => p.To).ShouldBe([KnownFormats.Mp4]);
    }

    [Fact]
    public void TodaFamiliaTienePolitica()
    {
        foreach (var category in Enum.GetValues<FormatCategory>())
        {
            var sample = KnownFormats.All.First(f => f.Category == category);
            ConversionPolicy.IsMeaningful(sample, sample).ShouldBeTrue($"{category} no tiene política");
        }
    }
}

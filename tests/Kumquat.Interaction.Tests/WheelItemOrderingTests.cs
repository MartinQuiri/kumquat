// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Formats;
using Kumquat.Interaction.Wheel;

namespace Kumquat.Interaction.Tests;

public sealed class WheelItemOrderingTests
{
    [Fact]
    public void Imagen_JpgArribaYPreferidosPrimero()
    {
        var items = WheelItemOrdering.Arrange(FormatCategory.Image, [KnownFormats.Bmp, KnownFormats.WebP, KnownFormats.Jpg, KnownFormats.Gif]);

        items.Select(i => i.Id).ShouldBe(["jpg", "webp", "gif", "bmp"]);
    }

    [Fact]
    public void ConOchoDestinos_LosCuatroFavoritosVanALosEjes()
    {
        var targets = new[] { "mp4", "mp3", "gif", "webm", "mov", "mkv", "m4a", "avi", "wav" }.Select(id => KnownFormats.FindById(id)!).ToList();

        var items = WheelItemOrdering.Arrange(FormatCategory.Video, targets);

        items.Count.ShouldBe(8);
        items[0].Id.ShouldBe("mp4"); // norte
        items[2].Id.ShouldBe("mp3"); // este
        items[4].Id.ShouldBe("gif"); // sur
        items[6].Id.ShouldBe("webm"); // oeste
        items.Select(i => i.Id).ShouldNotContain("wav"); // noveno: queda fuera
    }

    [Fact]
    public void FormatosNoPreferidos_VanAlFinalEnOrdenEstable()
    {
        var items = WheelItemOrdering.Arrange(FormatCategory.Audio, [KnownFormats.Zip, KnownFormats.Mp3, KnownFormats.Pdf]);

        items.Select(i => i.Id).ShouldBe(["mp3", "pdf", "zip"]);
    }

    [Fact]
    public void SinDestinos_RuedaVacia()
    {
        WheelItemOrdering.Arrange(FormatCategory.Image, []).ShouldBeEmpty();
    }
}

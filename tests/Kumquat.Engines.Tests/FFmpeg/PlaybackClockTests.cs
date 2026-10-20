// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Formats;
using Kumquat.Engines.FFmpeg;
using Kumquat.Engines.FFmpeg.Preview;

namespace Kumquat.Engines.Tests.FFmpeg;

public sealed class PlaybackClockTests
{
    [Fact]
    public async Task SinAudio_NoAvanzaHastaElPrimerFotograma()
    {
        // Lo que FFmpeg tarda en arrancar no cuenta: si contara, la reproducción empezaría
        // descartando por "atrasados" los primeros fotogramas y saltaría hacia adelante.
        var from = TimeSpan.FromSeconds(2);
        var clock = new PlaybackClock(from, audio: null);

        await Task.Delay(150, TestContext.Current.CancellationToken);
        clock.Now.ShouldBe(from);

        clock.Start();
        await Task.Delay(150, TestContext.Current.CancellationToken);
        clock.Now.ShouldBeGreaterThan(from + TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task Start_DosVeces_NoReiniciaElReloj()
    {
        var clock = new PlaybackClock(TimeSpan.Zero, audio: null);
        clock.Start();
        await Task.Delay(120, TestContext.Current.CancellationToken);
        var before = clock.Now;

        clock.Start();

        clock.Now.ShouldBeGreaterThanOrEqualTo(before);
    }

    [Fact]
    public void WebM_UsaUnCodecQueTraeEsteFfmpeg()
    {
        // El FFmpeg de ARM64 viene sin libvpx: ahí WebM va en AV1 (SVT-AV1).
        var codec = FfmpegProfiles.For(KnownFormats.WebM)!.SkipWhile(a => !string.Equals(a, "-c:v", StringComparison.Ordinal)).ElementAt(1);

        codec.ShouldBe(FfmpegProfiles.HasVp9 ? "libvpx-vp9" : "libsvtav1");
        FfmpegProfiles.For(KnownFormats.WebM)!.Contains("-row-mt").ShouldBe(FfmpegProfiles.HasVp9);
    }
}

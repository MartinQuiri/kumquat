// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Collections.Concurrent;
using System.Diagnostics;
using Kumquat.Core.Formats;
using Kumquat.Core.Previews;
using Kumquat.Engines.FFmpeg.Preview;

namespace Kumquat.Engines.Tests.Integration;

public sealed class FfmpegPlaybackTests
{
    [Fact]
    public void Recuadro_ConservaProporcionYEsPar()
    {
        FfmpegPlayback.Fit(1920, 1080).ShouldBe((960, 540));
        FfmpegPlayback.Fit(1080, 1920).ShouldBe((304, 540));
        FfmpegPlayback.Fit(320, 240).ShouldBe((320, 240)); // nunca agranda
        FfmpegPlayback.Fit(333, 201).ShouldBe((334, 202));
    }

    [Fact]
    public async Task Fotograma_EsExactoYDistintoSegunElInstante()
    {
        var playback = await PlaybackForAsync(KnownFormats.Mp4);
        playback.FrameSize.ShouldBe((Corpus.Width, Corpus.Height));

        using var early = await playback.FrameAtAsync(TimeSpan.FromSeconds(0.2), TestContext.Current.CancellationToken);
        using var late = await playback.FrameAtAsync(TimeSpan.FromSeconds(1.6), TestContext.Current.CancellationToken);
        early.ShouldNotBeNull();
        late.ShouldNotBeNull();
        (early.Width, early.Height).ShouldBe((Corpus.Width, Corpus.Height));

        // testsrc2 se mueve: dos instantes distintos dan imágenes distintas, y no negras.
        early.Pixels.SequenceEqual(late.Pixels).ShouldBeFalse();
        early.Pixels.ToArray().Count(b => b > 40).ShouldBeGreaterThan(early.Pixels.Length / 4);
    }

    [Fact]
    public async Task Reproducir_EnTiempoReal_ConFotogramasYPosicion()
    {
        var playback = await PlaybackForAsync(KnownFormats.Mp4);
        var frames = new ConcurrentQueue<TimeSpan>();
        var positions = new ConcurrentQueue<TimeSpan>();
        var watch = Stopwatch.StartNew();

        await playback.PlayAsync(
            TimeSpan.FromSeconds(0.5),
            TimeSpan.FromSeconds(1.5),
            frame =>
            {
                frames.Enqueue(frame.Time);
                frame.Dispose();
            },
            positions.Enqueue,
            TestContext.Current.CancellationToken);

        // Un segundo de medio dura ~1 s (en tiempo real, no más rápido) y trae hasta ~25 fotogramas.
        // Los atrasados se descartan a propósito: en una máquina lenta sin GPU (los runners de la
        // CI) llegan pocos, así que sólo se exige que llegue alguno y que no sobren.
        watch.Elapsed.TotalSeconds.ShouldBeInRange(0.8, 4);
        frames.Count.ShouldBeInRange(1, 26);
        frames.ShouldBe(frames.Order());
        frames.First().TotalSeconds.ShouldBe(0.5, 0.05);
        positions.Last().ShouldBe(TimeSpan.FromSeconds(1.5));
    }

    [Fact]
    public async Task Pausar_CortaEnSeco()
    {
        var playback = await PlaybackForAsync(KnownFormats.Mp4);
        using var pause = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));
        var watch = Stopwatch.StartNew();

        await playback.PlayAsync(TimeSpan.Zero, Corpus.Duration, f => f.Dispose(), _ => { }, pause.Token);

        watch.Elapsed.TotalSeconds.ShouldBeLessThan(1.5);
    }

    [Theory]
    [InlineData("mp3")]
    [InlineData("ogg")]
    [InlineData("flac")]
    public async Task SoloAudio_SinFotogramas_YConPosicion(string formatId)
    {
        var playback = await PlaybackForAsync(KnownFormats.FindById(formatId)!);
        playback.FrameSize.ShouldBe((0, 0));
        (await playback.FrameAtAsync(TimeSpan.Zero, TestContext.Current.CancellationToken)).ShouldBeNull();

        var positions = new ConcurrentQueue<TimeSpan>();
        await playback.PlayAsync(TimeSpan.Zero, TimeSpan.FromSeconds(0.6), f => f.Dispose(), positions.Enqueue, TestContext.Current.CancellationToken);
        positions.Last().ShouldBe(TimeSpan.FromSeconds(0.6));
    }

    private static async Task<IMediaPlayback> PlaybackForAsync(FileFormat format)
    {
        var sample = await Corpus.GetAsync(format);
        Assert.SkipWhen(sample is null, $"El entorno no puede generar una muestra {format.DisplayName}.");
        var info = await TestEngines.FfmpegRunner.ProbeAsync(sample, TestContext.Current.CancellationToken);
        info.ShouldNotBeNull();
        return new FfmpegPlayback(TestEngines.FfmpegRunner, sample, info);
    }
}

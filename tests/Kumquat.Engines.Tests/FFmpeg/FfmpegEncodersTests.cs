// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Formats;
using Kumquat.Engines.FFmpeg;
using Kumquat.Infrastructure.Engines;
using Kumquat.Infrastructure.Processes;

namespace Kumquat.Engines.Tests.FFmpeg;

/// <summary>
/// Fuera de Windows el FFmpeg puede ser el del sistema y no traer todo: lo que no puede codificar no
/// se ofrece (ni la conversión ni la herramienta).
/// </summary>
public sealed class FfmpegEncodersTests : IDisposable
{
    /// <summary>Recorte real de <c>ffmpeg -hide_banner -encoders</c>, con la leyenda que hay que ignorar.</summary>
    private const string EncodersOutput = """
        Encoders:
         V..... = Video
         A..... = Audio
         S..... = Subtitle
         .F.... = Frame-level multithreading
         ------
         V....D libx264              libx264 H.264 / AVC / MPEG-4 AVC / MPEG-4 part 10 (codec h264)
         A....D aac                  AAC (Advanced Audio Coding)
         A....D flac                 FLAC (Free Lossless Audio Codec)
         A....D libmp3lame           libmp3lame MP3 (MPEG audio layer 3) (codec mp3)
         A....D libvorbis            libvorbis (codec vorbis)
         A..X.D vorbis               Vorbis
        """;

    private readonly string _root = Directory.CreateTempSubdirectory("kumquat-encoders-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void SalidaDeEncoders_SeLeenLosNombresSinLaLeyenda()
    {
        FfmpegRunner.ParseEncoders(EncodersOutput).ShouldBe(["libx264", "aac", "flac", "libmp3lame", "libvorbis", "vorbis"], ignoreOrder: true);
    }

    [Fact]
    public void Perfiles_DeclaranSusCodificadores()
    {
        FfmpegProfiles.Encoders(FfmpegProfiles.For(KnownFormats.Ogg)!).ShouldBe(["libvorbis"]);
        FfmpegProfiles.Encoders(FfmpegProfiles.For(KnownFormats.Mp4)!).ShouldBe(["libx264", "aac"]);
        FfmpegProfiles.Encoders(["-map", "0:v", "-c", "copy", "-an"]).ShouldBeEmpty("copiar no codifica");
    }

    [Fact]
    public void SinLibvorbis_NoSeOfreceOgg()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "En Windows se usa siempre el FFmpeg propio, completo: no se consulta.");
        var ffmpeg = RunnerWith(EncodersOutput.Replace("libvorbis", "otro", StringComparison.Ordinal));
        var converter = new FfmpegConverter(ffmpeg);
        var normalize = FfmpegTool.CreateAll(ffmpeg).Single(t => t.Id == "audio.normalize");

        ffmpeg.CanEncode(KnownFormats.Ogg).ShouldBeFalse();
        converter.Edges.ShouldNotContain(e => e.To == KnownFormats.Ogg);
        converter.Edges.ShouldContain(e => e.From == KnownFormats.Wav && e.To == KnownFormats.Mp3);
        normalize.AppliesTo(KnownFormats.Ogg).ShouldBeFalse("normalizar un OGG lo vuelve a codificar en OGG");
        normalize.AppliesTo(KnownFormats.Mp3).ShouldBeTrue();
    }

    [Fact]
    public void FfmpegQueNoResponde_NoFiltraNada()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "En Windows no se consulta.");
        var ffmpeg = RunnerWith(output: null);

        ffmpeg.CanEncode(KnownFormats.Ogg).ShouldBeTrue();
    }

    /// <summary>Un FFmpeg falso en <c>engines/</c> cuyo <c>-encoders</c> devuelve <paramref name="output"/> (o falla si es <c>null</c>).</summary>
    private FfmpegRunner RunnerWith(string? output)
    {
        var bin = Directory.CreateDirectory(Path.Combine(_root, "engines", "ffmpeg", "bin")).FullName;
        File.WriteAllText(Path.Combine(bin, "ffmpeg" + EngineLocator.ExecutableExtension), string.Empty);
        return new FfmpegRunner(new FakeRunner(output), new EngineLocator(_root, overrideRoot: string.Empty, searchPath: string.Empty));
    }

    private sealed class FakeRunner(string? output) : IProcessRunner
    {
        public Task<ProcessOutcome> RunAsync(ProcessSpec spec, CancellationToken cancellationToken)
        {
            if (output is null)
            {
                return Task.FromResult(new ProcessOutcome(1, "falló", TimedOut: false));
            }

            foreach (var line in output.Split('\n'))
            {
                spec.OnStandardOutputLine?.Invoke(line.TrimEnd('\r'));
            }

            return Task.FromResult(new ProcessOutcome(0, string.Empty, TimedOut: false));
        }
    }
}

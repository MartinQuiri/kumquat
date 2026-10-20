// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Text;
using System.Text.RegularExpressions;
using Kumquat.Core.Formats;
using Kumquat.Core.Localization;
using Kumquat.Core.Results;
using Kumquat.Core.Text;
using Kumquat.Infrastructure.Engines;
using Kumquat.Infrastructure.Processes;

namespace Kumquat.Engines.FFmpeg;

/// <summary>
/// Ejecuta <c>ffmpeg.exe</c> de forma segura (rutas <c>file:</c>, lista blanca de protocolos,
/// Job Object) con progreso real basado en la duración que informa <c>ffprobe</c>.
/// Lo comparten el convertidor y las herramientas de audio/video.
/// </summary>
public sealed partial class FfmpegRunner
{
    private static readonly TimeSpan RunTimeout = TimeSpan.FromHours(6);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(30);
    private const long MaxMemoryBytes = 4L * 1024 * 1024 * 1024;

    private readonly IProcessRunner _runner;
    private readonly string? _ffmpeg;
    private readonly string? _ffprobe;

    /// <summary>Codificadores de este FFmpeg, o <c>null</c> si no se consultaron (se asume que están todos).</summary>
    private readonly HashSet<string>? _encoders;

    /// <summary>Crea el ejecutor localizando los binarios.</summary>
    public FfmpegRunner(IProcessRunner runner, EngineLocator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _ffmpeg = locator.FindExecutable("ffmpeg", "ffmpeg", "bin");
        _ffprobe = locator.FindExecutable("ffmpeg", "ffprobe", "bin");

        // Fuera de Windows el FFmpeg puede ser el del sistema, y los de los gestores de paquetes no traen
        // todos los codificadores (el de Homebrew no tiene libvorbis): lo que no puede codificar no se ofrece. El de Windows
        // es siempre el propio, fijado y completo.
        _encoders = _ffmpeg is null || OperatingSystem.IsWindows() ? null : ReadEncoders(runner, _ffmpeg);
    }

    /// <summary>Indica si FFmpeg está instalado.</summary>
    public bool IsAvailable => _ffmpeg is not null;

    /// <summary>Indica si este FFmpeg tiene los codificadores que piden unos argumentos de salida.</summary>
    public bool CanEncode(IReadOnlyList<string> outputArguments)
        => _encoders is null || FfmpegProfiles.Encoders(outputArguments).All(_encoders.Contains);

    /// <summary>Indica si este FFmpeg puede generar el formato (sin perfil propio, lo resuelve FFmpeg).</summary>
    public bool CanEncode(FileFormat target)
        => FfmpegProfiles.For(target) is not { } arguments || CanEncode(arguments);

    /// <summary>Nombres de la salida de <c>ffmpeg -encoders</c> (líneas como " A....D libvorbis  …").</summary>
    internal static HashSet<string> ParseEncoders(string output)
        => [.. EncoderLine().Matches(output).Select(match => match.Groups["name"].Value)];

    /// <summary>Al arrancar, una sola vez. Si FFmpeg no responde, no se filtra nada.</summary>
    private static HashSet<string>? ReadEncoders(IProcessRunner runner, string ffmpeg)
    {
        var output = new StringBuilder();
        var outcome = runner.RunAsync(
            new ProcessSpec { FileName = ffmpeg, Arguments = ["-hide_banner", "-encoders"], Timeout = ProbeTimeout, OnStandardOutputLine = line => output.AppendLine(line) },
            CancellationToken.None).GetAwaiter().GetResult();
        return outcome.Succeeded ? ParseEncoders(output.ToString()) : null;
    }

    [GeneratedRegex(@"^ [VAS][.F][.S][.X][.B][.D] (?<name>[^\s=]\S*)", RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EncoderLine();

    /// <summary>Convierte <paramref name="input"/> en <paramref name="output"/> con los argumentos de salida dados.</summary>
    public async Task<Result> RunAsync(string input, IReadOnlyList<string> outputArguments, string output, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var duration = progress is null || _ffmpeg is null ? null : await ProbeDurationAsync(input, cancellationToken);
        return await ExecuteAsync(FfmpegArguments.Build(input, output, outputArguments), duration, progress, cancellationToken);
    }

    /// <summary>
    /// Convierte un tramo: <c>-ss</c> antes de la entrada salta directo al instante (y al
    /// recodificar el corte es exacto); el progreso se mide sobre la duración del tramo.
    /// </summary>
    public Task<Result> RunSegmentAsync(string input, TimeSpan start, TimeSpan? length, IReadOnlyList<string> outputArguments, string output, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        List<string> inputOptions = ["-ss", TimeText.ToSeconds(start)];
        if (length is { } l)
        {
            inputOptions.AddRange(["-t", TimeText.ToSeconds(l)]);
        }

        return ExecuteAsync(FfmpegArguments.Build(input, output, outputArguments, inputOptions), length, progress, cancellationToken);
    }

    /// <summary>Ejecuta con varias entradas (unir); el progreso usa la duración total dada.</summary>
    public Task<Result> RunMultiAsync(IReadOnlyList<string> inputs, IReadOnlyList<string> outputArguments, string output, TimeSpan? totalDuration, IProgress<double>? progress, CancellationToken cancellationToken)
        => ExecuteAsync(FfmpegArguments.BuildMulti(inputs, output, outputArguments), totalDuration, progress, cancellationToken);

    /// <summary>
    /// Ejecuta FFmpeg leyendo su salida binaria (fotogramas o audio sin comprimir). Con prioridad
    /// normal: es para reproducir en vivo. <c>false</c> si FFmpeg no está o terminó con error.
    /// </summary>
    internal async Task<bool> StreamAsync(IReadOnlyList<string> arguments, Func<Stream, CancellationToken, Task> reader, CancellationToken cancellationToken)
    {
        if (_ffmpeg is null)
        {
            return false;
        }

        var outcome = await _runner.RunAsync(
            new ProcessSpec { FileName = _ffmpeg, Arguments = arguments, MaxMemoryBytes = MaxMemoryBytes, StandardOutputReader = reader, Interactive = true },
            cancellationToken);
        return outcome.Succeeded;
    }

    /// <summary>Lee duración, resolución y pistas de un medio.</summary>
    public async Task<MediaInfo?> ProbeAsync(string input, CancellationToken cancellationToken)
    {
        if (_ffprobe is null)
        {
            return null;
        }

        var output = new StringBuilder();
        var outcome = await _runner.RunAsync(
            new ProcessSpec
            {
                FileName = _ffprobe,
                Arguments = FfmpegArguments.ProbeStreams(input),
                Timeout = ProbeTimeout,
                MaxMemoryBytes = MaxMemoryBytes,
                OnStandardOutputLine = line => output.AppendLine(line),
            },
            cancellationToken);
        return outcome.Succeeded ? MediaInfo.Parse(output.ToString()) : null;
    }

    /// <summary>Etiquetas del contenedor (título, artista...); las claves se comparan sin distinguir mayúsculas.</summary>
    public async Task<IReadOnlyDictionary<string, string>> ProbeTagsAsync(string input, CancellationToken cancellationToken)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (_ffprobe is null)
        {
            return tags;
        }

        var output = new StringBuilder();
        var outcome = await _runner.RunAsync(
            new ProcessSpec
            {
                FileName = _ffprobe,
                Arguments = FfmpegArguments.ProbeTags(input),
                Timeout = ProbeTimeout,
                MaxMemoryBytes = MaxMemoryBytes,
                OnStandardOutputLine = line => output.AppendLine(line),
            },
            cancellationToken);
        if (!outcome.Succeeded)
        {
            return tags;
        }

        try
        {
            // OGG/Opus guardan las etiquetas en la pista de audio; el resto, en el contenedor (que manda).
            using var doc = System.Text.Json.JsonDocument.Parse(output.ToString());
            if (doc.RootElement.TryGetProperty("streams", out var streams))
            {
                foreach (var stream in streams.EnumerateArray().Where(s => s.TryGetProperty("tags", out _)))
                {
                    foreach (var tag in stream.GetProperty("tags").EnumerateObject())
                    {
                        tags[tag.Name] = tag.Value.GetString() ?? string.Empty;
                    }
                }
            }

            if (doc.RootElement.TryGetProperty("format", out var format) && format.TryGetProperty("tags", out var values))
            {
                foreach (var tag in values.EnumerateObject())
                {
                    tags[tag.Name] = tag.Value.GetString() ?? string.Empty;
                }
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // Sin etiquetas legibles: se edita desde cero.
        }

        return tags;
    }

    private async Task<Result> ExecuteAsync(IReadOnlyList<string> arguments, TimeSpan? duration, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (_ffmpeg is null)
        {
            return new Error("engine-missing", Strings.ErrorEngineMissing("FFmpeg"));
        }

        var parser = new FfmpegProgressParser(duration, p => progress?.Report(p));

        var outcome = await _runner.RunAsync(
            new ProcessSpec
            {
                FileName = _ffmpeg,
                Arguments = arguments,
                Timeout = RunTimeout,
                MaxMemoryBytes = MaxMemoryBytes,
                OnStandardOutputLine = parser.OnLine,
            },
            cancellationToken);

        if (outcome.TimedOut)
        {
            return new Error("engine-timeout", Strings.ErrorEngineTimeout("FFmpeg"));
        }

        return outcome.Succeeded
            ? Result.Success
            : new Error("engine-failed", Strings.ErrorEngineExit("FFmpeg", outcome.ExitCode, LastLines(outcome.StandardErrorTail, 3)));
    }

    private static string LastLines(string text, int count)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" | ", lines.Skip(Math.Max(0, lines.Length - count)));
    }

    private async Task<TimeSpan?> ProbeDurationAsync(string input, CancellationToken cancellationToken)
    {
        if (_ffprobe is null)
        {
            return null;
        }

        var output = new StringBuilder();
        var outcome = await _runner.RunAsync(
            new ProcessSpec
            {
                FileName = _ffprobe,
                Arguments = FfmpegArguments.ProbeDuration(input),
                Timeout = ProbeTimeout,
                MaxMemoryBytes = MaxMemoryBytes,
                OnStandardOutputLine = line => output.AppendLine(line),
            },
            cancellationToken);
        return outcome.Succeeded ? FfmpegProgressParser.ParseDuration(output.ToString()) : null;
    }
}

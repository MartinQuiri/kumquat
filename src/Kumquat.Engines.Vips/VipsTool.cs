// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Formats;
using Kumquat.Core.Results;
using Kumquat.Core.Tools;
using NetVips;

namespace Kumquat.Engines.Vips;

/// <summary>Herramienta de imagen con libvips: una transformación más opciones de guardado.</summary>
public sealed class VipsTool : ITool
{
    /// <summary>Formatos que libvips lee y escribe sin pérdida de capacidades (sin animación).</summary>
    private static readonly FileFormat[] EditableImages = [KnownFormats.Jpg, KnownFormats.Png, KnownFormats.WebP, KnownFormats.Tiff, KnownFormats.Avif];

    private readonly Func<Image, Image> _transform;
    private readonly bool _needsRandomAccess;
    private readonly Func<FileFormat, VOption> _saveOptions;

    private VipsTool(string id, string label, string suffix, Func<Image, Image> transform, Func<FileFormat, VOption> saveOptions, bool needsRandomAccess = false)
    {
        _needsRandomAccess = needsRandomAccess;
        Id = id;
        Label = label;
        OutputSuffix = suffix;
        _transform = transform;
        _saveOptions = saveOptions;
    }

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public string Label { get; }

    /// <inheritdoc />
    public string OutputSuffix { get; }

    /// <inheritdoc />
    public bool IsAvailable => VipsConverter.IsLibraryAvailable;

    /// <summary>Herramientas de imagen, en el orden en que aparecen en la rueda.</summary>
    public static IReadOnlyList<ITool> CreateAll() =>
    [
        new VipsTool("image.compress", "Comprimir", "comprimido", image => image.Copy(), CompressOptions),
        new VipsTool("image.rotate", "Rotar 90°", "rotado", image => image.Rot90(), QualityOptions, needsRandomAccess: true),
        new VipsTool("image.half", "Reducir 50%", "50%", image => image.Resize(0.5), QualityOptions),
        new VipsTool("image.strip", "Sin metadatos", "sin metadatos", image => image.Copy(), StripOptions),
    ];

    /// <inheritdoc />
    public bool AppliesTo(FileFormat format) => EditableImages.Contains(format);

    /// <inheritdoc />
    public FileFormat OutputFormat(FileFormat input) => input;

    /// <inheritdoc />
    public Task<Result> ApplyAsync(ToolStep step, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using var oriented = VipsLoader.OpenOriented(step.InputPath, _needsRandomAccess);
                    using var result = _transform(oriented);
                    result.SetProgress(new PercentProgress(progress), cancellationToken);
                    result.WriteToFile(step.OutputPath, _saveOptions(step.OutputFormat));
                    cancellationToken.ThrowIfCancellationRequested();
                    return Result.Success;
                }
                catch (VipsException ex)
                {
                    return new Error("engine-failed", $"libvips: {ex.Message.Trim()}");
                }
            },
            cancellationToken);
    }

    /// <summary>Compresión con pérdida razonable: lo que haría un usuario para mandar por correo.</summary>
    private static VOption CompressOptions(FileFormat format) => format.Id switch
    {
        "jpg" => new VOption { { "Q", 75 }, { "optimize_coding", true }, { "interlace", true }, { "keep", Enums.ForeignKeep.Icc } },
        "png" => new VOption { { "palette", true }, { "Q", 80 }, { "effort", 7 }, { "compression", 9 } },
        "webp" => new VOption { { "Q", 70 }, { "effort", 5 }, { "keep", Enums.ForeignKeep.Icc } },
        "avif" => new VOption { { "Q", 45 }, { "effort", 5 }, { "compression", Enums.ForeignHeifCompression.Av1 } },
        "tiff" => new VOption { { "compression", Enums.ForeignTiffCompression.Jpeg }, { "Q", 80 } },
        _ => [],
    };

    /// <summary>Alta calidad: para transformaciones que no buscan achicar el archivo.</summary>
    private static VOption QualityOptions(FileFormat format) => format.Id switch
    {
        "jpg" => new VOption { { "Q", 92 } },
        "webp" => new VOption { { "Q", 92 } },
        "avif" => new VOption { { "Q", 70 }, { "compression", Enums.ForeignHeifCompression.Av1 } },
        "tiff" => new VOption { { "compression", Enums.ForeignTiffCompression.Deflate } },
        _ => [],
    };

    private static VOption StripOptions(FileFormat format)
    {
        var options = QualityOptions(format);
        options.Add("keep", Enums.ForeignKeep.None);
        return options;
    }

    private sealed class PercentProgress(IProgress<double>? inner) : IProgress<int>
    {
        public void Report(int value) => inner?.Report(value / 100.0);
    }
}

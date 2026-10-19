// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;
using Kumquat.Core.Results;
using NetVips;

namespace Kumquat.Engines.Vips;

/// <summary>
/// Convertidor de imágenes basado en libvips (vía NetVips): rápido, con poca memoria
/// (procesa por franjas) y con buen soporte de HEIC/AVIF/WebP.
/// </summary>
/// <remarks>
/// libvips corre dentro del proceso. Es una excepción consciente a la regla de "motores
/// fuera de proceso": es una librería madura y fuzzeada (OSS-Fuzz). Si aparece un CVE
/// serio, se mueve a un worker aparte sin cambiar esta interfaz.
/// </remarks>
public sealed class VipsConverter : IConverter
{
    /// <summary>Identificador del convertidor.</summary>
    public const string ConverterId = "vips";

    private static readonly FileFormat[] LosslessSources = [KnownFormats.Png, KnownFormats.Tiff, KnownFormats.Bmp, KnownFormats.Gif];
    private static readonly FileFormat[] Animated = [KnownFormats.Gif, KnownFormats.WebP];
    private static readonly Lazy<bool> LibraryAvailable = new(TryInitialize);

    /// <summary>Crea el convertidor y detecta qué cargadores y guardadores trae libvips.</summary>
    public VipsConverter()
    {
        IsAvailable = IsLibraryAvailable;
        Edges = IsAvailable ? BuildEdges() : [];
    }

    /// <summary>Indica si libvips cargó (se inicializa una sola vez y desactiva su caché).</summary>
    public static bool IsLibraryAvailable => LibraryAvailable.Value;

    /// <inheritdoc />
    public string Id => ConverterId;

    /// <inheritdoc />
    public bool IsAvailable { get; }

    /// <inheritdoc />
    public IReadOnlyCollection<ConversionEdge> Edges { get; }

    /// <inheritdoc />
    public Task<Result> ConvertAsync(ConversionStep step, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);

        // libvips es síncrono; se corre en el pool para no bloquear al llamador.
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    Convert(step, progress, cancellationToken);
                    return Result.Success;
                }
                catch (VipsException ex)
                {
                    return new Error("engine-failed", $"libvips: {ex.Message.Trim()}");
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
            },
            cancellationToken);
    }

    private static void Convert(ConversionStep step, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var from = step.Edge.From;
        var to = step.Edge.To;
        var keepAnimation = Animated.Contains(from) && Animated.Contains(to);

        // Orientación EXIF aplicada a los píxeles: si no, la foto sale "acostada" al quitar metadatos.
        using var rotated = VipsLoader.OpenOriented(
            step.InputPath,
            needsRandomAccess: false,
            options: keepAnimation ? new VOption { { "n", -1 } } : null,
            applyOrientation: !keepAnimation);
        using var prepared = to == KnownFormats.Jpg && rotated.HasAlpha() ? rotated.Flatten(background: [255, 255, 255]) : rotated.Copy();

        // NetVips mata el pipeline de libvips cuando el token se cancela.
        prepared.SetProgress(new PercentProgress(progress), cancellationToken);

        prepared.WriteToFile(step.OutputPath, SaveOptions(from, to));
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static VOption SaveOptions(FileFormat from, FileFormat to)
    {
        var lossless = LosslessSources.Contains(from);
        return to.Id switch
        {
            "jpg" => new VOption { { "Q", 90 }, { "optimize_coding", true }, { "interlace", true } },
            "png" => new VOption { { "compression", 6 } },
            "webp" => lossless ? new VOption { { "lossless", true }, { "effort", 4 } } : new VOption { { "Q", 90 }, { "effort", 4 } },
            "avif" => new VOption { { "Q", 65 }, { "effort", 4 }, { "compression", Enums.ForeignHeifCompression.Av1 } },
            "tiff" => new VOption { { "compression", Enums.ForeignTiffCompression.Deflate } },
            "gif" => new VOption { { "effort", 7 } },
            _ => [],
        };
    }

    private static bool TryInitialize()
    {
        try
        {
            if (!ModuleInitializer.VipsInitialized)
            {
                return false;
            }

            // Sin caché de operaciones: libvips mantendría abiertos los archivos del usuario
            // (no se podrían borrar ni mover tras convertir) y la memoria crecería sin control.
            Cache.Max = 0;
            Cache.MaxFiles = 0;
            Cache.MaxMem = 0;
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (TypeInitializationException)
        {
            return false;
        }
    }

    private static bool HasOperation(string name)
    {
        try
        {
            using var op = Operation.NewFromName(name);
            return true;
        }
        catch (VipsException)
        {
            return false;
        }
    }

    private static List<ConversionEdge> BuildEdges()
    {
        var loaders = new (FileFormat Format, string Operation)[]
        {
            (KnownFormats.Jpg, "jpegload"), (KnownFormats.Png, "pngload"), (KnownFormats.WebP, "webpload"),
            (KnownFormats.Tiff, "tiffload"), (KnownFormats.Gif, "gifload"), (KnownFormats.Heic, "heifload"),
            (KnownFormats.Avif, "heifload"), (KnownFormats.Svg, "svgload"),
        }.Where(l => HasOperation(l.Operation)).Select(l => l.Format).ToList();

        var savers = new (FileFormat Format, string Operation)[]
        {
            (KnownFormats.Jpg, "jpegsave"), (KnownFormats.Png, "pngsave"), (KnownFormats.WebP, "webpsave"),
            (KnownFormats.Tiff, "tiffsave"), (KnownFormats.Gif, "gifsave"), (KnownFormats.Avif, "heifsave"),
        }.Where(s => HasOperation(s.Operation)).Select(s => s.Format).ToList();

        return [.. from source in loaders
                   from target in savers
                   where source != target
                   select new ConversionEdge(source, target, ConverterId, Cost(target))];
    }

    private static double Cost(FileFormat target) => target == KnownFormats.Gif ? 2 : target == KnownFormats.Avif ? 1.5 : 1;

    /// <summary>Adaptador síncrono 0..100 → 0..1 (Progress&lt;T&gt; publicaría en otro hilo, fuera de orden).</summary>
    private sealed class PercentProgress(IProgress<double>? inner) : IProgress<int>
    {
        public void Report(int value) => inner?.Report(value / 100.0);
    }
}

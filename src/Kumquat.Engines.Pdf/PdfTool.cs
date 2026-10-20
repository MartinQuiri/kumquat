// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;
using Kumquat.Core.Localization;
using Kumquat.Core.Results;
using Kumquat.Core.Tools;
using Kumquat.Engines.Vips;

namespace Kumquat.Engines.Pdf;

/// <summary>Herramientas de PDF (qpdf + MuPDF) y "unir imágenes en un PDF".</summary>
public sealed class PdfTool : ITool
{
    private static readonly FileFormat[] PdfOnly = [KnownFormats.Pdf];

    private readonly FileFormat[] _appliesTo;
    private readonly FileFormat? _output;
    private readonly Func<bool> _available;
    private readonly Func<ToolStep, CancellationToken, Task<Result>> _apply;

    private PdfTool(string id, string label, string suffix, FileFormat[] appliesTo, FileFormat? output, Func<bool> available, Func<ToolStep, CancellationToken, Task<Result>> apply, ToolMode mode = ToolMode.EachFile, int minInputs = 1)
    {
        Id = id;
        Label = label;
        OutputSuffix = suffix;
        _appliesTo = appliesTo;
        _output = output;
        _available = available;
        _apply = apply;
        Mode = mode;
        MinInputs = minInputs;
    }

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public string Label { get; }

    /// <inheritdoc />
    public string OutputSuffix { get; }

    /// <inheritdoc />
    public bool IsAvailable => _available();

    /// <inheritdoc />
    public ToolMode Mode { get; }

    /// <inheritdoc />
    public int MinInputs { get; }

    /// <summary>Herramientas en el orden de la rueda.</summary>
    public static IReadOnlyList<ITool> CreateAll(PdfEngines engines, ImagePdfBuilder images, ITempWorkspaceFactory workspaces)
    {
        ArgumentNullException.ThrowIfNull(engines);
        ArgumentNullException.ThrowIfNull(images);
        var secure = new PdfSecureTools(engines, workspaces ?? throw new ArgumentNullException(nameof(workspaces)));
        bool HasMutool() => engines.Mutool is not null;
        bool HasQpdf() => engines.Qpdf is not null;

        // Con un PDF protegido, estas piden la contraseña y lo abren solas (sin desbloquearlo antes).
        // Unir (varios archivos, cada uno con su clave) y Metadatos (lee los actuales antes de
        // preguntar nada) siguen pidiendo desbloquearlo primero; Desbloquear ya pide la contraseña.
        ITool WithPassword(PdfTool tool) => tool.Id switch
        {
            "pdf.compress" or "pdf.rotate" or "pdf.split" or "pdf.extract" => tool.OpeningProtected(engines, secure, PdfPasswordHandling.Keep),
            "pdf.to-jpg" => tool.OpeningProtected(engines, secure, PdfPasswordHandling.Drop),
            "pdf.protect" => tool.OpeningProtected(engines, secure, PdfPasswordHandling.Replace),
            _ => tool,
        };

        PdfTool[] tools =
        [
            // Unir: varios PDF en el orden natural de sus nombres.
            new PdfTool("pdf.merge", "Unir PDF", "unido", PdfOnly, KnownFormats.Pdf, HasQpdf,
                (step, ct) => engines.RunQpdfAsync(["--empty", "--pages", .. step.Inputs.Select(i => PdfEngines.Abs(i.Path)), "--", PdfEngines.Abs(step.OutputPath)], ct),
                ToolMode.Combine, minInputs: 2),

            // Comprimir: reduce imágenes de más de 225 ppp a 150 ppp (JPEG calidad 70), compacta
            // objetos y comprime flujos. Es lo que hace "comprimir PDF" en los servicios web.
            new PdfTool("pdf.compress", Strings.ToolCompress, Strings.SuffixCompressed, PdfOnly, KnownFormats.Pdf, HasMutool,
                (step, ct) => engines.RunMutoolAsync(
                [
                    "clean", "-gggg", "-z", "-f", "-i", "-Z",
                    "--color-image-subsample-method", "bicubic", "--color-image-subsample-dpi", "225,150", "--color-image-recompress-method", "jpeg:70",
                    "--gray-image-subsample-method", "bicubic", "--gray-image-subsample-dpi", "225,150", "--gray-image-recompress-method", "jpeg:70",
                    PdfEngines.Abs(step.InputPath), PdfEngines.Abs(step.OutputPath),
                ], ct)),

            new PdfTool("pdf.rotate", Strings.ToolRotate90, Strings.SuffixRotated, PdfOnly, KnownFormats.Pdf, HasQpdf,
                (step, ct) => engines.RunQpdfAsync([PdfEngines.Abs(step.InputPath), PdfEngines.Abs(step.OutputPath), "--rotate=+90"], ct)),

            // Dividir: una carpeta con un PDF por página (informe-01.pdf, informe-02.pdf...).
            new PdfTool("pdf.split", Strings.ToolSplitPages, Strings.SuffixPages, PdfOnly, null, HasQpdf,
                (step, ct) => engines.RunQpdfAsync(["--split-pages", PdfEngines.Abs(step.InputPath), Path.Combine(step.OutputPath, Path.GetFileNameWithoutExtension(step.InputPath) + ".pdf")], ct)),

            // Páginas a JPG: MuPDF renderiza PNG a 150 ppp y libvips los pasa a JPEG calidad 90.
            new PdfTool("pdf.to-jpg", Strings.ToolPagesToJpg, "JPG", PdfOnly, null, () => HasMutool() && VipsConverter.IsLibraryAvailable,
                (step, ct) => PagesToJpegAsync(engines, step, ct)),

            .. CreateWithParameters(engines, secure),

            // Unir imágenes en un PDF (una por página).
            new PdfTool("image.to-pdf", Strings.ToolImagesToPdf, "PDF", ImagePdfBuilder.Inputs, KnownFormats.Pdf, () => images.IsAvailable,
                (step, ct) => images.BuildAsync([.. step.Inputs.Select(i => (i.Path, i.Format!))], step.OutputPath, ct),
                ToolMode.Combine, minInputs: 2),
        ];
        return [.. tools.Select(WithPassword)];
    }

    /// <summary>Herramientas que piden datos antes de ejecutarse (páginas, contraseña, metadatos).</summary>
    private static PdfTool[] CreateWithParameters(PdfEngines engines, PdfSecureTools secure)
    {
        bool HasMutool() => engines.Mutool is not null;
        bool HasQpdf() => engines.Qpdf is not null;

        return
        [
            // Extraer páginas: un PDF nuevo solo con las páginas indicadas.
            new PdfTool("pdf.extract", Strings.ToolExtractPages, Strings.SuffixExtract, PdfOnly, KnownFormats.Pdf, HasQpdf, secure.ExtractAsync)
            {
                ParameterSource = (_, _) => Task.FromResult(PdfSecureTools.ExtractParameters()),
                Validator = PdfSecureTools.ValidateExtract,
            },

            new PdfTool("pdf.protect", Strings.ToolProtect, Strings.SuffixProtected, PdfOnly, KnownFormats.Pdf, HasQpdf, secure.ProtectAsync)
            {
                ParameterSource = (_, _) => Task.FromResult(PdfSecureTools.ProtectParameters()),
                Validator = PdfSecureTools.ValidateProtect,
            },

            new PdfTool("pdf.unlock", Strings.ToolUnlock, Strings.SuffixUnlocked, PdfOnly, KnownFormats.Pdf, HasQpdf, secure.UnlockAsync)
            {
                ParameterSource = (_, _) => Task.FromResult(PdfSecureTools.UnlockParameters()),
                Validator = PdfSecureTools.ValidateUnlock,
            },

            new PdfTool("pdf.metadata", Strings.ToolMetadata, Strings.SuffixMetadata, PdfOnly, KnownFormats.Pdf, HasMutool, secure.WriteMetadataAsync)
            {
                ParameterSource = (inputs, ct) => secure.MetadataParametersAsync(inputs[0].Path, ct),
                Validator = PdfSecureTools.ValidateMetadata,
            },
        ];
    }

    /// <summary>
    /// La misma herramienta, pero si el PDF pide contraseña para abrirse la agrega a lo que pregunta
    /// y lo abre con ella (<see cref="PdfSecureTools.WithOpenPasswordAsync"/>). Solo para herramientas
    /// de un archivo por vez.
    /// </summary>
    private PdfTool OpeningProtected(PdfEngines engines, PdfSecureTools secure, PdfPasswordHandling handling)
    {
        var parameters = ParameterSource;
        var validate = Validator;
        var apply = _apply;
        return new PdfTool(Id, Label, OutputSuffix, _appliesTo, _output, _available, RunAsync, Mode, MinInputs)
        {
            ParameterSource = AskAsync,
            Validator = options => PdfSecureTools.ValidateOpenPassword(options) ?? validate?.Invoke(options),
        };

        async Task<IReadOnlyList<ToolParameter>> AskAsync(IReadOnlyList<ToolInput> inputs, CancellationToken cancellationToken)
        {
            var own = parameters is null ? [] : await parameters(inputs, cancellationToken);
            foreach (var input in inputs)
            {
                if (await engines.RequiresPasswordAsync(input.Path, cancellationToken))
                {
                    return [PdfSecureTools.OpenPasswordParameter(handling), .. own];
                }
            }

            return own;
        }

        // Con varios PDF, cada uno se procesa por separado: la contraseña solo se usa en los que la piden.
        async Task<Result> RunAsync(ToolStep step, CancellationToken cancellationToken)
            => step.Option(PdfSecureTools.OpenPasswordKey) is { Length: > 0 } password && await engines.RequiresPasswordAsync(step.InputPath, cancellationToken)
                ? await secure.WithOpenPasswordAsync(step, password, handling, apply, cancellationToken)
                : await apply(step, cancellationToken);
    }

    /// <inheritdoc />
    public bool AppliesTo(FileFormat format) => _appliesTo.Contains(format);

    /// <inheritdoc />
    public FileFormat? OutputFormat(FileFormat? input) => _output;

    /// <summary>Origen de los parámetros a pedir (si la herramienta los necesita).</summary>
    private Func<IReadOnlyList<ToolInput>, CancellationToken, Task<IReadOnlyList<ToolParameter>>>? ParameterSource { get; init; }

    /// <summary>Validación de las opciones elegidas.</summary>
    private Func<IReadOnlyDictionary<string, string>, string?>? Validator { get; init; }

    /// <inheritdoc />
    public Task<IReadOnlyList<ToolParameter>> GetParametersAsync(IReadOnlyList<ToolInput> inputs, CancellationToken cancellationToken)
        => ParameterSource?.Invoke(inputs, cancellationToken) ?? Task.FromResult<IReadOnlyList<ToolParameter>>([]);

    /// <inheritdoc />
    public string? Validate(IReadOnlyDictionary<string, string> options) => Validator?.Invoke(options);

    /// <inheritdoc />
    public Task<Result> ApplyAsync(ToolStep step, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);
        return _apply(step, cancellationToken);
    }

    private static async Task<Result> PagesToJpegAsync(PdfEngines engines, ToolStep step, CancellationToken cancellationToken)
    {
        var stem = Path.GetFileNameWithoutExtension(step.InputPath);
        var pattern = Path.Combine(step.OutputPath, $"{stem}-%03d.png");
        var rendered = await engines.RunMutoolAsync(["draw", "-r", "150", "-o", pattern, PdfEngines.Abs(step.InputPath)], cancellationToken);
        if (!rendered.IsSuccess)
        {
            return rendered;
        }

        foreach (var png in Directory.EnumerateFiles(step.OutputPath, "*.png").ToList())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Run(() => VipsImaging.PngToJpeg(png, Path.ChangeExtension(png, ".jpg"), quality: 90), cancellationToken);
            File.Delete(png);
        }

        return Result.Success;
    }
}

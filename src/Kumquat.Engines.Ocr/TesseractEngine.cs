// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Globalization;
using Kumquat.Core.Localization;
using Kumquat.Core.Results;
using Kumquat.Infrastructure.Engines;
using Kumquat.Infrastructure.Processes;

namespace Kumquat.Engines.Ocr;

/// <summary>
/// Ejecuta Tesseract. Solo recibe rutas locales que crea Kumquat (Tesseract sabría leer URLs: nunca
/// se le pasa una) y los modelos de idioma de la carpeta propia, nunca los del sistema.
/// </summary>
public sealed class TesseractEngine
{
    /// <summary>Idiomas del reconocimiento: español e inglés a la vez (sirve para textos mezclados).</summary>
    public const string Languages = "spa+eng";

    /// <summary>Resolución con la que se dibujan las páginas de un PDF y se supone en fotos sin dato.</summary>
    public const int DefaultDpi = 300;

    private static readonly TimeSpan PageTimeout = TimeSpan.FromMinutes(5);
    private const long MaxMemoryBytes = 2L * 1024 * 1024 * 1024;

    private readonly IProcessRunner _runner;
    private readonly string? _executable;
    private readonly string? _tessdata;

    /// <summary>Localiza el motor y sus modelos.</summary>
    public TesseractEngine(IProcessRunner runner, EngineLocator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _executable = locator.Find("tesseract", Path.Combine("bin", "tesseract.exe"));
        var tessdata = _executable is null ? null : Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(_executable)!)!, "tessdata");
        _tessdata = tessdata is not null && Languages.Split('+').All(l => File.Exists(Path.Combine(tessdata, l + ".traineddata"))) ? tessdata : null;
    }

    /// <summary>Indica si el motor y los modelos de <see cref="Languages"/> están instalados.</summary>
    public bool IsAvailable => _executable is not null && _tessdata is not null;

    /// <summary>
    /// Reconoce una imagen y escribe <c>{outputBase}.txt</c> o <c>{outputBase}.pdf</c> (imagen con
    /// el texto invisible encima: se puede seleccionar y buscar).
    /// </summary>
    /// <param name="image">Imagen (PNG, JPEG, TIFF, BMP, GIF o WebP).</param>
    /// <param name="outputBase">Ruta de salida sin extensión.</param>
    /// <param name="format"><c>txt</c> o <c>pdf</c>.</param>
    /// <param name="dpi">Resolución de la imagen (define el tamaño de página del PDF).</param>
    /// <param name="cancellationToken">Cancelación.</param>
    public async Task<Result> RecognizeAsync(string image, string outputBase, string format, int dpi, CancellationToken cancellationToken)
    {
        if (!IsAvailable)
        {
            return new Error("engine-missing", Strings.ErrorOcrMissing);
        }

        if (format is not ("txt" or "pdf"))
        {
            throw new ArgumentOutOfRangeException(nameof(format), format, "Formato de OCR no admitido.");
        }

        var outcome = await _runner.RunAsync(
            new ProcessSpec
            {
                FileName = _executable!,
                Arguments =
                [
                    Path.GetFullPath(image), Path.GetFullPath(outputBase),
                    "--tessdata-dir", _tessdata!,
                    "-l", Languages,
                    "--dpi", dpi.ToString(CultureInfo.InvariantCulture),
                    "--psm", "3", // segmentación automática de página (sin detección de orientación)
                    format,
                ],
                Timeout = PageTimeout,
                MaxMemoryBytes = MaxMemoryBytes,
            },
            cancellationToken);
        if (outcome.TimedOut)
        {
            return new Error("engine-timeout", Strings.ErrorEngineTimeout("Tesseract"));
        }

        var output = outputBase + "." + format;
        if (outcome.Succeeded && File.Exists(output))
        {
            return Result.Success;
        }

        var detail = outcome.StandardErrorTail.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
        return new Error("engine-failed", Strings.ErrorEngineExit("Tesseract", outcome.ExitCode, detail));
    }
}

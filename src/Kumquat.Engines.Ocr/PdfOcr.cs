// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Kumquat.Core.Conversion;
using Kumquat.Core.Results;
using Kumquat.Engines.Pdf;
using Kumquat.Engines.Vips;

namespace Kumquat.Engines.Ocr;

/// <summary>
/// PDF escaneado → PDF buscable. Solo se reconocen las páginas sin texto: las que ya lo tienen se
/// conservan originales (vectoriales, nítidas). Cada página escaneada se dibuja a 300 ppp, Tesseract
/// la convierte en una página con la imagen y el texto invisible encima, y qpdf arma el documento
/// final reemplazando solo esas páginas.
/// </summary>
internal sealed class PdfOcr(TesseractEngine tesseract, PdfEngines pdf, ITempWorkspaceFactory workspaces)
{
    /// <summary>Caracteres visibles a partir de los cuales una página "ya tiene texto".</summary>
    internal const int MinTextCharacters = 25;

    /// <summary>Páginas reconocidas a la vez (cada Tesseract usa un núcleo).</summary>
    private static readonly int Parallelism = Math.Clamp(Environment.ProcessorCount / 2, 1, 4);

    public async Task<Result> RunAsync(string input, string output, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        using var workspace = workspaces.Create();
        var source = Path.GetFullPath(input);
        var count = await PageCountAsync(source, cancellationToken);
        if (count <= 0)
        {
            return new Error("pdf-unreadable", "No se pudo leer el PDF (¿está protegido o dañado?).");
        }

        var text = await pdf.RunMutoolAsync(["draw", "-q", "-F", "txt", "-o", Path.Combine(workspace.DirectoryPath, "text-%d.txt"), source], cancellationToken);
        if (!text.IsSuccess)
        {
            return text;
        }

        var scanned = Enumerable.Range(1, count).Where(page => !HasText(TextFile(workspace, page))).ToList();
        if (scanned.Count == 0)
        {
            return new Error("already-searchable", "El PDF ya tiene texto en todas sus páginas: no hace falta OCR.");
        }

        var drawn = await pdf.RunMutoolAsync(
            ["draw", "-q", "-r", TesseractEngine.DefaultDpi.ToString(CultureInfo.InvariantCulture), "-o", Path.Combine(workspace.DirectoryPath, "page-%d.png"), source, string.Join(',', scanned)],
            cancellationToken);
        if (!drawn.IsSuccess)
        {
            return drawn;
        }

        progress?.Report(0.1);
        var recognized = await RecognizePagesAsync(workspace, scanned, progress, cancellationToken);
        if (!recognized.IsSuccess)
        {
            return recognized;
        }

        var arguments = MergeArguments(source, count, scanned.ToHashSet(), page => OcrPage(workspace, page), Path.GetFullPath(output));
        var argumentFile = workspace.NewFilePath(".args");
        await File.WriteAllLinesAsync(argumentFile, arguments, new UTF8Encoding(false), cancellationToken);
        var merged = await pdf.RunQpdfAsync(["@" + argumentFile], cancellationToken);
        progress?.Report(1);
        return merged;
    }

    /// <summary>
    /// Argumentos de qpdf: el original como base (conserva metadatos) y sus páginas en orden, salvo
    /// las escaneadas, que vienen de su PDF reconocido. Páginas originales seguidas van en un rango.
    /// </summary>
    internal static List<string> MergeArguments(string source, int pageCount, IReadOnlySet<int> scanned, Func<int, string> ocrPage, string output)
    {
        List<string> arguments = [source, "--pages"];
        var page = 1;
        while (page <= pageCount)
        {
            if (scanned.Contains(page))
            {
                arguments.AddRange([ocrPage(page), "1"]);
                page++;
                continue;
            }

            var start = page;
            while (page <= pageCount && !scanned.Contains(page))
            {
                page++;
            }

            arguments.AddRange([source, start == page - 1 ? Invariant(start) : $"{Invariant(start)}-{Invariant(page - 1)}"]);
        }

        arguments.AddRange(["--", output]);
        return arguments;
    }

    /// <summary>Indica si el texto extraído de una página es suficiente para no reconocerla.</summary>
    internal static bool HasText(string textFile)
        => File.Exists(textFile) && File.ReadAllText(textFile).Count(c => !char.IsWhiteSpace(c)) >= MinTextCharacters;

    private static string Invariant(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string TextFile(ITempWorkspace workspace, int page) => Path.Combine(workspace.DirectoryPath, $"text-{Invariant(page)}.txt");

    private static string OcrPage(ITempWorkspace workspace, int page) => Path.Combine(workspace.DirectoryPath, $"ocr-{Invariant(page)}.pdf");

    private async Task<Result> RecognizePagesAsync(ITempWorkspace workspace, List<int> pages, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var failures = new ConcurrentQueue<Error>();
        var done = 0;
        try
        {
            await Parallel.ForEachAsync(pages, new ParallelOptions { MaxDegreeOfParallelism = Parallelism, CancellationToken = stop.Token }, async (page, token) =>
            {
                // JPEG: la página escaneada pesa una fracción del PNG dentro del PDF final.
                var png = Path.Combine(workspace.DirectoryPath, $"page-{Invariant(page)}.png");
                var jpeg = Path.ChangeExtension(png, ".jpg");
                VipsImaging.PngToJpeg(png, jpeg, quality: 85);
                File.Delete(png);

                var result = await tesseract.RecognizeAsync(jpeg, Path.ChangeExtension(OcrPage(workspace, page), null), "pdf", TesseractEngine.DefaultDpi, token);
                if (!result.IsSuccess)
                {
                    failures.Enqueue(result.Error!);
                    await stop.CancelAsync(); // una página falló: no seguir con las demás
                    return;
                }

                progress?.Report(0.1 + (0.85 * Interlocked.Increment(ref done) / pages.Count));
            });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Cancelado por una página fallida: se informa ese error.
        }

        cancellationToken.ThrowIfCancellationRequested();
        return failures.TryDequeue(out var error) ? error : Result.Success;
    }

    private async Task<int> PageCountAsync(string source, CancellationToken cancellationToken)
    {
        var count = await pdf.RunMutoolForOutputAsync(["show", source, "trailer/Root/Pages/Count"], cancellationToken);
        return int.TryParse(count?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var pages) ? pages : 0;
    }
}

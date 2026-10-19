// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.ComponentModel;
using System.Globalization;
using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;
using Kumquat.Core.Previews;
using Kumquat.Engines.FFmpeg;
using Kumquat.Engines.Pdf;
using Kumquat.Engines.Vips;
using Microsoft.Extensions.Logging;
using NetVips;

namespace Kumquat.Hosting;

/// <summary>
/// Vistas previas para la ventana de opciones, generadas con los mismos motores locales:
/// miniaturas de páginas (MuPDF), tira de fotogramas y forma de onda (FFmpeg) y miniatura de
/// fotos (libvips). Todo va a una carpeta temporal propia de cada vista previa.
/// </summary>
public sealed partial class PreviewService(FfmpegRunner ffmpeg, PdfEngines pdf, ITempWorkspaceFactory workspaces, ILogger<PreviewService> logger) : IPreviewService
{
    /// <summary>Páginas que se dibujan como máximo (el resto se muestra sin miniatura).</summary>
    public const int MaxPages = 60;

    /// <summary>Ancho de cada miniatura de página, en píxeles (se muestran a la mitad en pantallas 2x).</summary>
    public const int PageWidth = 240;

    /// <summary>Fotogramas de la tira de un video.</summary>
    public const int FilmstripFrames = 10;

    /// <summary>Lado mayor de la miniatura de una foto.</summary>
    public const int ImageSize = 1400;

    /// <inheritdoc />
    public async Task<FilePreview> CreateAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        var format = Detect(path);
        if (format is null)
        {
            return FilePreview.None(path);
        }

        var workspace = workspaces.Create();
        try
        {
            var preview = format.Category switch
            {
                FormatCategory.Pdf => await PagesAsync(path, workspace, cancellationToken),
                FormatCategory.Image => await Task.Run(() => Photo(path, workspace), cancellationToken),
                FormatCategory.Video or FormatCategory.Audio => await MediaAsync(path, workspace, cancellationToken),
                _ => null,
            };
            if (preview is not null)
            {
                return preview;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or VipsException or Win32Exception or InvalidOperationException)
        {
            LogFailed(ex, path);
        }
        catch
        {
            workspace.Dispose();
            throw;
        }

        workspace.Dispose();
        return FilePreview.None(path);
    }

    private static FileFormat? Detect(string path)
    {
        try
        {
            return FormatSniffer.DetectFile(path);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static FilePreview? Photo(string path, ITempWorkspace workspace)
    {
        if (!VipsConverter.IsLibraryAvailable)
        {
            return null;
        }

        var output = workspace.NewFilePath(".png");
        VipsImaging.Thumbnail(path, output, ImageSize);
        return new FilePreview(PreviewKind.Image, path, workspace) { Image = output };
    }

    private async Task<FilePreview?> PagesAsync(string path, ITempWorkspace workspace, CancellationToken cancellationToken)
    {
        // Un PDF protegido sin contraseña no se puede leer: sin vista previa (la ventana sigue funcionando).
        var count = await pdf.RunMutoolForOutputAsync(["show", Path.GetFullPath(path), "trailer/Root/Pages/Count"], cancellationToken);
        if (!int.TryParse(count?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var pageCount) || pageCount <= 0)
        {
            return null;
        }

        var pattern = Path.Combine(workspace.DirectoryPath, "page-%d.png");
        var last = Math.Min(pageCount, MaxPages);
        var drawn = await pdf.RunMutoolAsync(
            ["draw", "-q", "-o", pattern, "-w", PageWidth.ToString(CultureInfo.InvariantCulture), "-F", "png", Path.GetFullPath(path), $"1-{last}"],
            cancellationToken);
        if (!drawn.IsSuccess)
        {
            return null;
        }

        var pages = Enumerable.Range(1, last)
            .Select(i => Path.Combine(workspace.DirectoryPath, string.Create(CultureInfo.InvariantCulture, $"page-{i}.png")))
            .TakeWhile(File.Exists)
            .ToList();
        return new FilePreview(PreviewKind.Pages, path, workspace) { Pages = pages, PageCount = pageCount };
    }

    private async Task<FilePreview?> MediaAsync(string path, ITempWorkspace workspace, CancellationToken cancellationToken)
    {
        var info = await ffmpeg.ProbeAsync(path, cancellationToken);
        if (info?.Duration is not { } duration || duration <= TimeSpan.Zero)
        {
            return null;
        }

        var output = workspace.NewFilePath(".png");
        var seconds = duration.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
        IReadOnlyList<string> arguments = info.HasVideo
            ? ["-an", "-sn", "-vf", $"fps={FilmstripFrames}/{seconds},scale=-2:90,tile={FilmstripFrames}x1", "-frames:v", "1", "-update", "1"]
            : ["-vn", "-sn", "-filter_complex", "aformat=channel_layouts=mono,showwavespic=s=1600x240:colors=white:scale=sqrt", "-frames:v", "1", "-update", "1"];
        var result = await ffmpeg.RunAsync(path, arguments, output, progress: null, cancellationToken);
        var kind = info.HasVideo ? PreviewKind.Video : PreviewKind.Audio;

        // Sin imagen (códec raro) igual sirve el reproductor y el rango: la imagen es opcional.
        return new FilePreview(kind, path, workspace)
        {
            Duration = duration,
            Image = result.IsSuccess && File.Exists(output) ? output : null,
            ImageTiles = info.HasVideo ? FilmstripFrames : 1,
        };
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo generar la vista previa de {Path}")]
    private partial void LogFailed(Exception exception, string path);
}

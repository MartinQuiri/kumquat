// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Core.Previews;

/// <summary>
/// Vista previa de un archivo para la ventana de opciones. Las imágenes viven en una carpeta
/// temporal que se borra al desechar la vista previa.
/// </summary>
public sealed class FilePreview : IDisposable
{
    private readonly IDisposable? _storage;

    /// <summary>Crea una vista previa.</summary>
    /// <param name="kind">Tipo.</param>
    /// <param name="sourcePath">Archivo original (el reproductor de audio/video lo abre directo).</param>
    /// <param name="storage">Carpeta temporal a borrar al desechar, si hay.</param>
    public FilePreview(PreviewKind kind, string sourcePath, IDisposable? storage = null)
    {
        Kind = kind;
        SourcePath = sourcePath ?? throw new ArgumentNullException(nameof(sourcePath));
        _storage = storage;
    }

    /// <summary>Tipo de vista previa.</summary>
    public PreviewKind Kind { get; }

    /// <summary>Archivo original.</summary>
    public string SourcePath { get; }

    /// <summary>
    /// Imagen principal: la miniatura de una foto, la tira de fotogramas de un video (en una
    /// sola imagen, de izquierda a derecha) o la forma de onda de un audio (blanca sobre transparente).
    /// </summary>
    public string? Image { get; init; }

    /// <summary>Cuadros lado a lado en <see cref="Image"/> (la tira de un video); 1 en los demás casos.</summary>
    public int ImageTiles { get; init; } = 1;

    /// <summary>Miniaturas de las primeras páginas de un PDF, en orden (puede haber menos que <see cref="PageCount"/>).</summary>
    public IReadOnlyList<string> Pages { get; init; } = [];

    /// <summary>Total de páginas de un PDF.</summary>
    public int PageCount { get; init; }

    /// <summary>Reproducción y fotogramas exactos (audio y video), si el motor está disponible.</summary>
    public IMediaPlayback? Playback { get; init; }

    /// <summary>Duración de un audio o video.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Sin vista previa.</summary>
    public static FilePreview None(string sourcePath) => new(PreviewKind.None, sourcePath);

    /// <inheritdoc />
    public void Dispose() => _storage?.Dispose();
}

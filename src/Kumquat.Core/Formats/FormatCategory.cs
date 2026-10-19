// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Core.Formats;

/// <summary>Familia de un formato; agrupa los sectores de la rueda.</summary>
public enum FormatCategory
{
    /// <summary>Imágenes fijas o animadas.</summary>
    Image,

    /// <summary>Audio.</summary>
    Audio,

    /// <summary>Video.</summary>
    Video,

    /// <summary>PDF.</summary>
    Pdf,

    /// <summary>Documentos de texto.</summary>
    Document,

    /// <summary>Hojas de cálculo.</summary>
    Spreadsheet,

    /// <summary>Presentaciones.</summary>
    Slides,

    /// <summary>Libros electrónicos.</summary>
    Ebook,

    /// <summary>Subtítulos.</summary>
    Subtitle,

    /// <summary>Archivos comprimidos.</summary>
    Archive,
}

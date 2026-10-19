// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Core.Previews;

/// <summary>Tipo de vista previa.</summary>
public enum PreviewKind
{
    /// <summary>Sin vista previa (formato sin representación visual o motor ausente).</summary>
    None,

    /// <summary>Foto.</summary>
    Image,

    /// <summary>Páginas de un PDF.</summary>
    Pages,

    /// <summary>Video: reproductor y tira de fotogramas.</summary>
    Video,

    /// <summary>Audio: reproductor y forma de onda.</summary>
    Audio,
}

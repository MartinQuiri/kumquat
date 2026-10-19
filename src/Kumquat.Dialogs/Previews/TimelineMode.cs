// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Dialogs.Previews;

/// <summary>Qué se elige en la línea de tiempo.</summary>
internal enum TimelineMode
{
    /// <summary>Nada: solo ver y escuchar (p. ej. metadatos).</summary>
    View,

    /// <summary>Un tramo con dos asas (recortar).</summary>
    Range,

    /// <summary>Un instante: el cabezal es la elección (fotograma).</summary>
    Point,
}

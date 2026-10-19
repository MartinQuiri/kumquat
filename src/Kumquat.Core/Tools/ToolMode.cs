// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Core.Tools;

/// <summary>Cómo trata una herramienta varios archivos soltados juntos.</summary>
public enum ToolMode
{
    /// <summary>Se aplica a cada archivo por separado (comprimir, rotar...).</summary>
    EachFile,

    /// <summary>Combina todos los archivos en un solo resultado (unir PDF, comprimir en ZIP...).</summary>
    Combine,
}

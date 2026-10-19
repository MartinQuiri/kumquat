// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Core.Previews;

/// <summary>Genera vistas previas con los motores locales.</summary>
public interface IPreviewService
{
    /// <summary>
    /// Genera la vista previa de un archivo. Nunca lanza por un archivo dañado o un motor
    /// ausente: devuelve <see cref="FilePreview.None"/>. El llamador la desecha.
    /// </summary>
    Task<FilePreview> CreateAsync(string path, CancellationToken cancellationToken);
}

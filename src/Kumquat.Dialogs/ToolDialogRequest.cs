// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Previews;
using Kumquat.Core.Tools;

namespace Kumquat.Dialogs;

/// <summary>Lo que necesita la ventana de opciones de una herramienta.</summary>
/// <param name="ToolId">Id de la herramienta (elige ícono y color).</param>
/// <param name="Title">Nombre de la herramienta.</param>
/// <param name="Subtitle">Archivo (o "3 archivos").</param>
/// <param name="Parameters">Campos a pedir.</param>
/// <param name="Validate">Validación de la herramienta: mensaje de error o <c>null</c>.</param>
public sealed record ToolDialogRequest(
    string ToolId,
    string Title,
    string Subtitle,
    IReadOnlyList<ToolParameter> Parameters,
    Func<IReadOnlyDictionary<string, string>, string?> Validate)
{
    /// <summary>
    /// Genera la vista previa en segundo plano (la ventana aparece enseguida y la muestra al
    /// terminar). <c>null</c>: ventana compacta, sin panel de vista previa.
    /// </summary>
    public Func<CancellationToken, Task<FilePreview>>? Preview { get; init; }
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Core.Results;

/// <summary>
/// Error esperado del dominio (formato no soportado, motor ausente, cancelación...).
/// Las excepciones quedan reservadas para bugs.
/// </summary>
/// <param name="Code">Código estable, apto para telemetría y tests.</param>
/// <param name="Message">Mensaje legible para el usuario o el log.</param>
public sealed record Error(string Code, string Message)
{
    /// <summary>El usuario o el sistema canceló la operación.</summary>
    public static Error Cancelled { get; } = new("cancelled", "La operación fue cancelada.");

    /// <inheritdoc />
    public override string ToString() => $"{Code}: {Message}";
}

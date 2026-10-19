// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Text;

namespace Kumquat.Infrastructure.Processes;

/// <summary>Descripción de un proceso de motor a lanzar.</summary>
public sealed record ProcessSpec
{
    /// <summary>Ruta absoluta del ejecutable.</summary>
    public required string FileName { get; init; }

    /// <summary>Argumentos, uno por elemento: nunca se concatenan ni pasan por un shell.</summary>
    public required IReadOnlyList<string> Arguments { get; init; }

    /// <summary>Carpeta de trabajo; por defecto la del ejecutable.</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>Tiempo máximo de ejecución.</summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>Memoria máxima del proceso (Job Object).</summary>
    public long? MaxMemoryBytes { get; init; }

    /// <summary>Codificación de stdout/stderr del motor (UTF-8 sin BOM por defecto).</summary>
    public Encoding OutputEncoding { get; init; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Callback por cada línea de stdout.</summary>
    public Action<string>? OnStandardOutputLine { get; init; }

    /// <summary>Callback por cada línea de stderr.</summary>
    public Action<string>? OnStandardErrorLine { get; init; }
}

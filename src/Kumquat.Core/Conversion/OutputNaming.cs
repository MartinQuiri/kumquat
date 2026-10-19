// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Globalization;
using Kumquat.Core.Formats;

namespace Kumquat.Core.Conversion;

/// <summary>
/// Elige el nombre del archivo de salida. Kumquat nunca sobrescribe: la salida es
/// una copia nueva junto al original (<c>foto.png</c>, <c>foto (2).png</c>...).
/// </summary>
public static class OutputNaming
{
    /// <summary>Límite de intentos antes de rendirse (evita bucles ante un sistema de archivos extraño).</summary>
    public const int MaxAttempts = 10_000;

    /// <summary>Ruta de salida para convertir <paramref name="inputPath"/> a <paramref name="target"/>.</summary>
    /// <param name="inputPath">Archivo de origen.</param>
    /// <param name="target">Formato de salida.</param>
    /// <param name="exists">Predicado de existencia (inyectable para tests).</param>
    /// <param name="outputDirectory">Carpeta de salida; por defecto la del origen.</param>
    /// <param name="suffix">Sufijo opcional: <c>foto (comprimido).jpg</c>.</param>
    public static string ChooseOutputPath(string inputPath, FileFormat target, Func<string, bool> exists, string? outputDirectory = null, string? suffix = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(inputPath);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(exists);

        var directory = outputDirectory ?? Path.GetDirectoryName(Path.GetFullPath(inputPath))
            ?? throw new ArgumentException("La ruta de entrada no tiene carpeta.", nameof(inputPath));
        var stem = Path.GetFileNameWithoutExtension(inputPath);
        if (stem.Length == 0)
        {
            stem = "kumquat";
        }

        if (!string.IsNullOrWhiteSpace(suffix))
        {
            stem = $"{stem} ({suffix})";
        }

        var candidate = Path.Combine(directory, stem + target.DefaultExtension);
        if (!SamePath(candidate, inputPath) && !exists(candidate))
        {
            return candidate;
        }

        for (var n = 2; n < MaxAttempts; n++)
        {
            candidate = Path.Combine(directory, string.Create(CultureInfo.InvariantCulture, $"{stem} ({n}){target.DefaultExtension}"));
            if (!exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException($"No se encontró un nombre libre para '{stem}{target.DefaultExtension}'.");
    }

    /// <summary>Carpeta de salida libre junto al origen: <c>informe (páginas)</c>, <c>informe (páginas) (2)</c>...</summary>
    /// <param name="inputPath">Archivo de origen.</param>
    /// <param name="suffix">Sufijo; vacío = solo el nombre del origen (p. ej. al extraer un ZIP).</param>
    /// <param name="exists">Predicado de existencia (archivo o carpeta).</param>
    /// <param name="outputDirectory">Carpeta contenedora; por defecto la del origen.</param>
    public static string ChooseDirectoryPath(string inputPath, string? suffix, Func<string, bool> exists, string? outputDirectory = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(inputPath);
        ArgumentNullException.ThrowIfNull(exists);

        var parent = outputDirectory ?? Path.GetDirectoryName(Path.GetFullPath(inputPath))
            ?? throw new ArgumentException("La ruta de entrada no tiene carpeta.", nameof(inputPath));
        var stem = Path.GetFileNameWithoutExtension(Path.TrimEndingDirectorySeparator(inputPath));
        if (stem.Length == 0)
        {
            stem = "kumquat";
        }

        if (!string.IsNullOrWhiteSpace(suffix))
        {
            stem = $"{stem} ({suffix})";
        }

        var candidate = Path.Combine(parent, stem);
        if (!exists(candidate))
        {
            return candidate;
        }

        for (var n = 2; n < MaxAttempts; n++)
        {
            candidate = Path.Combine(parent, string.Create(CultureInfo.InvariantCulture, $"{stem} ({n})"));
            if (!exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException($"No se encontró un nombre libre para la carpeta '{stem}'.");
    }

    private static bool SamePath(string a, string b)
        => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
}

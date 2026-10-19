// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.IO.Compression;
using Kumquat.Core.Results;

namespace Kumquat.Infrastructure.Files;

/// <summary>
/// Extrae ZIP sin Zip Slip (rutas que escapan de la carpeta destino) ni bombas de
/// descompresión. Es el único extractor permitido (ver BannedSymbols.txt).
/// </summary>
public static class SafeExtractor
{
    /// <summary>Extrae un ZIP a una carpeta nueva o vacía.</summary>
    /// <returns>Lista de archivos extraídos, o un error si el archivo es peligroso.</returns>
    public static async Task<Result<IReadOnlyList<string>>> ExtractZipAsync(string zipPath, string destination, ExtractionLimits? limits, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(zipPath);
        ArgumentException.ThrowIfNullOrEmpty(destination);
        limits ??= ExtractionLimits.Default;

        var root = Path.GetFullPath(destination);
        var rootWithSeparator = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(root);

        using var archive = await ZipFile.OpenReadAsync(zipPath, cancellationToken);
        var check = Validate(archive, rootWithSeparator, limits);
        if (!check.IsSuccess)
        {
            return check.Error;
        }

        var written = new List<string>();
        long total = 0;
        foreach (var (entry, target) in check.Value)
        {
            if (target.EndsWith(Path.DirectorySeparatorChar))
            {
                Directory.CreateDirectory(target);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using var input = await entry.OpenAsync(cancellationToken);
            await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);

            // El tamaño declarado en la cabecera puede mentir: se cuenta lo realmente escrito.
            var buffer = new byte[81920];
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                total += read;
                if (total > limits.MaxTotalBytes)
                {
                    return new Error("archive-too-large", "El archivo comprimido supera el tamaño máximo permitido.");
                }

                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            written.Add(target);
        }

        return written;
    }

    private static Result<List<(ZipArchiveEntry Entry, string Target)>> Validate(ZipArchive archive, string rootWithSeparator, ExtractionLimits limits)
    {
        if (archive.Entries.Count > limits.MaxEntries)
        {
            return new Error("archive-too-many-entries", $"El archivo tiene más de {limits.MaxEntries} entradas.");
        }

        var plan = new List<(ZipArchiveEntry, string)>(archive.Entries.Count);
        long declaredTotal = 0;
        foreach (var entry in archive.Entries)
        {
            var target = Path.GetFullPath(Path.Combine(rootWithSeparator, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                return new Error("archive-path-traversal", $"La entrada '{entry.FullName}' intenta escribir fuera de la carpeta destino.");
            }

            declaredTotal += entry.Length;
            if (declaredTotal > limits.MaxTotalBytes)
            {
                return new Error("archive-too-large", "El archivo comprimido supera el tamaño máximo permitido.");
            }

            if (entry.CompressedLength > 0 && (double)entry.Length / entry.CompressedLength > limits.MaxCompressionRatio)
            {
                return new Error("archive-bomb", $"La entrada '{entry.FullName}' tiene una razón de compresión sospechosa.");
            }

            plan.Add((entry, target));
        }

        return plan;
    }
}

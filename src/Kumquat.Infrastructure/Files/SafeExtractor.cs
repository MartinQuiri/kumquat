// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Localization;
using Kumquat.Core.Results;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace Kumquat.Infrastructure.Files;

/// <summary>
/// Extrae ZIP, RAR, 7z, TAR y GZIP sin Zip Slip (rutas que escapan de la carpeta destino) ni
/// bombas de descompresión. Es el único extractor permitido (ver BannedSymbols.txt).
/// </summary>
public static class SafeExtractor
{
    /// <summary>
    /// Cómo comparar rutas: Windows no distingue mayúsculas; Linux sí, y comparar sin distinguirlas
    /// dejaría pasar "../OUT/x" (una carpeta hermana de "out") como si fuera de adentro.
    /// </summary>
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>Extrae un ZIP a una carpeta nueva o vacía.</summary>
    public static Task<Result<IReadOnlyList<string>>> ExtractZipAsync(string zipPath, string destination, ExtractionLimits? limits, CancellationToken cancellationToken)
        => ExtractAsync(zipPath, destination, limits, cancellationToken);

    /// <summary>Extrae cualquier archivo comprimido soportado a una carpeta nueva o vacía.</summary>
    /// <returns>Lista de archivos extraídos, o un error si el archivo es peligroso o está dañado.</returns>
    public static async Task<Result<IReadOnlyList<string>>> ExtractAsync(string archivePath, string destination, ExtractionLimits? limits, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(archivePath);
        ArgumentException.ThrowIfNullOrEmpty(destination);
        limits ??= ExtractionLimits.Default;

        var root = Path.GetFullPath(destination);
        var rootWithSeparator = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(root);

        try
        {
            if (!IsGzip(archivePath))
            {
                return await ExtractArchiveAsync(archivePath, rootWithSeparator, limits, cancellationToken);
            }

            // GZIP comprime un solo archivo; si es un TAR (.tar.gz / .tgz) se desempaqueta también.
            // Se reconoce el TAR por su firma "ustar": GZipStream de .NET no guarda el nombre.
            var single = await GunzipAsync(archivePath, rootWithSeparator, limits, cancellationToken);
            if (!single.IsSuccess || !IsTar(single.Value))
            {
                return single.IsSuccess ? new[] { single.Value } : single.Error;
            }

            var inner = await ExtractArchiveAsync(single.Value, rootWithSeparator, limits, cancellationToken);
            File.Delete(single.Value);
            return inner;
        }
        catch (ArchiveOperationException)
        {
            return new Error("archive-invalid", Strings.ErrorArchiveInvalid);
        }
        catch (InvalidDataException)
        {
            return new Error("archive-invalid", Strings.ErrorArchiveDamaged);
        }
        catch (InvalidFormatException)
        {
            return new Error("archive-invalid", Strings.ErrorArchiveInvalid);
        }
        catch (CryptographicException)
        {
            return new Error("archive-encrypted", Strings.ErrorArchiveEncrypted);
        }
    }

    /// <summary>Descomprime un GZIP a un único archivo (nombre: el del .gz sin extensión), contando bytes reales.</summary>
    private static async Task<Result<string>> GunzipAsync(string archivePath, string rootWithSeparator, ExtractionLimits limits, CancellationToken cancellationToken)
    {
        var name = Path.GetFileNameWithoutExtension(archivePath);
        var target = Path.Combine(rootWithSeparator, string.IsNullOrEmpty(name) ? Strings.FallbackFileName : name);
        var compressedLength = Math.Max(1, new FileInfo(archivePath).Length);

        await using var input = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        await using var gzip = new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress);
        await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await gzip.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > limits.MaxTotalBytes || (total > 64 * 1024 * 1024 && (double)total / compressedLength > limits.MaxCompressionRatio))
            {
                return new Error(total > limits.MaxTotalBytes ? "archive-too-large" : "archive-bomb", Strings.ErrorArchiveSuspicious);
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        // Un GZIP truncado puede "terminar" sin datos en vez de fallar.
        return total == 0 ? new Error("archive-invalid", Strings.ErrorArchiveEmpty) : target;
    }

    private static async Task<Result<IReadOnlyList<string>>> ExtractArchiveAsync(string archivePath, string rootWithSeparator, ExtractionLimits limits, CancellationToken cancellationToken)
    {
        using var archive = ArchiveFactory.OpenArchive(archivePath, new ReaderOptions());
        var plan = Validate([.. archive.Entries], rootWithSeparator, limits, Path.GetFileNameWithoutExtension(archivePath));
        if (!plan.IsSuccess)
        {
            return plan.Error;
        }

        var written = new List<string>();
        long total = 0;
        var buffer = new byte[81920];
        foreach (var (entry, target) in plan.Value)
        {
            if (entry.IsDirectory)
            {
                Directory.CreateDirectory(target);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using var input = await entry.OpenEntryStreamAsync(cancellationToken);
            await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);

            // El tamaño declarado en la cabecera puede mentir: se cuenta lo realmente escrito.
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                total += read;
                if (total > limits.MaxTotalBytes)
                {
                    return new Error("archive-too-large", Strings.ErrorArchiveTooLarge);
                }

                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            written.Add(target);
        }

        return written;
    }

    private static Result<List<(IArchiveEntry Entry, string Target)>> Validate(IReadOnlyList<IArchiveEntry> entries, string rootWithSeparator, ExtractionLimits limits, string fallbackName)
    {
        if (entries.Count > limits.MaxEntries)
        {
            return new Error("archive-too-many-entries", Strings.ErrorArchiveTooManyEntries(limits.MaxEntries));
        }

        var plan = new List<(IArchiveEntry, string)>(entries.Count);
        long declaredTotal = 0;
        foreach (var entry in entries)
        {
            // GZIP simple no guarda nombre: se usa el del archivo sin ".gz".
            var key = string.IsNullOrEmpty(entry.Key) ? fallbackName : entry.Key;
            // Las dos barras separan carpetas en cualquier sistema: un ZIP hecho en Windows puede traer
            // "..\\x", que en Linux sería un nombre de archivo válido y no se detectaría como salida.
            var relative = key.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var target = Path.GetFullPath(Path.Combine(rootWithSeparator, relative));
            if (!target.StartsWith(rootWithSeparator, PathComparison))
            {
                return new Error("archive-path-traversal", Strings.ErrorArchivePathTraversal(key));
            }

            declaredTotal += Math.Max(0, entry.Size);
            if (declaredTotal > limits.MaxTotalBytes)
            {
                return new Error("archive-too-large", Strings.ErrorArchiveTooLarge);
            }

            // En archivos sólidos (7z) el tamaño comprimido por entrada no existe (0): no se puede medir la razón.
            if (entry.CompressedSize > 0 && (double)entry.Size / entry.CompressedSize > limits.MaxCompressionRatio)
            {
                return new Error("archive-bomb", Strings.ErrorArchiveBomb(key));
            }

            plan.Add((entry, target));
        }

        return plan;
    }

    private static bool IsTar(string path)
    {
        Span<byte> header = stackalloc byte[262];
        using var stream = File.OpenRead(path);
        return stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false) == header.Length && header[257..262].SequenceEqual("ustar"u8);
    }

    private static bool IsGzip(string path)
    {
        Span<byte> magic = stackalloc byte[2];
        using var stream = File.OpenRead(path);
        return stream.Read(magic) == 2 && magic[0] == 0x1F && magic[1] == 0x8B;
    }
}

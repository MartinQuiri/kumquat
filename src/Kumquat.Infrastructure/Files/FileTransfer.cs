// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Globalization;
using Kumquat.Core.Localization;
using Kumquat.Core.Results;

namespace Kumquat.Infrastructure.Files;

/// <summary>
/// Copiar y mover archivos o carpetas a otra carpeta sin sobrescribir nunca: si el nombre ya
/// existe se usa <c>nombre (2).ext</c>. Mover entre unidades copia y luego borra el origen solo
/// si la copia terminó completa.
/// </summary>
public static class FileTransfer
{
    /// <summary>Copia un archivo o carpeta dentro de <paramref name="destinationFolder"/>.</summary>
    /// <returns>Ruta del elemento creado.</returns>
    public static Task<Result<string>> CopyAsync(string source, string destinationFolder, CancellationToken cancellationToken)
        => TransferAsync(source, destinationFolder, move: false, cancellationToken);

    /// <summary>Mueve un archivo o carpeta dentro de <paramref name="destinationFolder"/>.</summary>
    /// <returns>Nueva ruta del elemento.</returns>
    public static Task<Result<string>> MoveAsync(string source, string destinationFolder, CancellationToken cancellationToken)
        => TransferAsync(source, destinationFolder, move: true, cancellationToken);

    /// <summary>Primera ruta libre para <paramref name="name"/> dentro de la carpeta.</summary>
    public static string UniquePath(string folder, string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        var candidate = Path.Combine(folder, name);
        if (!Exists(candidate))
        {
            return candidate;
        }

        var isFile = Path.HasExtension(name) && !Directory.Exists(candidate);
        var stem = isFile ? Path.GetFileNameWithoutExtension(name) : name;
        var extension = isFile ? Path.GetExtension(name) : string.Empty;
        for (var n = 2; n < 10_000; n++)
        {
            candidate = Path.Combine(folder, string.Create(CultureInfo.InvariantCulture, $"{stem} ({n}){extension}"));
            if (!Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException($"No se encontró un nombre libre para '{name}'.");
    }

    private static async Task<Result<string>> TransferAsync(string source, string destinationFolder, bool move, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);
        ArgumentException.ThrowIfNullOrEmpty(destinationFolder);
        var fullSource = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source));
        var folder = Path.GetFullPath(destinationFolder);
        if (Validate(fullSource, folder) is { } invalid)
        {
            return invalid;
        }

        if (move && string.Equals(Path.GetDirectoryName(fullSource), folder.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            return fullSource; // Ya está ahí.
        }

        try
        {
            var target = UniquePath(folder, Path.GetFileName(fullSource));
            if (move && SameVolume(fullSource, folder))
            {
                if (Directory.Exists(fullSource))
                {
                    Directory.Move(fullSource, target);
                }
                else
                {
                    File.Move(fullSource, target, overwrite: false);
                }

                return target;
            }

            await CopyRecursiveAsync(fullSource, target, cancellationToken);
            if (move)
            {
                // Solo tras una copia completa se borra el original.
                if (Directory.Exists(fullSource))
                {
                    Directory.Delete(fullSource, recursive: true);
                }
                else
                {
                    File.Delete(fullSource);
                }
            }

            return target;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Error.Cancelled;
        }
        catch (UnauthorizedAccessException ex)
        {
            return new Error("access-denied", ex.Message);
        }
        catch (IOException ex)
        {
            return new Error("io", ex.Message);
        }
    }

    private static Error? Validate(string source, string folder)
    {
        if (!Exists(source))
        {
            return new Error("input-missing", Strings.ErrorToolInputMissing(source));
        }

        if (!Directory.Exists(folder))
        {
            return new Error("destination-missing", Strings.ErrorFolderMissing(folder));
        }

        // Copiar una carpeta dentro de sí misma no termina nunca.
        var inside = Directory.Exists(source)
            && (folder + Path.DirectorySeparatorChar).StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        return inside ? new Error("destination-inside-source", Strings.ErrorCopyIntoItself) : null;
    }

    private static async Task CopyRecursiveAsync(string source, string target, CancellationToken cancellationToken)
    {
        if (File.Exists(source))
        {
            await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await input.CopyToAsync(output, cancellationToken);
            File.SetLastWriteTimeUtc(target, File.GetLastWriteTimeUtc(source));
            return;
        }

        Directory.CreateDirectory(target);
        var options = new EnumerationOptions { AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true };
        foreach (var entry in Directory.EnumerateFileSystemEntries(source, "*", options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await CopyRecursiveAsync(entry, Path.Combine(target, Path.GetFileName(entry)), cancellationToken);
        }
    }

    private static bool SameVolume(string a, string b)
        => string.Equals(Path.GetPathRoot(a), Path.GetPathRoot(b), StringComparison.OrdinalIgnoreCase);

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
}

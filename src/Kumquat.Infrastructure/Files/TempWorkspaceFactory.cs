// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Conversion;

namespace Kumquat.Infrastructure.Files;

/// <summary>
/// Crea carpetas de trabajo bajo <c>%TEMP%\Kumquat</c>. Cada trabajo tiene la suya y la
/// borra al terminar; lo que sobreviva a un crash se limpia en el siguiente arranque.
/// </summary>
public sealed class TempWorkspaceFactory : ITempWorkspaceFactory
{
    private readonly string _root;
    private readonly TimeProvider _time;

    /// <summary>Crea la fábrica.</summary>
    /// <param name="time">Reloj inyectable.</param>
    /// <param name="root">Carpeta raíz; por defecto <c>%TEMP%\Kumquat</c>.</param>
    public TempWorkspaceFactory(TimeProvider time, string? root = null)
    {
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _root = root ?? Path.Combine(Path.GetTempPath(), "Kumquat");
    }

    /// <summary>Carpeta raíz.</summary>
    public string Root => _root;

    /// <inheritdoc />
    public ITempWorkspace Create()
    {
        var path = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return new TempWorkspace(path);
    }

    /// <summary>Borra carpetas de trabajo más viejas que <paramref name="maxAge"/>.</summary>
    /// <returns>Cantidad de carpetas borradas.</returns>
    public int CleanupStale(TimeSpan maxAge)
    {
        if (!Directory.Exists(_root))
        {
            return 0;
        }

        var cutoff = _time.GetUtcNow().UtcDateTime - maxAge;
        var deleted = 0;
        foreach (var directory in new DirectoryInfo(_root).EnumerateDirectories())
        {
            if (directory.LastWriteTimeUtc < cutoff && TempWorkspace.TryDeleteDirectory(directory.FullName))
            {
                deleted++;
            }
        }

        return deleted;
    }

    private sealed class TempWorkspace(string path) : ITempWorkspace
    {
        public string DirectoryPath { get; } = path;

        public string NewFilePath(string extension)
        {
            ArgumentException.ThrowIfNullOrEmpty(extension);
            if (!extension.StartsWith('.') || extension.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || extension.Contains("..", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Extensión inválida: '{extension}'.", nameof(extension));
            }

            return Path.Combine(DirectoryPath, Guid.NewGuid().ToString("N") + extension);
        }

        public void Dispose() => TryDeleteDirectory(DirectoryPath);

        internal static bool TryDeleteDirectory(string directory)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
                return true;
            }
            catch (IOException)
            {
                return false; // Un motor aún tiene un archivo abierto; se reintenta en el próximo arranque.
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}

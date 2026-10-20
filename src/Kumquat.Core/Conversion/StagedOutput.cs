// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Globalization;
using Kumquat.Core.Formats;
using Kumquat.Core.Localization;
using Kumquat.Core.Results;

namespace Kumquat.Core.Conversion;

/// <summary>
/// Escribe una salida de forma segura: el motor escribe dentro de una carpeta <c>.kumquat-*</c>
/// oculta y de sistema en la carpeta destino (no se ve en el Explorador aunque muestre los
/// archivos ocultos) y, solo si termina bien, se publica con un <c>File.Move</c> atómico (mismo
/// volumen, mismos permisos que la carpeta destino) al primer nombre libre. Nunca deja archivos a
/// medias con el nombre final ni sobrescribe nada. Compartido por conversiones y herramientas.
/// </summary>
/// <remarks>
/// El archivo no se puede crear oculto de antemano: los motores lo reemplazan al escribir y
/// Windows niega reemplazar un archivo oculto. Por eso se oculta la carpeta que lo contiene.
/// </remarks>
internal static class StagedOutput
{
    /// <summary>Ejecuta <paramref name="produce"/> sobre un archivo temporal y lo publica.</summary>
    /// <param name="inputPath">Archivo de origen (da el nombre base).</param>
    /// <param name="target">Formato de salida (da la extensión).</param>
    /// <param name="directory">Carpeta de salida.</param>
    /// <param name="suffix">Sufijo opcional del nombre (p. ej. "comprimido").</param>
    /// <param name="produce">Escribe la salida en la ruta temporal recibida.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <param name="exists">
    /// Cómo saber si un nombre está ocupado (por defecto <see cref="File.Exists(string?)"/>). Los
    /// tests lo reemplazan para provocar la carrera "otro proceso tomó el nombre": aun así,
    /// <c>File.Move</c> sin sobrescribir es lo que garantiza que nada se pise.
    /// </param>
    public static async Task<Result<string>> RunAsync(
        string inputPath,
        FileFormat target,
        string directory,
        string? suffix,
        Func<string, Task<Result>> produce,
        CancellationToken cancellationToken,
        Func<string, bool>? exists = null)
    {
        var stagingDirectory = CreateHiddenStaging(directory);
        var stagingPath = Path.Combine(stagingDirectory, "salida" + target.DefaultExtension);
        try
        {
            var result = await produce(stagingPath);
            if (!result.IsSuccess)
            {
                return result.Error;
            }

            if (!File.Exists(stagingPath))
            {
                return new Error("no-output", Strings.ErrorNoOutput);
            }

            return Publish(stagingPath, inputPath, target, directory, suffix, exists ?? File.Exists);
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
        finally
        {
            TryDeleteDirectory(stagingDirectory);
        }
    }

    /// <summary>
    /// Igual que <see cref="RunAsync"/> pero la salida es una carpeta (p. ej. las páginas de un PDF):
    /// se llena una carpeta oculta y se publica con un <c>Directory.Move</c> atómico.
    /// </summary>
    public static async Task<Result<string>> RunDirectoryAsync(
        string inputPath,
        string directory,
        string? suffix,
        Func<string, Task<Result>> produce,
        CancellationToken cancellationToken)
    {
        // La carpeta de salida va dentro de la oculta: así el resultado publicado no hereda los atributos.
        var stagingDirectory = CreateHiddenStaging(directory);
        var staging = Directory.CreateDirectory(Path.Combine(stagingDirectory, "salida")).FullName;
        try
        {
            var result = await produce(staging);
            if (!result.IsSuccess)
            {
                return result.Error;
            }

            if (!Directory.EnumerateFileSystemEntries(staging).Any())
            {
                return new Error("no-output", Strings.ErrorToolNoOutput);
            }

            for (var attempt = 0; ; attempt++)
            {
                var finalPath = OutputNaming.ChooseDirectoryPath(inputPath, suffix, p => Directory.Exists(p) || File.Exists(p), directory);
                try
                {
                    Directory.Move(staging, finalPath);
                    return finalPath;
                }
                catch (IOException) when (attempt < 5 && (Directory.Exists(finalPath) || File.Exists(finalPath)))
                {
                    // Carrera por el nombre: reintentar.
                }
            }
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
        finally
        {
            TryDeleteDirectory(stagingDirectory);
        }
    }

    /// <summary>Carpeta temporal oculta y de sistema dentro de la carpeta destino (mismo volumen).</summary>
    private static string CreateHiddenStaging(string directory)
    {
        var staging = Directory.CreateDirectory(Path.Combine(directory, string.Create(CultureInfo.InvariantCulture, $".kumquat-{Guid.NewGuid():N}")));
        if (OperatingSystem.IsWindows())
        {
            staging.Attributes |= FileAttributes.Hidden | FileAttributes.System;
        }

        return staging.FullName;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Mejor esfuerzo: la carpeta .kumquat-* queda (oculta y de sistema: no se ve).
        }
        catch (UnauthorizedAccessException)
        {
            // Idem.
        }
    }

    private static string Publish(string stagingPath, string inputPath, FileFormat target, string directory, string? suffix, Func<string, bool> exists)
    {
        // Mover dentro del mismo volumen es atómico; si otro proceso tomó el nombre entre
        // la elección y el movimiento, se elige el siguiente libre.
        for (var attempt = 0; ; attempt++)
        {
            var finalPath = OutputNaming.ChooseOutputPath(inputPath, target, exists, directory, suffix);
            try
            {
                File.Move(stagingPath, finalPath, overwrite: false);
                return finalPath;
            }
            catch (IOException) when (attempt < 5 && File.Exists(finalPath))
            {
                // Carrera por el nombre: reintentar.
            }
        }
    }
}

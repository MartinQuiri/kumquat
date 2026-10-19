// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;

namespace Kumquat.Infrastructure.Engines;

/// <summary>
/// Encuentra los binarios de los motores. Orden de búsqueda:
/// <list type="number">
/// <item><c>%KUMQUAT_ENGINES%\&lt;id&gt;\</c> (override para CI y desarrollo).</item>
/// <item><c>&lt;app&gt;\engines\&lt;id&gt;\</c> (instalación).</item>
/// <item><c>&lt;repo&gt;\engines\&lt;id&gt;\&lt;rid&gt;\</c> subiendo desde la app (desarrollo, tras <c>tools/fetch-engines.ps1</c>).</item>
/// </list>
/// </summary>
public sealed class EngineLocator
{
    /// <summary>Variable de entorno que fuerza la carpeta raíz de motores.</summary>
    public const string EnvironmentVariable = "KUMQUAT_ENGINES";

    private readonly string _appBase;
    private readonly string? _overrideRoot;

    /// <summary>Crea el localizador.</summary>
    /// <param name="appBase">Carpeta de la aplicación; por defecto <see cref="AppContext.BaseDirectory"/>.</param>
    /// <param name="overrideRoot">Raíz explícita; por defecto la variable <see cref="EnvironmentVariable"/>.</param>
    public EngineLocator(string? appBase = null, string? overrideRoot = null)
    {
        _appBase = appBase ?? AppContext.BaseDirectory;
        _overrideRoot = overrideRoot ?? Environment.GetEnvironmentVariable(EnvironmentVariable);
    }

    /// <summary>RID del proceso actual (<c>win-x64</c> o <c>win-arm64</c>).</summary>
    public static string RuntimeIdentifier => RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "win-arm64" : "win-x64";

    /// <summary>Ruta absoluta de un ejecutable de motor, o <c>null</c> si no está instalado.</summary>
    /// <param name="engineId">Id del motor en <c>tools/engines.json</c>, p. ej. <c>ffmpeg</c>.</param>
    /// <param name="relativePath">Ruta dentro de la carpeta del motor, p. ej. <c>bin\ffmpeg.exe</c>.</param>
    public string? Find(string engineId, string relativePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(engineId);
        ArgumentException.ThrowIfNullOrEmpty(relativePath);
        return Candidates(engineId).Select(root => Path.Combine(root, relativePath)).FirstOrDefault(File.Exists);
    }

    private IEnumerable<string> Candidates(string engineId)
    {
        if (!string.IsNullOrEmpty(_overrideRoot))
        {
            yield return Path.Combine(_overrideRoot, engineId, RuntimeIdentifier);
            yield return Path.Combine(_overrideRoot, engineId);
        }

        yield return Path.Combine(_appBase, "engines", engineId);

        for (var dir = new DirectoryInfo(_appBase); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "engines", engineId, RuntimeIdentifier);
            if (Directory.Exists(candidate))
            {
                yield return candidate;
                yield break;
            }
        }
    }
}

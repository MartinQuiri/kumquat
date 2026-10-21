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
/// <item>Solo en Linux y macOS (<see cref="FindExecutable"/>): el <c>PATH</c>, donde quedan los motores
/// instalados con el gestor de paquetes del sistema.</item>
/// </list>
/// </summary>
public sealed class EngineLocator
{
    /// <summary>Variable de entorno que fuerza la carpeta raíz de motores.</summary>
    public const string EnvironmentVariable = "KUMQUAT_ENGINES";

    private readonly string _appBase;
    private readonly string? _overrideRoot;
    private readonly string? _searchPath;

    /// <summary>Crea el localizador.</summary>
    /// <param name="appBase">Carpeta de la aplicación; por defecto <see cref="AppContext.BaseDirectory"/>.</param>
    /// <param name="overrideRoot">Raíz explícita; por defecto la variable <see cref="EnvironmentVariable"/>.</param>
    /// <param name="searchPath">Carpetas donde buscar motores del sistema (formato de <c>PATH</c>); por defecto el <c>PATH</c>.</param>
    public EngineLocator(string? appBase = null, string? overrideRoot = null, string? searchPath = null)
    {
        _appBase = appBase ?? AppContext.BaseDirectory;
        _overrideRoot = overrideRoot ?? Environment.GetEnvironmentVariable(EnvironmentVariable);
        _searchPath = searchPath ?? Environment.GetEnvironmentVariable("PATH");
    }

    /// <summary>RID del proceso actual: <c>win-x64</c>, <c>win-arm64</c>, <c>linux-x64</c>, <c>osx-arm64</c>…</summary>
    public static string RuntimeIdentifier { get; } = $"{OperatingSystemPrefix}-{(RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64")}";

    /// <summary>Extensión de los ejecutables en este sistema (<c>.exe</c> en Windows, ninguna en el resto).</summary>
    public static string ExecutableExtension => OperatingSystem.IsWindows() ? ".exe" : string.Empty;

    private static string OperatingSystemPrefix => OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux";

    /// <summary>
    /// Ruta absoluta del ejecutable de un motor, o <c>null</c>. Busca primero en las carpetas propias
    /// de Kumquat y, fuera de Windows, después en el <c>PATH</c>.
    /// </summary>
    /// <param name="engineId">Id del motor en <c>tools/engines.json</c>, p. ej. <c>ffmpeg</c>.</param>
    /// <param name="name">Nombre del ejecutable sin extensión, p. ej. <c>ffprobe</c>.</param>
    /// <param name="folder">Subcarpeta dentro de la carpeta del motor, p. ej. <c>bin</c>.</param>
    public string? FindExecutable(string engineId, string name, string? folder = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        var file = name + ExecutableExtension;
        return Find(engineId, folder is null ? file : Path.Combine(folder, file))
            ?? (OperatingSystem.IsWindows() ? null : FindOnSearchPath(file));
    }

    /// <summary>Ruta absoluta de un ejecutable de motor, o <c>null</c> si no está instalado.</summary>
    /// <param name="engineId">Id del motor en <c>tools/engines.json</c>, p. ej. <c>ffmpeg</c>.</param>
    /// <param name="relativePath">Ruta dentro de la carpeta del motor, p. ej. <c>bin\ffmpeg.exe</c>.</param>
    public string? Find(string engineId, string relativePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(engineId);
        ArgumentException.ThrowIfNullOrEmpty(relativePath);
        return Candidates(engineId).Select(root => Path.Combine(root, relativePath)).FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// Primera coincidencia en el <c>PATH</c>, siempre como ruta absoluta (las entradas relativas se
    /// ignoran: resolverlas contra la carpeta actual permitiría secuestrar el motor).
    /// </summary>
    private string? FindOnSearchPath(string file)
        => (_searchPath ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(Path.IsPathFullyQualified)
            .Select(dir => Path.Combine(dir, file))
            .FirstOrDefault(File.Exists);

    private IEnumerable<string> Candidates(string engineId)
    {
        if (!string.IsNullOrEmpty(_overrideRoot))
        {
            yield return Path.Combine(_overrideRoot, engineId, RuntimeIdentifier);
            yield return Path.Combine(_overrideRoot, engineId);
        }

        yield return Path.Combine(_appBase, "engines", engineId);

        // Subiendo: paquete instalado (<pkg>\engines\<id>, p. ej. desde <pkg>\cli\) o repo de desarrollo
        // (<repo>\engines\<id>\<rid>).
        for (var dir = new DirectoryInfo(_appBase).Parent; dir is not null; dir = dir.Parent)
        {
            var installed = Path.Combine(dir.FullName, "engines", engineId);
            var development = Path.Combine(installed, RuntimeIdentifier);
            if (Directory.Exists(development))
            {
                yield return development;
                yield break;
            }

            if (Directory.Exists(installed))
            {
                yield return installed;
                yield break;
            }
        }
    }
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Design;

/// <summary>Rutas de los recursos que se copian junto al ejecutable (carpeta <c>assets</c>).</summary>
public static class DesignAssets
{
    /// <summary>Familia tipográfica de la interfaz (Inter, licencia OFL).</summary>
    public const string FontFamily = "Inter";

    /// <summary>Carpeta de recursos.</summary>
    public static string Directory { get; } = Path.Combine(AppContext.BaseDirectory, "assets");

    /// <summary>Carpeta con los TTF de Inter.</summary>
    public static string FontsDirectory => Path.Combine(Directory, "fonts");

    /// <summary>Archivos TTF de Inter presentes (puede estar vacío: se cae a Segoe UI).</summary>
    public static IReadOnlyList<string> FontFiles
        => System.IO.Directory.Exists(FontsDirectory) ? System.IO.Directory.GetFiles(FontsDirectory, "Inter-*.ttf") : [];

    /// <summary>Logo en PNG con transparencia.</summary>
    public static string LogoPng => Path.Combine(Directory, "logo", "kumquat.png");

    /// <summary>Ícono de la app (varios tamaños).</summary>
    public static string LogoIco => Path.Combine(Directory, "logo", "kumquat.ico");
}

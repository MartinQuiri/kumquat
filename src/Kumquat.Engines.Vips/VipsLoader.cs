// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using NetVips;

namespace Kumquat.Engines.Vips;

/// <summary>
/// Abre imágenes eligiendo el modo de acceso correcto. El acceso secuencial es más rápido
/// y usa menos memoria, pero falla ("out of order read") en cualquier operación que lea
/// fuera de orden: rotar, y por lo tanto aplicar la orientación EXIF de las fotos de celular.
/// </summary>
internal static class VipsLoader
{
    /// <summary>Abre la imagen ya orientada según EXIF.</summary>
    /// <param name="path">Archivo.</param>
    /// <param name="needsRandomAccess">La operación posterior lee fuera de orden (p. ej. rotar).</param>
    /// <param name="options">Opciones del cargador (p. ej. <c>n=-1</c> para animaciones).</param>
    /// <param name="applyOrientation">Aplicar la orientación EXIF a los píxeles.</param>
    public static Image OpenOriented(string path, bool needsRandomAccess, VOption? options = null, bool applyOrientation = true)
    {
        var random = needsRandomAccess || (applyOrientation && NeedsRotation(path));
        using var loaded = Image.NewFromFile(path, access: random ? Enums.Access.Random : Enums.Access.Sequential, kwargs: options);
        return applyOrientation ? loaded.Autorot() : loaded.Copy();
    }

    /// <summary>Lee solo la cabecera: orientación EXIF distinta de 1 implica rotar.</summary>
    private static bool NeedsRotation(string path)
    {
        using var header = Image.NewFromFile(path, access: Enums.Access.Sequential);
        return header.Contains("orientation") && header.Get("orientation") is int orientation && orientation > 1;
    }
}

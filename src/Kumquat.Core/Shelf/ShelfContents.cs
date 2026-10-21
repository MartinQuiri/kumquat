// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Localization;

namespace Kumquat.Core.Shelf;

/// <summary>
/// Los archivos de una bandeja flotante ("Flotar"): referencias a archivos y carpetas que siguen
/// en su lugar, para arrastrarlos después a otra carpeta, un correo o una página. Nada se copia ni
/// se mueve al dejarlos aquí. Sin repetidos y en el orden en que llegaron.
/// </summary>
public sealed class ShelfContents
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private readonly List<string> _items = [];

    /// <summary>Rutas absolutas, en orden de llegada.</summary>
    public IReadOnlyList<string> Items => _items;

    /// <summary>Cantidad de elementos.</summary>
    public int Count => _items.Count;

    /// <summary>"1 archivo" o "N archivos".</summary>
    public string Summary => Count == 1 ? Strings.ShelfItemsOne : Strings.ShelfItems(Count);

    /// <summary>Agrega las rutas que no estaban. Devuelve cuántas se agregaron.</summary>
    public int Add(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var added = 0;
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var full = Path.GetFullPath(path);
            if (!_items.Contains(full, PathComparer))
            {
                _items.Add(full);
                added++;
            }
        }

        return added;
    }

    /// <summary>Quita una ruta de la bandeja (el archivo no se toca).</summary>
    public bool Remove(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var index = _items.FindIndex(p => PathComparer.Equals(p, path));
        if (index < 0)
        {
            return false;
        }

        _items.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Quita lo que ya no existe: lo que se movió al soltarlo en otra carpeta o se borró. Al
    /// mover dentro del mismo disco el Explorador no siempre avisa al origen, así que se mira el disco.
    /// </summary>
    /// <param name="exists">Comprobación de existencia (por defecto, archivo o carpeta en disco).</param>
    /// <returns>Lo que se quitó.</returns>
    public IReadOnlyList<string> RemoveMissing(Func<string, bool>? exists = null)
    {
        exists ??= path => File.Exists(path) || Directory.Exists(path);
        var missing = _items.Where(p => !exists(p)).ToList();
        _ = _items.RemoveAll(missing.Contains);
        return missing;
    }
}

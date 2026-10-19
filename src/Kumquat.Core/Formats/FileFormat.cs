// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Core.Formats;

/// <summary>Un formato de archivo conocido por Kumquat.</summary>
/// <param name="Id">Identificador estable en minúsculas (p. ej. <c>png</c>).</param>
/// <param name="DisplayName">Nombre corto para la rueda (p. ej. <c>PNG</c>).</param>
/// <param name="Category">Familia del formato.</param>
/// <param name="Extensions">Extensiones sin punto, la primera es la preferida.</param>
public sealed record FileFormat(string Id, string DisplayName, FormatCategory Category, IReadOnlyList<string> Extensions)
{
    /// <summary>Extensión preferida para archivos de salida, con punto.</summary>
    public string DefaultExtension => "." + Extensions[0];

    /// <summary>Igualdad por identificador: dos formatos son el mismo si su Id coincide.</summary>
    public bool Equals(FileFormat? other) => other is not null && string.Equals(Id, other.Id, StringComparison.Ordinal);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Id);

    /// <inheritdoc />
    public override string ToString() => Id;
}

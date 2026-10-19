// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Dialogs.Search;

/// <summary>Lo que muestra el buscador.</summary>
/// <param name="Title">Archivo (o "3 archivos").</param>
/// <param name="Subtitle">Formato y tamaño ("PNG · 2,4 MB"), o <c>null</c>.</param>
/// <param name="Icon">Ícono de la familia del archivo, o <c>null</c>.</param>
/// <param name="Items">Acciones, en el orden de la rueda (el orden a igual coincidencia).</param>
public sealed record PaletteRequest(string Title, string? Subtitle, string? Icon, IReadOnlyList<PaletteItem> Items);

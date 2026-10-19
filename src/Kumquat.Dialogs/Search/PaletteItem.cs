// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Design;

namespace Kumquat.Dialogs.Search;

/// <summary>Una acción del buscador.</summary>
/// <param name="Id">Lo que se devuelve al elegirla (id del destino, herramienta o acción).</param>
/// <param name="Title">Texto visible ("Convertir a JPG", "Comprimir").</param>
/// <param name="Group">Grupo ("Convertir", "Herramientas", "Acciones").</param>
/// <param name="Icon">Nombre del ícono en <see cref="Icons"/>, o <c>null</c>.</param>
/// <param name="Color">Color del ícono (el de su familia).</param>
/// <param name="Keywords">Otras palabras con las que se encuentra ("jpeg", "reducir").</param>
public sealed record PaletteItem(string Id, string Title, string Group, string? Icon, Rgb Color, IReadOnlyList<string> Keywords);

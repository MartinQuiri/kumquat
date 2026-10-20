// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Dialogs.Settings;

/// <summary>Lo que se edita en la ventana de configuración.</summary>
/// <param name="Trigger">Id del gesto que abre la rueda al arrastrar (uno de los ofrecidos).</param>
/// <param name="Hotkey">Ctrl+Alt+K abre el buscador.</param>
/// <param name="ContextMenu">"Kumquat…" en el menú del clic derecho del Explorador.</param>
/// <param name="StartWithWindows">Abrir Kumquat al iniciar sesión.</param>
/// <param name="OutputFolder">Carpeta para resultados de archivos sin carpeta (adjuntos, .zip, web).</param>
/// <param name="Language">Idioma de la interfaz: "auto" o un código de <c>Strings.Languages</c>.</param>
public sealed record SettingsValues(string Trigger, bool Hotkey, bool ContextMenu, bool StartWithWindows, string OutputFolder, string Language = "auto");

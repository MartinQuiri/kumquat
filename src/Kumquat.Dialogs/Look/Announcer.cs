// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;

namespace Kumquat.Dialogs.Look;

/// <summary>
/// Hace que el lector de pantalla diga algo que no tiene el foco (la opción elegida en el
/// buscador mientras se escribe, un error de validación). El último anuncio reemplaza al anterior.
/// </summary>
internal static class Announcer
{
    /// <summary>Último texto anunciado (pruebas).</summary>
    internal static string? Last { get; private set; }

    public static void Say(UIElement source, string text)
    {
        ArgumentNullException.ThrowIfNull(source);
        Last = text;
        var peer = UIElementAutomationPeer.FromElement(source) ?? UIElementAutomationPeer.CreatePeerForElement(source);
        peer?.RaiseNotificationEvent(AutomationNotificationKind.Other, AutomationNotificationProcessing.MostRecent, text, "Kumquat");
    }
}

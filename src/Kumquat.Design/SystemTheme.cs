// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Microsoft.Win32;

namespace Kumquat.Design;

/// <summary>Preferencia de tema claro/oscuro de las apps de Windows.</summary>
public static class SystemTheme
{
    /// <summary>Indica si las apps usan el tema oscuro (por defecto, si no se puede leer).</summary>
    public static bool AppsUseDark()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is not int light || light == 0;
    }
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Localization;

namespace Kumquat.App;

/// <summary>
/// Aviso de éxito de un lote como frases completas (cada idioma pluraliza a su manera).
/// </summary>
/// <param name="One">Para un archivo: recibe su nombre.</param>
/// <param name="Many">Para varios: recibe la cantidad.</param>
internal sealed record BatchText(Func<string, string> One, Func<int, string> Many)
{
    /// <summary>"foto.png convertido a PNG" / "3 archivos convertidos a PNG".</summary>
    public static BatchText Converted(string target, string suffix = "")
        => new(file => Strings.NotifyConvertedOne(file, target) + suffix, count => Strings.NotifyConvertedMany(count, target) + suffix);

    /// <summary>"factura.pdf guardado en Escritorio".</summary>
    public static BatchText Saved(string suffix)
        => new(file => Strings.NotifySavedOne(file) + suffix, count => Strings.NotifySavedMany(count) + suffix);

    /// <summary>"foto.jpg listo (Comprimir)".</summary>
    public static BatchText ToolDone(string tool, string suffix = "")
        => new(file => Strings.NotifyToolOne(file, tool) + suffix, count => Strings.NotifyToolMany(count, tool) + suffix);

    /// <summary>"foto.jpg movido a Fotos" / "copiado a Fotos".</summary>
    public static BatchText Transferred(bool move, string folder)
        => move
            ? new(file => Strings.NotifyMovedOne(file, folder), count => Strings.NotifyMovedMany(count, folder))
            : new(file => Strings.NotifyCopiedOne(file, folder), count => Strings.NotifyCopiedMany(count, folder));
}

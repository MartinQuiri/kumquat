// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Formats;
using Kumquat.Core.Localization;
using Kumquat.Engines.Calibre;
using Kumquat.Engines.Office;
using Kumquat.Interaction.Wheel;

namespace Kumquat.App;

/// <summary>
/// Motores externos que no se distribuyen con Kumquat (son grandes y tienen su propio instalador):
/// si se arrastra algo que solo ellos convierten y no están, la rueda ofrece instalarlos.
/// </summary>
internal static class EngineOffers
{
    /// <summary>Prefijo de los ids de "Instalar …".</summary>
    public const string Prefix = "action.install.";

    public static EngineOffer LibreOffice => new(
        Prefix + "libreoffice",
        "LibreOffice",
        "TheDocumentFoundation.LibreOffice",
        Strings.OfferLibreOfficePurpose,
        "The Document Foundation",
        "MPL 2.0",
        [.. OfficeConverter.TextDocuments, .. OfficeConverter.Spreadsheets, .. OfficeConverter.Presentations]);

    public static EngineOffer Calibre => new(
        Prefix + "calibre",
        "Calibre",
        "calibre.calibre",
        Strings.OfferCalibrePurpose,
        "Kovid Goyal",
        "GPL-3.0",
        [.. CalibreConverter.Inputs.Where(f => f != KnownFormats.Docx)]);

    public static IReadOnlyList<EngineOffer> All => [LibreOffice, Calibre];

    /// <summary>winget (App Installer), que trae Windows 10/11; <c>null</c> si no está.</summary>
    public static string? Winget
    {
        get
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "winget.exe");
            return File.Exists(path) ? path : null;
        }
    }

    /// <summary>Oferta por id de acción.</summary>
    public static EngineOffer? ById(string actionId) => All.FirstOrDefault(o => string.Equals(o.ActionId, actionId, StringComparison.Ordinal));

    /// <summary>
    /// Qué ofrecer instalar para estos formatos: solo si no hay a qué convertirlos y un motor
    /// conocido los cubre todos.
    /// </summary>
    public static EngineOffer? For(IReadOnlyCollection<FileFormat> formats, IReadOnlyCollection<FileFormat> targets)
    {
        ArgumentNullException.ThrowIfNull(formats);
        ArgumentNullException.ThrowIfNull(targets);
        return formats.Count == 0 || targets.Count > 0 ? null : All.FirstOrDefault(o => formats.All(o.Formats.Contains));
    }

    /// <summary>Sector "Instalar …" para la rueda.</summary>
    public static WheelItem Item(EngineOffer offer) => WheelItem.Action(offer.ActionId, Strings.InstallItem(offer.Name));
}

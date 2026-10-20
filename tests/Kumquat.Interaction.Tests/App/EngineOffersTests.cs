// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.App;
using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;
using Kumquat.Core.Tools;
using Kumquat.Design;
using Kumquat.Interaction.Wheel;

namespace Kumquat.Interaction.Tests.App;

/// <summary>Ofrecer instalar LibreOffice solo cuando hace falta, y nada sin permiso.</summary>
public sealed class EngineOffersTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("kumquat-offers-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void DocumentosSinConversion_OfreceLibreOffice()
    {
        EngineOffers.For([KnownFormats.Docx], []).ShouldNotBeNull().ActionId.ShouldBe(EngineOffers.LibreOffice.ActionId);
        EngineOffers.For([KnownFormats.Xlsx, KnownFormats.Odp], []).ShouldNotBeNull().ActionId.ShouldBe(EngineOffers.LibreOffice.ActionId);
    }

    [Fact]
    public void KindleSinConversion_OfreceCalibre()
    {
        EngineOffers.For([KnownFormats.Mobi], []).ShouldNotBeNull().ActionId.ShouldBe(EngineOffers.Calibre.ActionId);
        EngineOffers.For([KnownFormats.Azw3, KnownFormats.Fb2], []).ShouldNotBeNull().ActionId.ShouldBe(EngineOffers.Calibre.ActionId);
        EngineOffers.For([KnownFormats.Epub], [KnownFormats.Pdf]).ShouldBeNull("EPUB ya convierte con MuPDF");
        EngineInstaller.Arguments(EngineOffers.Calibre)[2].ShouldBe("calibre.calibre");
    }

    [Fact]
    public void CadaOferta_TieneIconoYPalabras()
    {
        foreach (var offer in EngineOffers.All)
        {
            Icons.ForItem(offer.ActionId).ShouldBe("install");
            CommandKeywords.For(offer.ActionId).ShouldContain("instalar");
        }
    }

    [Fact]
    public void ConConversionOMezclado_NoOfreceNada()
    {
        EngineOffers.For([KnownFormats.Docx], [KnownFormats.Pdf]).ShouldBeNull("ya hay con qué convertir");
        EngineOffers.For([KnownFormats.Docx, KnownFormats.Png], []).ShouldBeNull("LibreOffice no convierte imágenes");
        EngineOffers.For([], []).ShouldBeNull();
    }

    [Fact]
    public void Rueda_SinMotorDeOficina_PrimeraAccionEsInstalar()
    {
        var docx = Path.Combine(_dir, "carta.docx");
        File.WriteAllBytes(docx, OfficeZip("word/document.xml"));
        var targets = new WheelTargets(new ConversionGraph([]), new ToolService([])) { WingetAvailable = () => true };

        var actions = targets.For([docx]).Single(r => r.Kind == WheelRingKind.Actions).Items;

        actions[0].Id.ShouldBe(EngineOffers.LibreOffice.ActionId);
        actions[0].Label.ShouldBe("Instalar LibreOffice");
        actions.Skip(1).Select(i => i.Id).ShouldBe(FileActions.Items.Select(i => i.Id));
        Icons.ForItem(actions[0].Id).ShouldBe("install");
    }

    [Fact]
    public void Rueda_SinWinget_NoOfrece()
    {
        var docx = Path.Combine(_dir, "carta.docx");
        File.WriteAllBytes(docx, OfficeZip("word/document.xml"));
        var targets = new WheelTargets(new ConversionGraph([]), new ToolService([])) { WingetAvailable = () => false };

        targets.For([docx]).Single(r => r.Kind == WheelRingKind.Actions).Items.ShouldNotContain(i => i.Id.StartsWith(EngineOffers.Prefix, StringComparison.Ordinal));
    }

    [Fact]
    public void Winget_PaqueteExactoDelRepositorioOficial()
    {
        EngineInstaller.Arguments(EngineOffers.LibreOffice).ShouldBe(
            ["install", "--id", "TheDocumentFoundation.LibreOffice", "--exact", "--source", "winget", "--accept-package-agreements", "--accept-source-agreements"]);
        EngineInstaller.Succeeded(0).ShouldBeTrue();
        EngineInstaller.Succeeded(unchecked((int)0x8A15002B)).ShouldBeTrue("ya estaba instalado");
        EngineInstaller.Succeeded(1).ShouldBeFalse();
        EngineInstaller.Succeeded(unchecked((int)0x8A150011)).ShouldBeFalse();
    }

    private static byte[] OfficeZip(string mainPart)
    {
        using var memory = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(memory, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("[Content_Types].xml").Open()))
            {
                writer.Write("<Types/>");
            }

            using (var writer = new StreamWriter(zip.CreateEntry(mainPart).Open()))
            {
                writer.Write("<document/>");
            }
        }

        return memory.ToArray();
    }
}

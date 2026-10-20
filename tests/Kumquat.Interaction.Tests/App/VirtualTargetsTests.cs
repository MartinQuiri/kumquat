// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.App;
using Kumquat.Core.Conversion;
using Kumquat.Core.Tools;
using Kumquat.Hosting;
using Kumquat.Interaction.Wheel;
using Microsoft.Extensions.DependencyInjection;

namespace Kumquat.Interaction.Tests.App;

/// <summary>Anillos y etiqueta de lo que se arrastra sin ruta (adjuntos, contenido de un .zip, web).</summary>
public sealed class VirtualTargetsTests : IDisposable
{
    private readonly ServiceProvider _services = new ServiceCollection().AddKumquat(logToFile: false).BuildServiceProvider();

    public void Dispose() => _services.Dispose();

    [Fact]
    public void AdjuntoPdf_ConvertirHerramientasYGuardar()
    {
        var rings = Targets().For(["factura.pdf"]);

        rings.Single(r => r.Kind == WheelRingKind.Convert).Items.ShouldContain(i => i.Id == "txt");
        rings.Single(r => r.Kind == WheelRingKind.Tools).Items.ShouldContain(i => i.Id == "pdf.compress");
        var actions = rings.Single(r => r.Kind == WheelRingKind.Actions).Items.ShouldHaveSingleItem();
        actions.Id.ShouldBe(FileActions.Save);
        actions.Label.ShouldBe("Guardar en Escritorio");
    }

    [Fact]
    public void ExtensionDesconocida_SoloGuardar()
        => Targets().For(["datos.xyz"]).ShouldHaveSingleItem().Items.ShouldHaveSingleItem().Id.ShouldBe(FileActions.Save);

    [Fact]
    public void Etiqueta_DiceDondeSeGuarda()
    {
        var targets = Targets();
        targets.OutputFolder = () => @"D:\Resultados";

        targets.CaptionFor(["factura.pdf"]).ShouldBe("PDF · se guarda en Resultados");
        targets.CaptionFor(["a.jpg", "b.jpg"]).ShouldBe("2 JPG · se guarda en Resultados");
        targets.CaptionFor(["a.jpg", "b.pdf"]).ShouldBe("2 archivos · se guarda en Resultados");
        var rings = targets.For(["a.jpg"]);
        rings[^1].Items.Single().Label.ShouldBe("Guardar en Resultados");
    }

    [Fact]
    public void ArchivosReales_NoSonVirtuales()
    {
        var folder = Directory.CreateTempSubdirectory("kumquat-virtual-").FullName;
        var real = Path.Combine(folder, "real.pdf");
        File.WriteAllText(real, "x");
        try
        {
            WheelTargets.AreVirtual([real]).ShouldBeFalse();
            WheelTargets.AreVirtual([folder]).ShouldBeFalse();
            WheelTargets.AreVirtual(["no-existe.pdf"]).ShouldBeTrue();
            WheelTargets.AreVirtual([]).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private WheelTargets Targets() => new(_services.GetRequiredService<ConversionGraph>(), _services.GetRequiredService<ToolService>());
}

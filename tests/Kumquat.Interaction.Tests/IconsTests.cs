// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.App;
using Kumquat.Core.Formats;
using Kumquat.Design;
using Kumquat.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Kumquat.Interaction.Tests;

public sealed class IconsTests
{
    [Fact]
    public void TodaHerramientaYAccion_TieneIcono()
    {
        using var services = new ServiceCollection().AddKumquat(logToFile: false).BuildServiceProvider();
        var ids = services.GetServices<Kumquat.Core.Tools.ITool>().Select(t => t.Id)
            .Concat(FileActions.Items.Select(i => i.Id));
        foreach (var id in ids)
        {
            var icon = Icons.ForItem(id);
            icon.ShouldNotBeNull($"{id} no tiene ícono");
            IconPaths.Get(icon).ShouldNotBeNull(icon);
        }
    }

    [Fact]
    public void TodaFamiliaDeFormatos_TieneIcono()
    {
        foreach (var category in Enum.GetValues<FormatCategory>())
        {
            IconPaths.Get(Icons.ForCategory(category)).ShouldNotBeNull(category.ToString());
        }
    }

    [Fact]
    public void RecursosDeDiseño_SeCopianJuntoAlEjecutable()
    {
        DesignAssets.FontFiles.Count.ShouldBe(4);
        File.Exists(DesignAssets.LogoPng).ShouldBeTrue();
        File.Exists(DesignAssets.LogoIco).ShouldBeTrue();
        Wheel.WheelFonts.IsInter.ShouldBeTrue();
    }

    [Fact]
    public void IconoDeBandeja_SeCargaDelLogo()
    {
        var icon = Tray.TrayIcon.CreateKumquatIcon();
        icon.ShouldNotBe(0);
        Native.Win32.DestroyIcon(icon);
    }
}

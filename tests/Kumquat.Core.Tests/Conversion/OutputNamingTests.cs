// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;

namespace Kumquat.Core.Tests.Conversion;

public sealed class OutputNamingTests
{
    private const string Dir = @"C:\fotos";

    [Fact]
    public void NombreLibre_UsaMismoNombreConNuevaExtension()
    {
        OutputNaming.ChooseOutputPath(@"C:\fotos\gato.heic", KnownFormats.Jpg, _ => false)
            .ShouldBe(@"C:\fotos\gato.jpg");
    }

    [Fact]
    public void NombreOcupado_AgregaSufijoNumerado()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { @"C:\fotos\gato.jpg", @"C:\fotos\gato (2).jpg" };

        OutputNaming.ChooseOutputPath(@"C:\fotos\gato.heic", KnownFormats.Jpg, taken.Contains)
            .ShouldBe(@"C:\fotos\gato (3).jpg");
    }

    [Fact]
    public void MismaExtension_NuncaDevuelveElOrigen()
    {
        // jpeg -> jpg: el destino coincidiría con el origen si la extensión ya es .jpg
        OutputNaming.ChooseOutputPath(@"C:\fotos\gato.jpg", KnownFormats.Jpg, _ => false)
            .ShouldBe(@"C:\fotos\gato (2).jpg");
    }

    [Fact]
    public void CarpetaDeSalidaExplicita()
    {
        OutputNaming.ChooseOutputPath(@"C:\fotos\gato.png", KnownFormats.WebP, _ => false, @"D:\salida")
            .ShouldBe(@"D:\salida\gato.webp");
    }

    [Fact]
    public void NombreSoloExtension_UsaNombrePorDefecto()
    {
        OutputNaming.ChooseOutputPath(Dir + @"\.png", KnownFormats.Jpg, _ => false)
            .ShouldBe(@"C:\fotos\kumquat.jpg");
    }

    [Fact]
    public void TodoOcupado_Lanza()
    {
        Should.Throw<IOException>(() => OutputNaming.ChooseOutputPath(@"C:\fotos\gato.png", KnownFormats.Jpg, _ => true));
    }
}

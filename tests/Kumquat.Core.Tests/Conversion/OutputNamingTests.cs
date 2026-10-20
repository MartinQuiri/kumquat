// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;

namespace Kumquat.Core.Tests.Conversion;

public sealed class OutputNamingTests
{
    private static readonly string Dir = Rooted("fotos");

    [Fact]
    public void NombreLibre_UsaMismoNombreConNuevaExtension()
    {
        OutputNaming.ChooseOutputPath(Path.Combine(Dir, "gato.heic"), KnownFormats.Jpg, _ => false)
            .ShouldBe(Path.Combine(Dir, "gato.jpg"));
    }

    [Fact]
    public void NombreOcupado_AgregaSufijoNumerado()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.Combine(Dir, "gato.jpg"), Path.Combine(Dir, "gato (2).jpg") };

        OutputNaming.ChooseOutputPath(Path.Combine(Dir, "gato.heic"), KnownFormats.Jpg, taken.Contains)
            .ShouldBe(Path.Combine(Dir, "gato (3).jpg"));
    }

    [Fact]
    public void MismaExtension_NuncaDevuelveElOrigen()
    {
        // jpeg -> jpg: el destino coincidiría con el origen si la extensión ya es .jpg
        OutputNaming.ChooseOutputPath(Path.Combine(Dir, "gato.jpg"), KnownFormats.Jpg, _ => false)
            .ShouldBe(Path.Combine(Dir, "gato (2).jpg"));
    }

    [Fact]
    public void CarpetaDeSalidaExplicita()
    {
        OutputNaming.ChooseOutputPath(Path.Combine(Dir, "gato.png"), KnownFormats.WebP, _ => false, Rooted("salida"))
            .ShouldBe(Path.Combine(Rooted("salida"), "gato.webp"));
    }

    [Fact]
    public void NombreSoloExtension_UsaNombrePorDefecto()
    {
        OutputNaming.ChooseOutputPath(Path.Combine(Dir, ".png"), KnownFormats.Jpg, _ => false)
            .ShouldBe(Path.Combine(Dir, "kumquat.jpg"));
    }

    [Fact]
    public void TodoOcupado_Lanza()
    {
        Should.Throw<IOException>(() => OutputNaming.ChooseOutputPath(Path.Combine(Dir, "gato.png"), KnownFormats.Jpg, _ => true));
    }

    /// <summary>Ruta absoluta del sistema actual: <c>C:\fotos</c> en Windows, <c>/fotos</c> en Linux y macOS.</summary>
    private static string Rooted(string folder) => Path.Combine(OperatingSystem.IsWindows() ? @"C:\" : "/", folder);
}

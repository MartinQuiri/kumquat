// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.App;
using Kumquat.Core.Text;

namespace Kumquat.Interaction.Tests.App;

/// <summary>Etiqueta de formato y tamaño bajo la rueda.</summary>
public sealed class WheelCaptionTests : IDisposable
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly string _dir = Directory.CreateTempSubdirectory("kumquat-caption-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void UnArchivo_FormatoPorContenidoYTamaño()
    {
        // Mal nombrado a propósito: el formato sale del contenido, no de la extensión.
        var png = Write("foto.jpg", PngSignature, 2048);

        WheelTargets.Caption([png]).ShouldBe($"PNG · {FileSizeText.Format(2048 + PngSignature.Length)}");
    }

    [Fact]
    public void VariosDelMismoFormato_CuentaYSuma()
    {
        var a = Write("a.png", PngSignature, 1000);
        var b = Write("b.png", PngSignature, 3000);

        WheelTargets.Caption([a, b]).ShouldBe($"2 PNG · {FileSizeText.Format(4000 + (2 * PngSignature.Length))}");
    }

    [Fact]
    public void FormatosMezclados_YCarpetas()
    {
        var png = Write("a.png", PngSignature, 10);
        var other = Write("notas.xyz", [1, 2, 3], 0);

        WheelTargets.Caption([png, other]).ShouldStartWith("2 archivos · ");
        WheelTargets.Caption([png, _dir]).ShouldStartWith("2 elementos · ");
        WheelTargets.Caption([_dir]).ShouldBe("Carpeta");
        WheelTargets.Caption([other]).ShouldStartWith("XYZ · ");
        WheelTargets.Caption([]).ShouldBeNull();
    }

    private string Write(string name, byte[] header, int padding)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, [.. header, .. new byte[padding]]);
        return path;
    }
}

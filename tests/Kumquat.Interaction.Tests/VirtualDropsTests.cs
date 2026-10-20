// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;
using System.Text;
using Kumquat.Interaction.DragDrop;

namespace Kumquat.Interaction.Tests;

/// <summary>
/// Archivos virtuales con un IDataObject real (el de WPF, que entrega los datos por HGLOBAL como un
/// navegador): nombres, extracción, nombres maliciosos y limpieza.
/// </summary>
[Collection(WpfWindows.Name)]
public sealed class VirtualDropsTests
{
    [Theory]
    [InlineData("factura.pdf", "factura.pdf")]
    [InlineData(@"..\..\Windows\evil.exe", "evil.exe")]
    [InlineData("carpeta/sub/foto.jpg", "foto.jpg")]
    [InlineData("nom<bre>:raro?.txt", "nom_bre__raro_.txt")]
    [InlineData("..", "archivo")]
    [InlineData("   ", "archivo")]
    [InlineData("termina en punto.", "termina en punto")]
    public void NombreSeguro(string name, string expected) => VirtualDrops.SafeName(name).ShouldBe(expected);

    [Fact]
    public void LeeNombres_YExtraeElContenido()
    {
        var content = Encoding.UTF8.GetBytes("%PDF-1.4 contenido de prueba");
        var (names, extracted, inRoot, data) = WithDataObject("factura ñ.pdf", content, pointer =>
        {
            var paths = VirtualDrops.Extract(pointer);
            return (VirtualDrops.ReadNames(pointer), paths, paths.All(VirtualDrops.IsExtracted), paths.Select(File.ReadAllBytes).ToList());
        });

        try
        {
            names.ShouldBe(["factura ñ.pdf"]);
            extracted.ShouldHaveSingleItem().ShouldEndWith("factura ñ.pdf");
            inRoot.ShouldBeTrue();
            data.ShouldHaveSingleItem().ShouldBe(content);
        }
        finally
        {
            VirtualDrops.Discard(extracted);
        }

        File.Exists(extracted[0]).ShouldBeFalse("la copia temporal se borra al terminar");
    }

    [Fact]
    public void NombreMalicioso_NoSaleDeLaCarpetaTemporal()
    {
        var extracted = WithDataObject(@"..\..\..\evil.exe", [1, 2, 3], VirtualDrops.Extract);
        try
        {
            var path = extracted.ShouldHaveSingleItem();
            Path.GetFileName(path).ShouldBe("evil.exe");
            VirtualDrops.IsExtracted(path).ShouldBeTrue();
        }
        finally
        {
            VirtualDrops.Discard(extracted);
        }
    }

    [Fact]
    public void SinArchivosVirtuales_NadaQueLeer()
    {
        var names = OnSta(() =>
        {
            var data = new System.Windows.DataObject(System.Windows.DataFormats.UnicodeText, "solo texto");
            var pointer = Marshal.GetComInterfaceForObject(data, typeof(System.Runtime.InteropServices.ComTypes.IDataObject));
            try
            {
                return VirtualDrops.ReadNames(pointer);
            }
            finally
            {
                Marshal.Release(pointer);
            }
        });

        names.ShouldBeEmpty();
    }

    [Fact]
    public void RutasReales_NoSonExtraidas() => VirtualDrops.IsExtracted(Path.Combine(Path.GetTempPath(), "otra", "a.pdf")).ShouldBeFalse();

    /// <summary>FILEGROUPDESCRIPTORW con un elemento: cItems + FILEDESCRIPTORW (592 bytes, nombre en 72).</summary>
    private static byte[] Descriptor(string name)
    {
        var bytes = new byte[4 + 592];
        BitConverter.GetBytes(1u).CopyTo(bytes, 0);
        Encoding.Unicode.GetBytes(name).CopyTo(bytes, 4 + 72);
        return bytes;
    }

    private static T WithDataObject<T>(string name, byte[] content, Func<nint, T> action)
        => OnSta(() =>
        {
            var data = new System.Windows.DataObject();
            data.SetData("FileGroupDescriptorW", new MemoryStream(Descriptor(name)));
            data.SetData("FileContents", new MemoryStream(content));
            var pointer = Marshal.GetComInterfaceForObject(data, typeof(System.Runtime.InteropServices.ComTypes.IDataObject));
            try
            {
                return action(pointer);
            }
            finally
            {
                Marshal.Release(pointer);
            }
        });

    private static T OnSta<T>(Func<T> action)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
#pragma warning disable CA1031 // Se relanza en el hilo del test.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return failure is null ? result : throw new InvalidOperationException("Falló en el hilo STA.", failure);
    }
}

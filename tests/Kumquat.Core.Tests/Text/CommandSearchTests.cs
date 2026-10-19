// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Text;

namespace Kumquat.Core.Tests.Text;

public sealed class CommandSearchTests
{
    private static readonly (string Title, string[] Keywords)[] Items =
    [
        ("Convertir a JPG", ["jpg", "jpeg", "jfif"]),
        ("Convertir a PNG", ["png"]),
        ("Convertir a PDF", ["pdf"]),
        ("Comprimir", ["reducir", "achicar", "peso"]),
        ("PDF buscable", ["ocr"]),
        ("Extraer páginas", ["sacar"]),
        ("Canción nueva", []),
        ("Copiar ruta", ["path"]),
    ];

    [Theory]
    [InlineData("", "Convertir a JPG")]
    [InlineData("pd", "Convertir a PDF")]
    [InlineData("jpeg", "Convertir a JPG")]
    [InlineData("comp", "Comprimir")]
    [InlineData("achicar", "Comprimir")]
    [InlineData("cancion", "Canción nueva")]
    [InlineData("CANCIÓN", "Canción nueva")]
    [InlineData("ext pag", "Extraer páginas")]
    [InlineData("conv png", "Convertir a PNG")]
    [InlineData("cmpr", "Comprimir")]
    [InlineData("ocr", "PDF buscable")]
    public void PrimerResultado(string query, string expected) => Search(query)[0].ShouldBe(expected);

    [Fact]
    public void SinCoincidencias_ListaVacia() => Search("xyzzy").ShouldBeEmpty();

    [Fact]
    public void BusquedaVacia_TodoEnElOrdenOriginal() => Search("   ").ShouldBe(Items.Select(i => i.Title));

    [Fact]
    public void CadaPalabraTieneQueCoincidir() => Search("pdf zzz").ShouldBeEmpty();

    [Fact]
    public void ConDosLetras_NoBuscaLetrasSueltas()
    {
        // "cr" trae lo que lo contiene ("ocr"), no "Copiar ruta" por la c y la r sueltas.
        Search("cr").ShouldBe(["PDF buscable"]);
        Search("co").ShouldBe(["Convertir a JPG", "Convertir a PNG", "Convertir a PDF", "Comprimir", "Copiar ruta"], ignoreOrder: true);
    }

    [Fact]
    public void PalabraClaveExacta_GanaAlPrefijoDelTitulo()
    {
        // Escribir "pdf" sobre una foto casi siempre es "convertirla a PDF".
        Search("pdf").ShouldBe(["Convertir a PDF", "PDF buscable"]);
    }

    [Theory]
    [InlineData("  Máx.  1920   px ", "max 1920 px")]
    [InlineData("Copiar a…", "copiar a")]
    [InlineData("media.trim", "media trim")]
    public void Normalizar(string text, string expected) => CommandSearch.Normalize(text).ShouldBe(expected);

    private static List<string> Search(string query)
        => CommandSearch.Filter(Items, query, i => i.Title, i => i.Keywords).Select(i => i.Title).ToList();
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using CsCheck;
using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;

namespace Kumquat.Core.Tests.Conversion;

public sealed class ConversionGraphTests
{
    private static readonly FileFormat Png = KnownFormats.Png;
    private static readonly FileFormat Jpg = KnownFormats.Jpg;
    private static readonly FileFormat WebP = KnownFormats.WebP;
    private static readonly FileFormat Bmp = KnownFormats.Bmp;
    private static readonly FileFormat Pdf = KnownFormats.Pdf;

    [Fact]
    public void FindPath_AristaDirecta()
    {
        var graph = new ConversionGraph([Edge(Png, Jpg)]);

        var path = graph.FindPath(Png, Jpg).ShouldNotBeNull();

        path.Edges.Count.ShouldBe(1);
        path.From.ShouldBe(Png);
        path.To.ShouldBe(Jpg);
    }

    [Fact]
    public void FindPath_MultiSalto()
    {
        var graph = new ConversionGraph([Edge(Png, Jpg), Edge(Jpg, Pdf)]);

        var path = graph.FindPath(Png, Pdf).ShouldNotBeNull();

        path.Edges.Select(e => e.To).ShouldBe([Jpg, Pdf]);
    }

    [Fact]
    public void FindPath_EligeLaRutaMasBarata()
    {
        var graph = new ConversionGraph([Edge(Png, Pdf, cost: 10), Edge(Png, Jpg), Edge(Jpg, Pdf)]);

        graph.FindPath(Png, Pdf).ShouldNotBeNull().Cost.ShouldBe(2);
    }

    [Fact]
    public void FindPath_RespetaLimiteDeSaltos()
    {
        var graph = new ConversionGraph([Edge(Png, Jpg), Edge(Jpg, WebP), Edge(WebP, Bmp)]);

        graph.FindPath(Png, Bmp, maxHops: 2).ShouldBeNull();
        graph.FindPath(Png, Bmp, maxHops: 3).ShouldNotBeNull();
    }

    [Fact]
    public void FindPath_ConLimite_PrefiereRutaMasCaraSiLaBarataEsMasLarga()
    {
        // Png->Jpg->WebP->Bmp cuesta 3 pero usa 3 saltos; Png->Bmp cuesta 5 en 1 salto.
        var graph = new ConversionGraph([Edge(Png, Jpg), Edge(Jpg, WebP), Edge(WebP, Bmp), Edge(Png, WebP, cost: 4), Edge(Png, Bmp, cost: 5)]);

        graph.FindPath(Png, Bmp, maxHops: 3).ShouldNotBeNull().Cost.ShouldBe(3);
        graph.FindPath(Png, Bmp, maxHops: 2).ShouldNotBeNull().Cost.ShouldBe(5);
    }

    [Fact]
    public void FindPath_MismoFormato_DevuelveNull()
    {
        new ConversionGraph([Edge(Png, Jpg)]).FindPath(Png, Png).ShouldBeNull();
    }

    [Fact]
    public void Constructor_AristasDuplicadas_GanaLaMasBarata()
    {
        var graph = new ConversionGraph([Edge(Png, Jpg, "caro", 5), Edge(Png, Jpg, "barato", 1)]);

        graph.DirectEdgesFrom(Png).ShouldHaveSingleItem().ConverterId.ShouldBe("barato");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Constructor_CostoInvalido_Lanza(double cost)
    {
        Should.Throw<ArgumentException>(() => new ConversionGraph([Edge(Png, Jpg, cost: cost)]));
    }

    [Fact]
    public void Targets_OrdenadosPorCosto_SinElOrigen()
    {
        var graph = new ConversionGraph([Edge(Png, Pdf, cost: 3), Edge(Png, Jpg), Edge(Jpg, Png)]);

        graph.Targets(Png).Select(p => p.To).ShouldBe([Jpg, Pdf]);
    }

    [Fact]
    public void CommonTargets_InterseccionConservandoOrden()
    {
        var graph = new ConversionGraph([Edge(Png, Jpg), Edge(Png, Pdf, cost: 2), Edge(Png, WebP, cost: 3), Edge(Bmp, WebP), Edge(Bmp, Pdf)]);

        graph.CommonTargets([Png, Bmp]).ShouldBe([Pdf, WebP]);
        graph.CommonTargets([]).ShouldBeEmpty();
    }

    [Fact]
    public void FromConverters_IgnoraConvertidoresNoDisponibles()
    {
        var graph = ConversionGraph.FromConverters([
            new FakeConverter("ok", available: true, Edge(Png, Jpg, "ok")),
            new FakeConverter("ausente", available: false, Edge(Png, Pdf, "ausente")),
        ]);

        graph.Targets(Png).Select(p => p.To).ShouldBe([Jpg]);
    }

    [Fact]
    public void Propiedad_TodaRutaEsUnaCadenaValidaSinCiclosYDentroDelLimite()
    {
        var formats = KnownFormats.All.Take(12).ToArray();
        var genEdge = Gen.Select(Gen.Int[0, formats.Length - 1], Gen.Int[0, formats.Length - 1], Gen.Double[0.1, 10])
            .Where(t => t.Item1 != t.Item2)
            .Select(t => Edge(formats[t.Item1], formats[t.Item2], cost: t.Item3));

        Gen.Select(genEdge.Array[0, 40], Gen.Int[1, 4]).Sample((edges, maxHops) =>
        {
            var graph = new ConversionGraph(edges);
            foreach (var from in formats)
            {
                foreach (var path in graph.Targets(from, maxHops))
                {
                    path.Edges.Count.ShouldBeInRange(1, maxHops);
                    path.From.ShouldBe(from);
                    for (var i = 1; i < path.Edges.Count; i++)
                    {
                        path.Edges[i].From.ShouldBe(path.Edges[i - 1].To);
                    }

                    var visited = path.Edges.Select(e => e.From).Append(path.To).ToList();
                    visited.Distinct().Count().ShouldBe(visited.Count);

                    // Ninguna arista directa disponible puede ser más barata que la ruta elegida.
                    var direct = graph.DirectEdgesFrom(from).FirstOrDefault(e => e.To == path.To);
                    if (direct is not null)
                    {
                        path.Cost.ShouldBeLessThanOrEqualTo(direct.Cost + 1e-9);
                    }
                }
            }
        });
    }

    private static ConversionEdge Edge(FileFormat from, FileFormat to, string converter = "fake", double cost = 1)
        => new(from, to, converter, cost);
}

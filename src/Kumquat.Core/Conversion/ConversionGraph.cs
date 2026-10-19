// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Collections.Frozen;
using Kumquat.Core.Formats;

namespace Kumquat.Core.Conversion;

/// <summary>
/// Grafo inmutable de capacidades: nodos = formatos, aristas = conversiones directas.
/// Es la única fuente de verdad sobre qué destinos muestra la rueda.
/// </summary>
public sealed class ConversionGraph
{
    /// <summary>Saltos máximos por defecto: más allá la pérdida de calidad acumulada no compensa.</summary>
    public const int DefaultMaxHops = 3;

    private readonly FrozenDictionary<FileFormat, ConversionEdge[]> _outgoing;

    /// <summary>Construye el grafo a partir de aristas sueltas.</summary>
    /// <param name="edges">Aristas. Si dos convertidores ofrecen la misma, gana la de menor costo.</param>
    public ConversionGraph(IEnumerable<ConversionEdge> edges)
    {
        ArgumentNullException.ThrowIfNull(edges);
        var list = edges.ToList();
        var invalid = list.Find(e => !(e.Cost > 0) || double.IsInfinity(e.Cost));
        if (invalid is not null)
        {
            throw new ArgumentException($"La arista {invalid.From}->{invalid.To} de '{invalid.ConverterId}' tiene un costo no positivo o infinito.", nameof(edges));
        }

        _outgoing = list
            .Where(e => e.From != e.To)
            .GroupBy(e => (e.From, e.To))
            .Select(g => g.MinBy(e => e.Cost)!)
            .GroupBy(e => e.From)
            .ToFrozenDictionary(g => g.Key, g => g.OrderBy(e => e.Cost).ThenBy(e => e.To.Id, StringComparer.Ordinal).ToArray());
    }

    /// <summary>Todas las aristas directas del grafo.</summary>
    public IEnumerable<ConversionEdge> Edges => _outgoing.Values.SelectMany(e => e);

    /// <summary>Crea el grafo con los convertidores disponibles.</summary>
    public static ConversionGraph FromConverters(IEnumerable<IConverter> converters)
    {
        ArgumentNullException.ThrowIfNull(converters);
        return new ConversionGraph(converters.Where(c => c.IsAvailable).SelectMany(c => c.Edges));
    }

    /// <summary>Aristas directas que salen de un formato.</summary>
    public IReadOnlyList<ConversionEdge> DirectEdgesFrom(FileFormat from)
        => _outgoing.TryGetValue(from, out var edges) ? edges : [];

    /// <summary>
    /// Ruta más barata entre dos formatos (Dijkstra con límite de saltos).
    /// </summary>
    /// <returns>La ruta, o <c>null</c> si no existe dentro del límite.</returns>
    public ConversionPath? FindPath(FileFormat from, FileFormat to, int maxHops = DefaultMaxHops)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxHops, 1);
        return from == to ? null : ShortestPaths(from, maxHops).GetValueOrDefault(to);
    }

    /// <summary>
    /// Destinos alcanzables desde un formato, ordenados por costo de ruta.
    /// </summary>
    public IReadOnlyList<ConversionPath> Targets(FileFormat from, int maxHops = DefaultMaxHops)
    {
        ArgumentNullException.ThrowIfNull(from);
        return [.. ShortestPaths(from, maxHops).Values
            .OrderBy(p => p.Cost)
            .ThenBy(p => p.Edges.Count)
            .ThenBy(p => p.To.Id, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Destinos comunes a varios formatos de entrada (para soltar varios archivos a la vez).
    /// </summary>
    public IReadOnlyList<FileFormat> CommonTargets(IReadOnlyCollection<FileFormat> sources, int maxHops = DefaultMaxHops)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (sources.Count == 0)
        {
            return [];
        }

        var perSource = sources.Distinct().Select(s => Targets(s, maxHops)).ToList();
        var common = perSource
            .Skip(1)
            .Aggregate(
                perSource[0].Select(p => p.To).ToHashSet(),
                (acc, targets) =>
                {
                    acc.IntersectWith(targets.Select(p => p.To));
                    return acc;
                });

        // Conserva el orden del primer origen, que ya está ordenado por costo.
        return [.. perSource[0].Select(p => p.To).Where(common.Contains)];
    }

    private Dictionary<FileFormat, ConversionPath> ShortestPaths(FileFormat from, int maxHops)
    {
        // Estado = (formato, saltos usados). Con el límite de saltos, el mejor costo por
        // formato no basta: una ruta más cara pero más corta puede habilitar destinos más lejanos.
        var best = new Dictionary<(FileFormat Format, int Hops), (double Cost, ConversionEdge[] Path)>();
        var queue = new PriorityQueue<(FileFormat Format, int Hops, ConversionEdge[] Path), double>();
        queue.Enqueue((from, 0, []), 0);

        var result = new Dictionary<FileFormat, ConversionPath>();
        while (queue.TryDequeue(out var state, out var cost))
        {
            if (state.Format != from && !result.ContainsKey(state.Format))
            {
                result[state.Format] = new ConversionPath(state.Path);
            }

            if (state.Hops == maxHops)
            {
                continue;
            }

            foreach (var edge in DirectEdgesFrom(state.Format))
            {
                if (edge.To == from || state.Path.Any(e => e.From == edge.To))
                {
                    continue; // sin ciclos
                }

                var nextCost = cost + edge.Cost;
                var key = (edge.To, state.Hops + 1);
                if (best.TryGetValue(key, out var known) && known.Cost <= nextCost)
                {
                    continue;
                }

                ConversionEdge[] nextPath = [.. state.Path, edge];
                best[key] = (nextCost, nextPath);
                queue.Enqueue((edge.To, state.Hops + 1, nextPath), nextCost);
            }
        }

        return result;
    }
}

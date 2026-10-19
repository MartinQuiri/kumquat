// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Conversion;
using Kumquat.Core.Results;

namespace Kumquat.Core.Tests.Conversion;

/// <summary>Convertidor de prueba: copia la entrada y añade una marca por paso.</summary>
internal sealed class FakeConverter : IConverter
{
    private readonly Func<ConversionStep, CancellationToken, Task<Result>>? _behavior;

    public FakeConverter(string id, bool available, params ConversionEdge[] edges)
        : this(id, available, null, edges)
    {
    }

    public FakeConverter(string id, bool available, Func<ConversionStep, CancellationToken, Task<Result>>? behavior, params ConversionEdge[] edges)
    {
        Id = id;
        IsAvailable = available;
        Edges = edges;
        _behavior = behavior;
    }

    public string Id { get; }

    public bool IsAvailable { get; }

    public IReadOnlyCollection<ConversionEdge> Edges { get; }

    public List<ConversionStep> Calls { get; } = [];

    public async Task<Result> ConvertAsync(ConversionStep step, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        lock (Calls)
        {
            Calls.Add(step);
        }

        if (_behavior is not null)
        {
            return await _behavior(step, cancellationToken);
        }

        var content = await File.ReadAllTextAsync(step.InputPath, cancellationToken);
        progress?.Report(0.5);
        await File.WriteAllTextAsync(step.OutputPath, $"{content}>{step.Edge.To.Id}", cancellationToken);
        return Result.Success;
    }
}

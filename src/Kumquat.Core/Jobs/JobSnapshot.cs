// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Conversion;
using Kumquat.Core.Results;

namespace Kumquat.Core.Jobs;

/// <summary>Instantánea inmutable de un trabajo, apta para la UI.</summary>
/// <param name="Id">Identificador del trabajo.</param>
/// <param name="Request">Pedido original.</param>
/// <param name="State">Estado actual.</param>
/// <param name="Progress">Avance entre 0 y 1.</param>
/// <param name="OutputPath">Salida, si terminó bien.</param>
/// <param name="Error">Error, si falló.</param>
public sealed record JobSnapshot(Guid Id, ConversionRequest Request, JobState State, double Progress, string? OutputPath, Error? Error);

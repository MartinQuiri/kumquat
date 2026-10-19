// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Diagnostics.CodeAnalysis;

namespace Kumquat.Core.Results;

/// <summary>Resultado de una operación que no produce valor.</summary>
public sealed class Result
{
    private Result(Error? error) => Error = error;

    /// <summary>Resultado exitoso compartido.</summary>
    public static Result Success { get; } = new(null);

    /// <summary>Error, si la operación falló.</summary>
    public Error? Error { get; }

    /// <summary>Indica si la operación terminó bien.</summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    /// <summary>Crea un resultado fallido.</summary>
    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(error);
    }

    /// <summary>Convierte implícitamente un error en resultado fallido.</summary>
    public static implicit operator Result(Error error) => Failure(error);
}

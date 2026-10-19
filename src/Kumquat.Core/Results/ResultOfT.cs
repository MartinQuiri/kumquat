// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Diagnostics.CodeAnalysis;

namespace Kumquat.Core.Results;

/// <summary>Resultado de una operación que produce un valor.</summary>
/// <typeparam name="T">Tipo del valor.</typeparam>
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Fábricas Success/Failure: patrón idiomático de Result<T>.")]
[SuppressMessage("Design", "MA0048:File name must match type name", Justification = "Convención T-sufijo para el genérico de Result.")]
public sealed class Result<T>
{
    private Result(T? value, Error? error)
    {
        Value = value;
        Error = error;
    }

    /// <summary>Valor, si la operación terminó bien.</summary>
    public T? Value { get; }

    /// <summary>Error, si la operación falló.</summary>
    public Error? Error { get; }

    /// <summary>Indica si la operación terminó bien.</summary>
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    /// <summary>Crea un resultado exitoso.</summary>
    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(value, null);
    }

    /// <summary>Crea un resultado fallido.</summary>
    public static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(default, error);
    }

    /// <summary>Convierte implícitamente un valor en resultado exitoso.</summary>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>Convierte implícitamente un error en resultado fallido.</summary>
    public static implicit operator Result<T>(Error error) => Failure(error);
}

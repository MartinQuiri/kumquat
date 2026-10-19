// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Dialogs;

/// <summary>
/// Errores inesperados en tareas de las ventanas (vista previa, reproducción). Las ventanas los
/// atrapan para no quedar en un estado roto; la app los registra en el log.
/// </summary>
public static class DialogDiagnostics
{
    /// <summary>Recibe qué estaba haciendo la ventana y el error. Lo asigna la app.</summary>
    public static Action<string, Exception>? Error { get; set; }

    /// <summary>
    /// Indica si una excepción es un fallo esperable de una tarea de UI (y se puede atrapar sin
    /// ocultar errores graves como falta de memoria).
    /// </summary>
    internal static bool IsRecoverable(Exception exception)
        => exception is not (OperationCanceledException or OutOfMemoryException or StackOverflowException or AccessViolationException);

    internal static void Report(string activity, Exception exception) => Error?.Invoke(activity, exception);
}

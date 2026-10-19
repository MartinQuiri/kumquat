// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.ComponentModel;
using System.Diagnostics;

namespace Kumquat.Infrastructure.Processes;

/// <summary>
/// Lanza motores sin shell, con <see cref="ProcessStartInfo.ArgumentList"/>, dentro de un
/// Job Object (memoria acotada y muerte garantizada) y con prioridad baja para no
/// entorpecer la interacción.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    private const int StderrTailLines = 40;

    /// <inheritdoc />
    public async Task<ProcessOutcome> RunAsync(ProcessSpec spec, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (!Path.IsPathFullyQualified(spec.FileName))
        {
            throw new ArgumentException("El ejecutable debe ser una ruta absoluta (evita el secuestro por PATH).", nameof(spec));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var startInfo = new ProcessStartInfo(spec.FileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,

            // FFmpeg y la mayoría de motores escriben UTF-8; sin esto .NET usa la página OEM
            // y los nombres con ñ, tildes o kanji llegan corruptos a los logs y parsers.
            StandardOutputEncoding = spec.OutputEncoding,
            StandardErrorEncoding = spec.OutputEncoding,
            WorkingDirectory = spec.WorkingDirectory ?? Path.GetDirectoryName(spec.FileName)!,
        };
        foreach (var argument in spec.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var job = new JobObject(spec.MaxMemoryBytes);
        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException($"No se pudo iniciar '{spec.FileName}'.");
        }

        // Ventana mínima entre Start y Assign: los motores no crean hijos tan rápido.
        // Para motores que sí lo hacen (LibreOffice) habrá que lanzar suspendido con CreateProcess.
        job.Assign(process.SafeHandle);
        TryLowerPriority(process);
        process.StandardInput.Close();

        var stderrTail = new Queue<string>(StderrTailLines);
        var stdoutPump = PumpAsync(process.StandardOutput, spec.OnStandardOutputLine, null);
        var stderrPump = PumpAsync(process.StandardError, spec.OnStandardErrorLine, stderrTail);

        using var timeout = spec.Timeout is { } t ? new CancellationTokenSource(t) : new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            await process.WaitForExitAsync(linked.Token);
            await Task.WhenAll(stdoutPump, stderrPump);
            return new ProcessOutcome(process.ExitCode, Tail(stderrTail), TimedOut: false);
        }
        catch (OperationCanceledException)
        {
            job.Terminate(exitCode: 1);
            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(stdoutPump, stderrPump);
            cancellationToken.ThrowIfCancellationRequested();
            return new ProcessOutcome(-1, Tail(stderrTail), TimedOut: true);
        }
    }

    private static void TryLowerPriority(Process process)
    {
        try
        {
            process.PriorityClass = ProcessPriorityClass.BelowNormal;
        }
        catch (Win32Exception)
        {
            // El proceso pudo haber terminado ya: no es un error.
        }
        catch (InvalidOperationException)
        {
            // Idem.
        }
    }

    private static async Task PumpAsync(StreamReader reader, Action<string>? onLine, Queue<string>? tail)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            onLine?.Invoke(line);
            if (tail is not null)
            {
                lock (tail)
                {
                    if (tail.Count == StderrTailLines)
                    {
                        tail.Dequeue();
                    }

                    tail.Enqueue(line);
                }
            }
        }
    }

    private static string Tail(Queue<string> tail)
    {
        lock (tail)
        {
            return string.Join(Environment.NewLine, tail);
        }
    }
}

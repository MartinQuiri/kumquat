// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Jobs;
using Kumquat.Core.Localization;

namespace Kumquat.App;

/// <summary>
/// Agrupa los trabajos de un mismo drop y produce una sola notificación al terminar
/// ("3 archivos convertidos a PNG"), en vez de una por archivo.
/// </summary>
internal sealed class BatchNotifier(Action<string, string, bool> notify)
{
    private readonly Dictionary<Guid, Batch> _byJob = [];

    /// <param name="jobIds">Trabajos del drop.</param>
    /// <param name="text">Aviso de éxito: para un archivo (con su nombre) y para varios (con la cantidad).</param>
    /// <param name="completed">Se llama una vez al terminar el lote (p. ej. borrar copias temporales).</param>
    public void Track(IReadOnlyCollection<Guid> jobIds, BatchText text, Action? completed = null)
    {
        ArgumentNullException.ThrowIfNull(jobIds);
        ArgumentNullException.ThrowIfNull(text);
        if (jobIds.Count == 0)
        {
            completed?.Invoke();
            return;
        }

        var batch = new Batch(text, jobIds.Count) { Completed = completed };
        foreach (var id in jobIds)
        {
            _byJob[id] = batch;
        }
    }

    /// <summary>Debe llamarse en el hilo de UI.</summary>
    public void OnJobChanged(JobSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.State is not (JobState.Succeeded or JobState.Failed or JobState.Cancelled) || !_byJob.Remove(snapshot.Id, out var batch))
        {
            return;
        }

        batch.Record(snapshot);
        if (!batch.IsComplete)
        {
            return;
        }

        batch.Completed?.Invoke();
        if (batch.Failed == 0)
        {
            var message = batch.Succeeded == 1 ? batch.Text.One(Path.GetFileName(batch.LastOutput) ?? string.Empty) : batch.Text.Many(batch.Succeeded);
            notify(Strings.NotifyDone, message, false);
        }
        else
        {
            notify(
                batch.Succeeded == 0 ? Strings.NotifyFailedAll : Strings.NotifyPartial(batch.Succeeded, batch.Total),
                batch.FirstError ?? Strings.ErrorUnknown,
                true);
        }
    }

    private sealed class Batch(BatchText text, int total)
    {
        private int _done;

        public BatchText Text { get; } = text;

        public Action? Completed { get; init; }

        public int Total { get; } = total;

        public int Succeeded { get; private set; }

        public int Failed { get; private set; }

        public string? LastOutput { get; private set; }

        public string? FirstError { get; private set; }

        public bool IsComplete => _done == Total;

        public void Record(JobSnapshot snapshot)
        {
            _done++;
            if (snapshot.State == JobState.Succeeded)
            {
                Succeeded++;
                LastOutput = snapshot.OutputPath;
            }
            else
            {
                Failed++;
                FirstError ??= $"{Path.GetFileName(snapshot.Request.InputPath)}: {snapshot.Error?.Message}";
            }
        }
    }
}

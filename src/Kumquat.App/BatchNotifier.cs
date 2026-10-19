// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Globalization;
using Kumquat.Core.Jobs;

namespace Kumquat.App;

/// <summary>
/// Agrupa los trabajos de un mismo drop y produce una sola notificación al terminar
/// ("3 archivos convertidos a PNG"), en vez de una por archivo.
/// </summary>
internal sealed class BatchNotifier(Action<string, string, bool> notify)
{
    private readonly Dictionary<Guid, Batch> _byJob = [];

    public void Track(IReadOnlyCollection<Guid> jobIds, string targetName)
    {
        ArgumentNullException.ThrowIfNull(jobIds);
        var batch = new Batch(targetName, jobIds.Count);
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

        if (batch.Failed == 0)
        {
            var what = batch.Succeeded == 1 ? Path.GetFileName(batch.LastOutput) : string.Create(CultureInfo.CurrentCulture, $"{batch.Succeeded} archivos");
            notify("Listo", $"{what} convertido{(batch.Succeeded == 1 ? string.Empty : "s")} a {batch.TargetName}", false);
        }
        else
        {
            notify(
                batch.Succeeded == 0 ? "No se pudo convertir" : string.Create(CultureInfo.CurrentCulture, $"{batch.Succeeded} de {batch.Total} convertidos"),
                batch.FirstError ?? "Error desconocido",
                true);
        }
    }

    private sealed class Batch(string targetName, int total)
    {
        private int _done;

        public string TargetName { get; } = targetName;

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

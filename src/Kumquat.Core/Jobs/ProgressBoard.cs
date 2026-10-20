// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Core.Jobs;

/// <summary>
/// Estado del panel de progreso a partir de los avisos de <see cref="JobRunner"/>: qué trabajos
/// hay, cuánto les falta y si el panel se ve. Lógica pura (el reloj se inyecta): la ventana solo dibuja.
/// </summary>
/// <remarks>
/// El panel aparece cuando algo lleva más de <see cref="ShowDelay"/> (lo instantáneo no lo hace
/// parpadear) y se va <see cref="HideDelay"/> después de que termina todo. El tiempo restante sale
/// del ritmo de avance desde el primer progreso informado, suavizado para que no salte.
/// </remarks>
public sealed class ProgressBoard(TimeProvider time)
{
    /// <summary>Filas como máximo (el resto se resume como "y N más").</summary>
    public const int MaxRows = 4;

    /// <summary>Cuánto tiene que durar un trabajo para que aparezca el panel.</summary>
    public static readonly TimeSpan ShowDelay = TimeSpan.FromMilliseconds(700);

    /// <summary>Cuánto queda a la vista después de terminar todo.</summary>
    public static readonly TimeSpan HideDelay = TimeSpan.FromSeconds(1.5);

    /// <summary>Antes de esto, o con menos avance que <see cref="MinProgressForEstimate"/>, no se estima.</summary>
    public static readonly TimeSpan MinElapsedForEstimate = TimeSpan.FromSeconds(1.5);

    /// <summary>Avance mínimo para estimar (con menos, el ritmo todavía no dice nada).</summary>
    public const double MinProgressForEstimate = 0.03;

    private const double Smoothing = 0.3;

    private readonly Dictionary<Guid, Entry> _active = [];
    private readonly List<Guid> _order = [];
    private DateTimeOffset? _lastActivityEnded;
    private bool _shown;
    private bool _dismissed;

    /// <summary>"Ocultar": el panel no vuelve hasta que termine lo que está en curso (el próximo lote sí lo muestra).</summary>
    public void Dismiss() => _dismissed = _active.Count > 0;

    /// <summary>Registra un cambio de un trabajo (en el hilo de UI).</summary>
    public void OnJobChanged(JobSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var now = time.GetUtcNow();
        switch (snapshot.State)
        {
            case JobState.Queued:
                if (!_active.ContainsKey(snapshot.Id))
                {
                    _active[snapshot.Id] = new Entry(snapshot.Request, now);
                    _order.Add(snapshot.Id);
                }

                break;
            case JobState.Running:
                if (!_active.TryGetValue(snapshot.Id, out var entry))
                {
                    entry = new Entry(snapshot.Request, now);
                    _active[snapshot.Id] = entry;
                    _order.Add(snapshot.Id);
                }

                entry.Report(snapshot.Progress, now);
                break;
            default:
                if (_active.Remove(snapshot.Id))
                {
                    _order.Remove(snapshot.Id);
                }

                if (_active.Count == 0)
                {
                    _lastActivityEnded = now;
                    _dismissed = false;
                }

                break;
        }
    }

    /// <summary>Lo que hay que mostrar ahora.</summary>
    public ProgressView View()
    {
        var now = time.GetUtcNow();
        if (_active.Count > 0)
        {
            _lastActivityEnded = null;
            _shown |= _active.Values.Any(e => now - e.Added >= ShowDelay);
        }
        else if (_lastActivityEnded is { } ended && now - ended >= HideDelay)
        {
            _shown = false;
        }

        var ordered = _order.Select(id => (Id: id, Entry: _active[id])).OrderByDescending(e => e.Entry.IsRunning).ToList();
        var rows = ordered.Take(MaxRows)
            .Select(e => new ProgressRow(e.Id, Path.GetFileName(e.Entry.Request.InputPath), e.Entry.Request.Description, e.Entry.IsRunning, e.Entry.Progress, e.Entry.Remaining(now)))
            .ToList();
        var running = ordered.Count(e => e.Entry.IsRunning);
        return new ProgressView(_shown && !_dismissed, rows, running, ordered.Count - running, Math.Max(0, ordered.Count - MaxRows));
    }

    private sealed class Entry(JobRequest request, DateTimeOffset added)
    {
        private DateTimeOffset? _firstProgress;
        private DateTimeOffset _lastReport;
        private double _firstValue;
        private double? _rate; // fracción por segundo, suavizada

        public JobRequest Request { get; } = request;

        public DateTimeOffset Added { get; } = added;

        public bool IsRunning { get; private set; }

        public double? Progress { get; private set; }

        public void Report(double value, DateTimeOffset now)
        {
            IsRunning = true;
            if (value <= 0 || double.IsNaN(value))
            {
                return; // "empezó" sin avance: todavía no se sabe cuánto falta.
            }

            value = Math.Min(value, 1);
            Progress = Math.Max(Progress ?? 0, value);
            _lastReport = now;
            if (_firstProgress is null)
            {
                _firstProgress = now;
                _firstValue = value;
                return;
            }

            var seconds = (now - _firstProgress.Value).TotalSeconds;
            if (seconds > 0 && Progress > _firstValue)
            {
                var rate = (Progress.Value - _firstValue) / seconds;
                _rate = _rate is { } previous ? previous + (Smoothing * (rate - previous)) : rate;
            }
        }

        public TimeSpan? Remaining(DateTimeOffset now)
        {
            if (Progress is not { } done || _rate is not { } rate || rate <= 0 || done < MinProgressForEstimate || now - Added < MinElapsedForEstimate)
            {
                return null;
            }

            // Desde el último aviso también corre el reloj (un motor que informa poco no "congela" el tiempo).
            var seconds = Math.Max(0, ((1 - done) / rate) - (now - _lastReport).TotalSeconds);
            return TimeSpan.FromSeconds(Math.Min(seconds, TimeSpan.FromDays(1).TotalSeconds));
        }
    }
}

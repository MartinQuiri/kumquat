// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Diagnostics;
using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;
using Kumquat.Core.Jobs;
using Kumquat.Core.Previews;
using Kumquat.Core.Tools;
using Kumquat.Dialogs;
using Kumquat.Hosting;
using Kumquat.Infrastructure.Files;
using Kumquat.Interaction;
using Kumquat.Interaction.Input;
using Kumquat.Interaction.Native;
using Kumquat.Interaction.Tray;
using Kumquat.Interaction.Wheel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kumquat.App;

/// <summary>Aplicación de bandeja: une la rueda, la cola de trabajos y las notificaciones.</summary>
internal sealed partial class TrayApplication : IDisposable
{
    private static readonly string SettingsPath = Path.Combine(KumquatServices.DataDirectory, "settings.json");

    private readonly IServiceProvider _services;
    private readonly UiThread _ui;
    private readonly ILogger<TrayApplication> _logger;
    private readonly JobRunner _jobs;
    private readonly WheelTargets _targets;
    private readonly ConversionService _conversions;
    private readonly ToolService _tools;
    private readonly IPreviewService _previews;
    private readonly BatchNotifier _notifier;
    private AppSettings _settings;
    private WheelController? _wheel;
    private TrayIcon? _tray;

    public TrayApplication(IServiceProvider services, UiThread ui)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _logger = services.GetRequiredService<ILogger<TrayApplication>>();
        _jobs = services.GetRequiredService<JobRunner>();
        _conversions = services.GetRequiredService<ConversionService>();
        _tools = services.GetRequiredService<ToolService>();
        _previews = services.GetRequiredService<IPreviewService>();
        _targets = new WheelTargets(services.GetRequiredService<ConversionGraph>(), _tools);
        _notifier = new BatchNotifier((title, message, isError) => _tray?.Notify(title, message, isError));
        _settings = AppSettings.Load(SettingsPath);
    }

    public void Start()
    {
        _jobs.JobChanged += (_, e) => _ui.Post(() => _notifier.OnJobChanged(e.Snapshot));

        _tray = new TrayIcon("Kumquat: arrastra un archivo y presiona Shift", BuildMenu);
        _wheel = new WheelController(_targets.For, OnChosen, _settings.Trigger) { Trace = LogInteraction, NothingSelected = OnNothingSelected };
        _wheel.Start();
        var hotkeyActive = _settings.HotkeyEnabled && _wheel.SetHotkey(Hotkey.Default);
        LogStarted(_settings.Trigger);

        // Kumquat no tiene ventana: sin este aviso, abrir el .exe parece no hacer nada.
        var hotkeyHint = hotkeyActive ? $" O selecciona archivos y presiona {Hotkey.Default.Display}." : string.Empty;
        _tray.Notify("Kumquat está activo", $"Arrastra un archivo y, sin soltarlo, presiona {TriggerName(_settings.Trigger)}.{hotkeyHint}");
    }

    public void Dispose()
    {
        _wheel?.Dispose();
        _tray?.Dispose();
    }

    private void OnChosen(IReadOnlyList<string> files, WheelItem item)
    {
        switch (item.Ring)
        {
            case WheelRingKind.Convert:
                Enqueue(files.Select(f => _conversions.CreateJob(new ConversionRequest(f, item.Target!))), "convertido", $"a {item.Label}", item.Id);
                break;
            case WheelRingKind.Tools:
                // Fuera del Drop: puede abrir la ventana de opciones.
                _ui.Post(() => StartTool(files, item));
                break;
            case WheelRingKind.Actions when item.Id is FileActions.CopyTo or FileActions.MoveTo:
                // Fuera del Drop: un diálogo modal dentro del Drop congelaría el arrastre del Explorador.
                _ui.Post(() => TransferTo(files, move: string.Equals(item.Id, FileActions.MoveTo, StringComparison.Ordinal)));
                break;
            case WheelRingKind.Actions:
                if (FileActions.Execute(item.Id, files) is { } message)
                {
                    _tray?.Notify("Kumquat", message);
                }

                LogEnqueued(files.Count, item.Id);
                break;
        }
    }

    /// <summary>
    /// Lee los parámetros en segundo plano (duración, metadatos actuales: lanza procesos) y vuelve
    /// al hilo de UI para mostrar la ventana de opciones, si la herramienta la necesita.
    /// </summary>
    private void StartTool(IReadOnlyList<string> files, WheelItem item)
    {
        _ = Task.Run(() => _tools.GetParametersAsync(files, item.Id, CancellationToken.None))
            .ContinueWith(task => _ui.Post(() => AskAndEnqueue(files, item, task)), TaskScheduler.Default);
    }

    private void AskAndEnqueue(IReadOnlyList<string> files, WheelItem item, Task<IReadOnlyList<ToolParameter>> parameters)
    {
        if (!parameters.IsCompletedSuccessfully)
        {
            LogToolFailed(item.Id, parameters.Exception);
            _tray?.Notify("No se pudo abrir la herramienta", item.Label, isError: true);
            return;
        }

        IReadOnlyDictionary<string, string>? options = null;
        if (parameters.Result.Count > 0)
        {
            var subtitle = files.Count == 1 ? Path.GetFileName(files[0]) : $"{files.Count} archivos";
            options = ToolOptionsDialog.Ask(new ToolDialogRequest(item.Id, item.Label, subtitle, parameters.Result, chosen => _tools.Validate(item.Id, chosen))
            {
                Preview = HasPreview(files) ? ct => _previews.CreateAsync(files[0], ct) : null,
            });
            if (options is null)
            {
                return; // Cancelado.
            }
        }

        Enqueue(_tools.CreateJobs(files, item.Id, options), "listo", $"({item.Label})", item.Id);
    }

    /// <summary>Vista previa solo para un archivo de una familia que la tiene (PDF, foto, audio, video).</summary>
    private static bool HasPreview(IReadOnlyList<string> files)
    {
        if (files.Count != 1)
        {
            return false;
        }

        try
        {
            return FormatSniffer.DetectFile(files[0])?.Category is FormatCategory.Pdf or FormatCategory.Image or FormatCategory.Video or FormatCategory.Audio;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void TransferTo(IReadOnlyList<string> files, bool move)
    {
        var folder = FolderPicker.PickFolder(move ? "Mover a…" : "Copiar a…", move ? "Mover aquí" : "Copiar aquí");
        if (folder is null)
        {
            return;
        }

        var jobs = files.Select(f => new JobRequest(
            f,
            move ? "Mover" : "Copiar",
            (_, ct) => move ? FileTransfer.MoveAsync(f, folder, ct) : FileTransfer.CopyAsync(f, folder, ct)));
        Enqueue(jobs, move ? "movido" : "copiado", $"a {Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar))}", move ? FileActions.MoveTo : FileActions.CopyTo);
    }

    private void Enqueue(IEnumerable<JobRequest> jobs, string verb, string detail, string id)
    {
        var ids = jobs.Select(_jobs.Enqueue).ToList();
        _notifier.Track(ids, verb, detail);
        LogEnqueued(ids.Count, id);
    }

    private List<TrayMenuItem> BuildMenu()
    {
        TrayMenuItem TriggerOption(TriggerKey key, string label)
            => new((_settings.Trigger == key ? "✓ " : "    ") + label, () => SetTrigger(key));

        return
        [
            new($"Kumquat {typeof(TrayApplication).Assembly.GetName().Version?.ToString(3)}", null),
            TrayMenuItem.Separator,
            TriggerOption(TriggerKey.Shift, "Abrir la rueda con Shift"),
            TriggerOption(TriggerKey.Control, "Abrir la rueda con Ctrl"),
            TriggerOption(TriggerKey.CapsLock, "Abrir la rueda con Bloq Mayús"),
            TriggerOption(TriggerKey.RightClick, "Abrir la rueda con clic derecho"),
            TrayMenuItem.Separator,
            new((_settings.HotkeyEnabled ? "✓ " : "    ") + $"{Hotkey.Default.Display} sobre los archivos seleccionados", ToggleHotkey),
            TrayMenuItem.Separator,
            new("Abrir carpeta de registros", () => OpenFolder(KumquatServices.LogDirectory)),
            new("Licencias de terceros", () => OpenFolder(AppContext.BaseDirectory)),
            TrayMenuItem.Separator,
            new("Salir", _ui.Quit),
        ];
    }

    private void SetTrigger(TriggerKey key)
    {
        _settings = _settings with { Trigger = key };
        _settings.Save(SettingsPath);
        _wheel!.Trigger = key;
        LogStarted(key);
    }

    private void ToggleHotkey()
    {
        var enabled = !_settings.HotkeyEnabled;
        _settings = _settings with { HotkeyEnabled = enabled };
        _settings.Save(SettingsPath);
        if (!_wheel!.SetHotkey(enabled ? Hotkey.Default : null))
        {
            _tray?.Notify("Atajo no disponible", $"Otra aplicación ya usa {Hotkey.Default.Display}.", isError: true);
        }
    }

    private void OnNothingSelected(bool explorerInFront)
        => _tray?.Notify(
            "Nada seleccionado",
            explorerInFront
                ? "Selecciona uno o más archivos y vuelve a presionar " + Hotkey.Default.Display + "."
                : "Funciona en el Explorador y el escritorio: selecciona archivos y presiona " + Hotkey.Default.Display + ".");

    private static string TriggerName(TriggerKey key) => key switch
    {
        TriggerKey.Control => "Ctrl",
        TriggerKey.CapsLock => "Bloq Mayús",
        TriggerKey.RightClick => "el clic derecho",
        _ => "Shift",
    };

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        var explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        var startInfo = new ProcessStartInfo(explorer) { UseShellExecute = false };
        startInfo.ArgumentList.Add(path);
        using var _ = Process.Start(startInfo);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Rueda activa con el gesto {Trigger}")]
    private partial void LogStarted(TriggerKey trigger);

    [LoggerMessage(Level = LogLevel.Error, Message = "No se pudieron leer los parámetros de {Tool}")]
    private partial void LogToolFailed(string tool, Exception? exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Interacción: {Step}")]
    private partial void LogInteraction(string step);

    [LoggerMessage(Level = LogLevel.Information, Message = "Encolados {Count} archivos hacia {Target}")]
    private partial void LogEnqueued(int count, string target);
}

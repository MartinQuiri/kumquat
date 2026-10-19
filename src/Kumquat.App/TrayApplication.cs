// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Diagnostics;
using Kumquat.Core.Conversion;
using Kumquat.Core.Jobs;
using Kumquat.Core.Tools;
using Kumquat.Hosting;
using Kumquat.Interaction;
using Kumquat.Interaction.Input;
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
        _targets = new WheelTargets(services.GetRequiredService<ConversionGraph>(), _tools);
        _notifier = new BatchNotifier((title, message, isError) => _tray?.Notify(title, message, isError));
        _settings = AppSettings.Load(SettingsPath);
    }

    public void Start()
    {
        _jobs.JobChanged += (_, e) => _ui.Post(() => _notifier.OnJobChanged(e.Snapshot));

        _tray = new TrayIcon("Kumquat: arrastra un archivo y presiona Shift", BuildMenu);
        _wheel = new WheelController(_targets.For, OnChosen, _settings.Trigger) { Trace = LogInteraction };
        _wheel.Start();
        LogStarted(_settings.Trigger);

        // Kumquat no tiene ventana: sin este aviso, abrir el .exe parece no hacer nada.
        _tray.Notify("Kumquat está activo 🍊", $"Arrastra un archivo y, sin soltarlo, presiona {TriggerName(_settings.Trigger)}. El ícono está en la bandeja (junto al reloj).");
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
                Enqueue(files.Select(f => _tools.CreateJob(f, item.Id)), "listo", $"({item.Label})", item.Id);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Interacción: {Step}")]
    private partial void LogInteraction(string step);

    [LoggerMessage(Level = LogLevel.Information, Message = "Encolados {Count} archivos hacia {Target}")]
    private partial void LogEnqueued(int count, string target);
}

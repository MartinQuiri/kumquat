// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Diagnostics;
using Kumquat.Core.Conversion;
using Kumquat.Core.Jobs;
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
        _targets = new WheelTargets(services.GetRequiredService<ConversionGraph>());
        _notifier = new BatchNotifier((title, message, isError) => _tray?.Notify(title, message, isError));
        _settings = AppSettings.Load(SettingsPath);
    }

    public void Start()
    {
        _jobs.JobChanged += (_, e) => _ui.Post(() => _notifier.OnJobChanged(e.Snapshot));

        _tray = new TrayIcon("Kumquat: arrastra un archivo y presiona Shift", BuildMenu);
        _wheel = new WheelController(_targets.For, OnChosen, _settings.Trigger);
        _wheel.Start();
        LogStarted(_settings.Trigger);
    }

    public void Dispose()
    {
        _wheel?.Dispose();
        _tray?.Dispose();
    }

    private void OnChosen(IReadOnlyList<string> files, WheelItem item)
    {
        var ids = files.Select(f => _jobs.Enqueue(new ConversionRequest(f, item.Target))).ToList();
        _notifier.Track(ids, item.Target.DisplayName);
        LogEnqueued(ids.Count, item.Target.Id);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Encolados {Count} archivos hacia {Target}")]
    private partial void LogEnqueued(int count, string target);
}

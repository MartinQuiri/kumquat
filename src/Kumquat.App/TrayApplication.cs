// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Diagnostics;
using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;
using Kumquat.Core.Jobs;
using Kumquat.Core.Previews;
using Kumquat.Core.Tools;
using Kumquat.Dialogs;
using Kumquat.Dialogs.Search;
using Kumquat.Dialogs.Settings;
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

        DialogDiagnostics.Error = (activity, ex) => LogDialogError(activity, ex);
        _tray = new TrayIcon("Kumquat: arrastra un archivo y presiona Shift", BuildMenu);
        _wheel = new WheelController(_targets.For, OnChosen, _settings.Trigger) { Trace = LogInteraction, NothingSelected = OnNothingSelected, CaptionForFiles = WheelTargets.Caption };
        _wheel.KeyboardPicker = files => _ui.Post(() => ShowPalette(files));
        _wheel.Start();
        ApplySettings(notifyHotkeyConflict: false);
        var hotkeyActive = _settings.HotkeyEnabled && _wheel.HasHotkey;
        LogStarted(_settings.Trigger);

        // Kumquat no tiene ventana: sin este aviso, abrir el .exe parece no hacer nada.
        var hotkeyHint = hotkeyActive ? $" O selecciona archivos y presiona {Hotkey.Default.Display}." : string.Empty;
        _tray.Notify("Kumquat está activo", $"Arrastra un archivo y, sin soltarlo, presiona {TriggerName(_settings.Trigger)}.{hotkeyHint}");
    }

    /// <summary>Abre la rueda con estos archivos (Kumquat se abrió desde el menú contextual).</summary>
    public void OpenWheel(IReadOnlyList<string> files) => _ui.Post(() => _wheel?.OpenForFiles(files));

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

    private List<TrayMenuItem> BuildMenu() =>
    [
        new($"Kumquat {typeof(TrayApplication).Assembly.GetName().Version?.ToString(3)}", null),
        TrayMenuItem.Separator,
        new("Configuración…", () => _ui.Post(OpenSettings)),
        TrayMenuItem.Separator,
        new("Abrir carpeta de registros", () => OpenFolder(KumquatServices.LogDirectory)),
        new("Licencias de terceros", () => OpenFolder(AppContext.BaseDirectory)),
        TrayMenuItem.Separator,
        new("Salir", _ui.Quit),
    ];

    /// <summary>Gestos para la rueda, en el orden de la ventana (el primero es el recomendado).</summary>
    private static readonly (string Id, string Label)[] TriggerChoices =
    [
        (nameof(TriggerKey.Shift), "Shift (recomendado)"),
        (nameof(TriggerKey.Control), "Ctrl"),
        (nameof(TriggerKey.CapsLock), "Bloq Mayús"),
        (nameof(TriggerKey.RightClick), "Clic derecho"),
    ];

    private void OpenSettings()
    {
        var current = new SettingsValues(_settings.Trigger.ToString(), _settings.HotkeyEnabled, _settings.ContextMenuEnabled, _settings.StartWithWindows, _settings.ResolvedOutputFolder);
        if (SettingsDialog.Ask(current, TriggerChoices, AppSettings.DefaultOutputFolder) is not { } chosen)
        {
            return;
        }

        _settings = _settings with
        {
            Trigger = Enum.Parse<TriggerKey>(chosen.Trigger),
            HotkeyEnabled = chosen.Hotkey,
            ContextMenuEnabled = chosen.ContextMenu,
            StartWithWindows = chosen.StartWithWindows,
            OutputFolder = string.Equals(chosen.OutputFolder, AppSettings.DefaultOutputFolder, StringComparison.OrdinalIgnoreCase) ? null : chosen.OutputFolder,
        };
        _settings.Save(SettingsPath);
        ApplySettings(notifyHotkeyConflict: true);
        LogStarted(_settings.Trigger);
    }

    /// <summary>Aplica la configuración a la rueda, el atajo, el menú contextual y el inicio con Windows.</summary>
    private void ApplySettings(bool notifyHotkeyConflict)
    {
        _wheel!.Trigger = _settings.Trigger;
        var hotkeyFree = _wheel.SetHotkey(_settings.HotkeyEnabled ? Hotkey.Default : null);
        if (!hotkeyFree && notifyHotkeyConflict)
        {
            _tray?.Notify("Atajo no disponible", $"Otra aplicación ya usa {Hotkey.Default.Display}.", isError: true);
        }

        SyncContextMenu();
        try
        {
            Autostart.Set(_settings.StartWithWindows, Environment.ProcessPath!);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            LogContextMenuFailed(ex);
        }
    }

    /// <summary>Registra (o corrige la ruta si se movió la carpeta) o quita la entrada del menú.</summary>
    private void SyncContextMenu()
    {
        try
        {
            var executable = Environment.ProcessPath!;
            if (!_settings.ContextMenuEnabled)
            {
                ExplorerMenu.Unregister();
            }
            else if (!ExplorerMenu.IsRegisteredFor(executable))
            {
                ExplorerMenu.Register(executable);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            LogContextMenuFailed(ex);
        }
    }

    /// <summary>Buscador con todo lo que se puede hacer con estos archivos (atajo y menú contextual).</summary>
    private void ShowPalette(IReadOnlyList<string> files)
    {
        var rings = _targets.For(files);
        var items = rings.SelectMany(r => r.Items).ToList();
        var request = new PaletteRequest(
            files.Count == 1 ? Path.GetFileName(files[0]) : $"{files.Count} archivos",
            SafeCaption(files),
            WheelTargets.CategoryIcon(files),
            [.. items.Select(PaletteItems.From)]);
        var chosen = CommandPalette.Ask(request);
        LogInteraction($"Buscador -> {chosen ?? "(nada)"}.");
        if (chosen is not null && items.FirstOrDefault(i => string.Equals(i.Id, chosen, StringComparison.Ordinal)) is { } item)
        {
            OnChosen(files, item);
        }
    }

    private static string? SafeCaption(IReadOnlyList<string> files)
    {
        try
        {
            return WheelTargets.Caption(files);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
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

    [LoggerMessage(Level = LogLevel.Error, Message = "Error en la ventana de opciones al {Activity}")]
    private partial void LogDialogError(string activity, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo actualizar el menú contextual del Explorador")]
    private partial void LogContextMenuFailed(Exception exception);
}

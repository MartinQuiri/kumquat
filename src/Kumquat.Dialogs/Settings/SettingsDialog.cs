// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;
using Kumquat.Core.Localization;
using Kumquat.Design;
using Kumquat.Dialogs.Look;

namespace Kumquat.Dialogs.Settings;

/// <summary>Configuración de Kumquat, con el mismo vidrio que las ventanas de herramientas.</summary>
public sealed class SettingsDialog : Window
{
    private const double CaptionHeight = 44;

    private readonly DialogLook _look;
    private readonly SettingsValues _initial;
    private readonly string _defaultFolder;
    private readonly List<(string Id, RadioButton Button)> _triggers = [];
    private readonly List<(string Code, RadioButton Button)> _languages = [];
    private readonly CheckBox _hotkey;
    private readonly CheckBox _contextMenu;
    private readonly CheckBox _startWithWindows;
    private readonly TextBlock _folder;
    private string _outputFolder;

    private SettingsDialog(SettingsValues values, IReadOnlyList<(string Id, string Label)> triggers, string defaultFolder, bool dark)
    {
        _initial = values;
        _defaultFolder = defaultFolder;
        _outputFolder = values.OutputFolder;
        _look = new DialogLook(dark, Palette.Kumquat);
        Title = Strings.SettingsWindowTitle;
        ThemeMode = dark ? ThemeMode.Dark : ThemeMode.Light;
        FontFamily = DialogLook.Font;
        Resources["ContentControlThemeFontFamily"] = DialogLook.Font;
        Foreground = _look.Text;
        Width = 560;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        _hotkey = Check(Strings.SettingsHotkey, values.Hotkey);
        _contextMenu = Check(Strings.SettingsContextMenu, values.ContextMenu);
        _startWithWindows = Check(Strings.SettingsStartWithWindows, values.StartWithWindows);
        _folder = new TextBlock { Text = values.OutputFolder, TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 13, ToolTip = values.OutputFolder, VerticalAlignment = VerticalAlignment.Center };

        var panel = new StackPanel { Margin = new Thickness(24, CaptionHeight, 24, 22) };
        panel.Children.Add(Header());
        panel.Children.Add(Section(Strings.SettingsWheelSection, Strings.SettingsWheelHint, TriggerOptions(triggers, values.Trigger)));
        panel.Children.Add(Section(Strings.SettingsKeyboardSection, null, Stack(_hotkey, _contextMenu)));
        panel.Children.Add(Section(Strings.SettingsOutputSection, Strings.SettingsOutputHint, FolderRow()));
        panel.Children.Add(Section(Strings.SettingsStartupSection, null, Stack(_startWithWindows)));
        panel.Children.Add(Section(Strings.SettingsLanguageSection, Strings.SettingsLanguageHint, LanguageOptions(values.Language)));
        panel.Children.Add(Buttons());
        Content = new Grid { Background = Tint(), Children = { panel } };

        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = CaptionHeight - 8, GlassFrameThickness = new Thickness(-1), UseAeroCaptionButtons = true });
        SourceInitialized += (_, _) => WindowBackdrop.TryApply(this, dark);
    }

    /// <summary>Valores guardados, o <c>null</c> si se canceló.</summary>
    public SettingsValues? Result { get; private set; }

    /// <summary>Muestra la ventana (modal, hilo STA de UI).</summary>
    /// <param name="values">Valores actuales.</param>
    /// <param name="triggers">Gestos ofrecidos (id, texto), el primero es el recomendado.</param>
    /// <param name="defaultFolder">Carpeta por defecto para "Usar el Escritorio".</param>
    public static SettingsValues? Ask(SettingsValues values, IReadOnlyList<(string Id, string Label)> triggers, string defaultFolder)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(triggers);
        var dialog = new SettingsDialog(values, triggers, defaultFolder, SystemTheme.AppsUseDark());
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    /// <summary>Crea la ventana sin mostrarla (pruebas).</summary>
    internal static SettingsDialog CreateForTests(SettingsValues values, IReadOnlyList<(string Id, string Label)> triggers, string defaultFolder, bool dark = true)
        => new(values, triggers, defaultFolder, dark);

    /// <summary>Lo elegido en la ventana (pruebas y "Guardar").</summary>
    internal SettingsValues Collect() => new(
        _triggers.FirstOrDefault(t => t.Button.IsChecked == true).Id ?? _initial.Trigger,
        _hotkey.IsChecked == true,
        _contextMenu.IsChecked == true,
        _startWithWindows.IsChecked == true,
        _outputFolder,
        _languages.FirstOrDefault(l => l.Button.IsChecked == true).Code ?? _initial.Language);

    /// <summary>Renderiza la ventana a un PNG (documentación); el acrílico se simula con un degradado.</summary>
    internal static void RenderPreview(SettingsDialog dialog, string pngPath, bool dark)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        dialog.Left = -20000;
        dialog.Top = -20000;
        dialog.ShowActivated = false;
        dialog.Show();
        dialog.UpdateLayout();
        var content = (FrameworkElement)dialog.Content;
        var size = new Size(content.ActualWidth, content.ActualHeight);
        const double scale = 2.0;
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)(size.Width * scale), (int)(size.Height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            var backdrop = new LinearGradientBrush(
                dark ? Color.FromRgb(0x2B, 0x2A, 0x38) : Color.FromRgb(0xF4, 0xEE, 0xF2),
                dark ? Color.FromRgb(0x1E, 0x24, 0x2E) : Color.FromRgb(0xE9, 0xF0, 0xF6),
                45);
            context.DrawRectangle(backdrop, null, new Rect(size));
            context.DrawRectangle(new VisualBrush(content), null, new Rect(size));
        }

        bitmap.Render(visual);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using (var file = File.Create(pngPath))
        {
            encoder.Save(file);
        }

        dialog.Close();
    }

    /// <summary>Elige un gesto (pruebas).</summary>
    internal void ChooseTriggerForTests(string id) => _triggers.Single(t => string.Equals(t.Id, id, StringComparison.Ordinal)).Button.IsChecked = true;

    /// <summary>Simula "Usar el Escritorio" (pruebas).</summary>
    internal void UseDefaultFolderForTests() => SetFolder(_defaultFolder);

    private static StackPanel Stack(params UIElement[] children)
    {
        var stack = new StackPanel();
        foreach (var child in children)
        {
            stack.Children.Add(child);
        }

        return stack;
    }

    private static CheckBox Check(string text, bool value)
    {
        var check = new CheckBox { Content = text, IsChecked = value, FontSize = 13.5, Margin = new Thickness(0, 4, 0, 4) };
        AutomationProperties.SetName(check, text);
        return check;
    }

    private LinearGradientBrush Tint() => new(
        _look.IsDark ? Color.FromArgb(0x30, 0x2A, 0x2A, 0x33) : Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF),
        _look.IsDark ? Color.FromArgb(0x50, 0x12, 0x12, 0x16) : Color.FromArgb(0x30, 0xF2, 0xF2, 0xF7),
        90);

    private Grid Header()
    {
        var header = new Grid { Margin = new Thickness(0, 4, 0, 18) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.Children.Add(_look.IconTile("settings", 48));
        var title = new TextBlock { Text = Strings.SettingsTitle, FontSize = 22, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
        Grid.SetColumn(title, 1);
        header.Children.Add(title);
        return header;
    }

    private Border Section(string title, string? description, UIElement content)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = title, FontSize = 14.5, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
        if (description is not null)
        {
            stack.Children.Add(new TextBlock { Text = description, FontSize = 12.5, Foreground = _look.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) });
        }

        stack.Children.Add(content);
        return new Border
        {
            CornerRadius = new CornerRadius(14),
            Background = _look.Platter,
            BorderBrush = _look.PlatterBorder,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 0, 12),
            Child = stack,
        };
    }

    private System.Windows.Controls.Primitives.UniformGrid TriggerOptions(IReadOnlyList<(string Id, string Label)> triggers, string current)
    {
        var options = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        foreach (var (id, label) in triggers)
        {
            var button = new RadioButton { Content = label, GroupName = "trigger", IsChecked = string.Equals(id, current, StringComparison.Ordinal), FontSize = 13.5, Margin = new Thickness(0, 4, 18, 4) };
            AutomationProperties.SetName(button, label);
            _triggers.Add((id, button));
            options.Children.Add(button);
        }

        return options;
    }

    /// <summary>Elige un idioma (pruebas).</summary>
    internal void ChooseLanguageForTests(string code) => _languages.Single(l => string.Equals(l.Code, code, StringComparison.Ordinal)).Button.IsChecked = true;

    /// <summary>Automático y cada idioma con su nombre en ese idioma (así lo encuentra quien no entiende el actual).</summary>
    private System.Windows.Controls.Primitives.UniformGrid LanguageOptions(string current)
    {
        var options = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3 };
        foreach (var (code, label) in Strings.Languages.Prepend(("auto", Strings.SettingsLanguageAuto)))
        {
            var button = new RadioButton { Content = label, GroupName = "language", IsChecked = string.Equals(code, current, StringComparison.OrdinalIgnoreCase), FontSize = 13.5, Margin = new Thickness(0, 4, 18, 4) };
            AutomationProperties.SetName(button, label);
            _languages.Add((code, button));
            options.Children.Add(button);
        }

        if (!_languages.Any(l => l.Button.IsChecked == true))
        {
            _languages[0].Button.IsChecked = true;
        }

        return options;
    }

    private Grid FolderRow()
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var icon = DialogLook.Icon("show-in-folder", 18, _look.AccentBrush);
        icon.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(icon);
        _folder.Margin = new Thickness(10, 0, 10, 0);
        Grid.SetColumn(_folder, 1);
        row.Children.Add(_folder);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        var change = SmallPill(Strings.SettingsChangeFolder);
        change.Click += (_, _) => PickFolder();
        var reset = SmallPill(Strings.SettingsUseDesktop);
        reset.Click += (_, _) => SetFolder(_defaultFolder);
        buttons.Children.Add(change);
        buttons.Children.Add(reset);
        Grid.SetColumn(buttons, 2);
        row.Children.Add(buttons);
        return row;
    }

    private Button SmallPill(string text)
    {
        var button = DialogLook.Pill(text, _look.Subtle, _look.Text);
        button.Height = 32;
        button.FontSize = 12.5;
        button.Padding = new Thickness(14, 0, 14, 0);
        button.Margin = new Thickness(6, 0, 0, 0);
        return button;
    }

    private Grid Buttons()
    {
        var buttons = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
        var cancel = DialogLook.Pill(Strings.ButtonCancel, _look.Subtle, _look.Text);
        cancel.IsCancel = true;
        cancel.Height = 44;
        var save = DialogLook.Pill(Strings.ButtonSave, _look.AccentGradient, Brushes.White);
        save.IsDefault = true;
        save.Height = 44;
        save.FontSize = 15;
        save.Click += (_, _) =>
        {
            Result = Collect();
            DialogResult = true;
        };
        Grid.SetColumn(save, 2);
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        return buttons;
    }

    private void PickFolder()
    {
        var picker = new Microsoft.Win32.OpenFolderDialog
        {
            Title = Strings.SettingsPickFolderTitle,
            InitialDirectory = Directory.Exists(_outputFolder) ? _outputFolder : _defaultFolder,
        };
        if (picker.ShowDialog(this) == true)
        {
            SetFolder(picker.FolderName);
        }
    }

    private void SetFolder(string folder)
    {
        _outputFolder = folder;
        _folder.Text = folder;
        _folder.ToolTip = folder;
    }
}

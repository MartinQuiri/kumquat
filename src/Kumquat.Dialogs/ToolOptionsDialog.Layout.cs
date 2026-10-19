// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Kumquat.Core.Tools;
using Kumquat.Design;
using Kumquat.Dialogs.Look;

namespace Kumquat.Dialogs;

/// <summary>Panel lateral: encabezado, campos y botones.</summary>
public sealed partial class ToolOptionsDialog
{
    private Grid BuildSidebar()
    {
        var sidebar = new Grid();
        sidebar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sidebar.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        sidebar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        sidebar.Children.Add(BuildHeader());

        var fields = new StackPanel { Margin = new Thickness(0, 4, 4, 0) };
        if (_parameters.Any(p => p.Kind == ToolParameterKind.Text))
        {
            fields.Children.Add(new TextBlock { Text = "Deja un campo vacío para borrarlo.", Foreground = _look.Muted, Margin = new Thickness(0, 0, 0, 14), FontSize = 12.5 });
        }

        foreach (var parameter in _parameters)
        {
            fields.Children.Add(BuildField(parameter));
        }

        var scroll = new ScrollViewer { Content = fields, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0, 0, 4, 0) };
        Grid.SetRow(scroll, 1);
        sidebar.Children.Add(scroll);

        var footer = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
        footer.Children.Add(_error);
        footer.Children.Add(BuildButtons());
        Grid.SetRow(footer, 2);
        sidebar.Children.Add(footer);
        return sidebar;
    }

    private Grid BuildHeader()
    {
        var header = new Grid { Margin = new Thickness(0, 8, 0, 22) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.Children.Add(_look.IconTile(Icons.ForItem(_request.ToolId), 52));

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
        text.Children.Add(new TextBlock { Text = _request.Title, FontSize = 22, FontWeight = FontWeights.SemiBold, Foreground = _look.Text, TextTrimming = TextTrimming.CharacterEllipsis });
        text.Children.Add(new TextBlock { Text = _request.Subtitle, FontSize = 13, Foreground = _look.Muted, Margin = new Thickness(0, 2, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = _request.Subtitle });
        Grid.SetColumn(text, 1);
        header.Children.Add(text);
        return header;
    }

    private StackPanel BuildField(ToolParameter parameter)
    {
        if (parameter.Kind == ToolParameterKind.CustomFields)
        {
            return BuildCustomSection(parameter);
        }

        var field = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
        var label = new Label
        {
            Content = parameter.Label + (parameter.Required ? " *" : string.Empty),
            Padding = new Thickness(0, 0, 0, 6),
            FontWeight = FontWeights.Medium,
            FontSize = 13,
            Foreground = _look.Text,
        };
        field.Children.Add(label);
        Control input;
        if (parameter.Kind == ToolParameterKind.Password)
        {
            var box = new PasswordBox { Password = parameter.DefaultValue ?? string.Empty, FontSize = 14 };
            _readers[parameter.Key] = () => box.Password;
            input = box;
        }
        else
        {
            var box = new TextBox { Text = parameter.DefaultValue ?? string.Empty, FontSize = 14 };
            _readers[parameter.Key] = () => box.Text;
            _boxes[parameter.Key] = box;
            input = box;
        }

        label.Target = input;
        AutomationProperties.SetName(input, parameter.Label);
        field.Children.Add(input);
        if (parameter.Hint is not null)
        {
            field.Children.Add(new TextBlock { Text = parameter.Hint, Foreground = _look.Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 0) });
        }

        return field;
    }

    private StackPanel BuildCustomSection(ToolParameter parameter)
    {
        var section = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        section.Children.Add(new TextBlock { Text = parameter.Label, FontWeight = FontWeights.SemiBold, FontSize = 14, Foreground = _look.Text, Margin = new Thickness(0, 0, 0, 8) });
        section.Children.Add(_customRows);
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(DialogLook.Icon("plus", 14, _look.Text));
        content.Children.Add(new TextBlock { Text = "Agregar campo", Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        var add = DialogLook.Pill(content, _look.Subtle, _look.Text);
        add.Height = 34;
        add.FontSize = 13;
        add.Padding = new Thickness(14, 0, 16, 0);
        add.HorizontalAlignment = HorizontalAlignment.Left;
        add.Margin = new Thickness(0, 2, 0, 0);
        add.Click += (_, _) => AddCustomRow();
        section.Children.Add(add);
        return section;
    }

    private void AddCustomRow()
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var name = new TextBox { Margin = new Thickness(0, 0, 6, 0), FontSize = 14 };
        var value = new TextBox { Margin = new Thickness(0, 0, 6, 0), FontSize = 14 };
        AutomationProperties.SetName(name, "Nombre del campo");
        AutomationProperties.SetName(value, "Valor del campo");
        var remove = DialogLook.Pill(DialogLook.Icon("close", 12, _look.Text), _look.Subtle, _look.Text);
        remove.Width = 32;
        remove.Height = 32;
        remove.Padding = new Thickness(0);
        remove.ToolTip = "Quitar";
        AutomationProperties.SetName(remove, "Quitar campo");
        remove.Click += (_, _) => _customRows.Children.Remove(row);
        Grid.SetColumn(value, 1);
        Grid.SetColumn(remove, 2);
        row.Children.Add(name);
        row.Children.Add(value);
        row.Children.Add(remove);
        _customRows.Children.Add(row);
        name.Focus();
    }

    private Grid BuildButtons()
    {
        var buttons = new Grid();
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });

        var cancel = DialogLook.Pill("Cancelar", _look.Subtle, _look.Text);
        cancel.IsCancel = true;
        cancel.Height = 44;
        var apply = DialogLook.Pill("Aplicar", _look.AccentGradient, Brushes.White);
        apply.IsDefault = true;
        apply.Height = 44;
        apply.FontSize = 15;
        apply.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 16, ShadowDepth = 3, Opacity = 0.35, Color = _look.Accent, Direction = 270 };
        apply.Click += (_, _) => TryAccept();
        Grid.SetColumn(apply, 2);
        buttons.Children.Add(cancel);
        buttons.Children.Add(apply);
        return buttons;
    }
}

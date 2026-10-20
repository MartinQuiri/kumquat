// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;
using Kumquat.Cli;
using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;
using Kumquat.Core.Localization;
using Kumquat.Core.Tools;
using Kumquat.Hosting;
using Microsoft.Extensions.DependencyInjection;

// Códigos de salida: 0 ok, 1 alguna conversión falló, 2 uso incorrecto, 130 cancelado.
Console.OutputEncoding = System.Text.Encoding.UTF8;
System.Globalization.CultureInfo.CurrentUICulture = Strings.Resolve("auto", System.Globalization.CultureInfo.CurrentUICulture);
var command = CliCommand.Parse(args);
if (command.Error is not null)
{
    await Console.Error.WriteLineAsync($"kumquat-cli: {Strings.CliUsageHint(command.Error)}");
    return 2;
}

if (string.Equals(command.Name, "help", StringComparison.Ordinal))
{
    Console.WriteLine(Strings.CliHelp);
    return 0;
}

await using var services = new ServiceCollection().AddKumquat(logToFile: false).BuildServiceProvider();

// Ctrl+C, SIGTERM (kill, timeout, systemd) y SIGHUP (se cerró la terminal) cancelan el trabajo en
// curso: el motor y sus hijos se terminan y no quedan temporales. Sin esto, en Linux y macOS el
// proceso moría de golpe y el motor seguía corriendo huérfano (en Windows lo mata el Job Object).
using var cancel = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancel.Cancel();
};
using var onTerminate = PosixSignalRegistration.Create(PosixSignal.SIGTERM, Cancel);
using var onHangUp = PosixSignalRegistration.Create(PosixSignal.SIGHUP, Cancel);

void Cancel(PosixSignalContext context)
{
    context.Cancel = true;
    cancel.Cancel();
}
var graph = services.GetRequiredService<ConversionGraph>();

switch (command.Name)
{
    case "formats":
        foreach (var group in KnownFormats.All.GroupBy(f => f.Category))
        {
            Console.WriteLine($"{group.Key,-12} {string.Join(", ", group.Select(f => f.Id))}");
        }

        return 0;

    case "engines":
        Console.WriteLine(KumquatServices.DescribeEngines(services));
        return 0;

    case "targets":
        foreach (var file in command.Files)
        {
            var format = FormatSniffer.DetectFile(file);
            var targets = format is null ? [] : graph.Targets(format).Select(p => p.To.Id + (p.Edges.Count > 1 ? "*" : string.Empty));
            Console.WriteLine($"{file} ({format?.Id ?? Strings.CliUnknownFileFormat}): {string.Join(", ", targets)}");
        }

        Console.WriteLine(Strings.CliMultiStep);
        return 0;

    case "tools":
        var toolService = services.GetRequiredService<ToolService>();
        foreach (var file in command.Files)
        {
            var format = FormatSniffer.DetectFile(file);
            var tools = format is null ? [] : toolService.ToolsForFormats([format]).Select(t => $"{t.Id} ({t.Label})");
            Console.WriteLine($"{file} ({format?.Id ?? Strings.CliUnknownFileFormat}): {string.Join(", ", tools)}");
        }

        return 0;

    case "apply":
        return await ApplyToolAsync(services.GetRequiredService<ToolService>(), command.Files[0], command.Files.Skip(1).ToList(), command.Options, cancel.Token);
}

var target = KnownFormats.FindById(command.Target!) ?? KnownFormats.FindByExtension(command.Target!);
if (target is null)
{
    await Console.Error.WriteLineAsync($"kumquat-cli: {Strings.CliUnknownTarget(command.Target)}");
    return 2;
}

var service = services.GetRequiredService<ConversionService>();
var failures = 0;
foreach (var file in command.Files)
{
    var progress = new ConsoleProgress(Path.GetFileName(file));
    var result = await service.ConvertAsync(new ConversionRequest(file, target, command.OutputDirectory), progress, cancel.Token);
    await Console.Error.WriteAsync("\r" + new string(' ', 60) + "\r");
    if (result.IsSuccess)
    {
        Console.WriteLine(result.Value);
    }
    else if (cancel.IsCancellationRequested)
    {
        await Console.Error.WriteLineAsync($"kumquat-cli: {Strings.CliCancelled}");
        return 130;
    }
    else
    {
        failures++;
        await Console.Error.WriteLineAsync($"kumquat-cli: {file}: {result.Error.Message}");
    }
}

return failures == 0 ? 0 : 1;

static async Task<int> ApplyToolAsync(ToolService tools, string toolId, IReadOnlyList<string> files, IReadOnlyDictionary<string, string>? set, CancellationToken cancellationToken)
{
    // Opciones = valores iniciales que propone la herramienta (de los textos solo se cambian los indicados) + --set.
    var parameters = await tools.GetParametersAsync(files, toolId, cancellationToken);
    var options = parameters.Where(p => p.Kind is not (ToolParameterKind.Text or ToolParameterKind.CustomFields)).ToDictionary(p => p.Key, p => p.DefaultValue ?? string.Empty, StringComparer.Ordinal);
    foreach (var (key, value) in set ?? new Dictionary<string, string>(StringComparer.Ordinal))
    {
        options[key] = value;
    }

    // Las herramientas que combinan (unir, ZIP) reciben todos los archivos juntos; el resto, uno a uno.
    var batches = tools.Find(toolId)?.Mode == ToolMode.Combine ? [files] : files.Select(f => (IReadOnlyList<string>)[f]).ToList();
    var failed = 0;
    foreach (var batch in batches)
    {
        var file = batch[0];
        var result = await tools.ApplyAsync(batch, toolId, options, new ConsoleProgress(Path.GetFileName(file)), cancellationToken);
        await Console.Error.WriteAsync("\r" + new string(' ', 60) + "\r");
        if (result.IsSuccess)
        {
            Console.WriteLine(result.Value);
        }
        else if (cancellationToken.IsCancellationRequested)
        {
            await Console.Error.WriteLineAsync($"kumquat-cli: {Strings.CliCancelled}");
            return 130;
        }
        else
        {
            failed++;
            await Console.Error.WriteLineAsync($"kumquat-cli: {file}: {result.Error.Message}");
        }
    }

    return failed == 0 ? 0 : 1;
}

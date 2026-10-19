// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Cli;
using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;
using Kumquat.Core.Tools;
using Kumquat.Hosting;
using Microsoft.Extensions.DependencyInjection;

// Códigos de salida: 0 ok, 1 alguna conversión falló, 2 uso incorrecto, 130 cancelado.
Console.OutputEncoding = System.Text.Encoding.UTF8;
var command = CliCommand.Parse(args);
if (command.Error is not null)
{
    await Console.Error.WriteLineAsync($"kumquat-cli: {command.Error}  (usa 'kumquat-cli help')");
    return 2;
}

if (string.Equals(command.Name, "help", StringComparison.Ordinal))
{
    Console.WriteLine("""
        Kumquat: conversor local de archivos.

        Uso:
          kumquat-cli convert <archivo>... --to <formato> [--out <carpeta>]
          kumquat-cli targets <archivo>...     formatos a los que se puede convertir
          kumquat-cli tools <archivo>...       herramientas disponibles para el archivo
          kumquat-cli apply <herramienta> <archivo>... [--set clave=valor]...
          kumquat-cli formats                  formatos conocidos
          kumquat-cli engines                  estado de los motores

        Ejemplos:
          kumquat-cli convert foto.heic --to jpg
          kumquat-cli convert *.wav --to mp3 --out D:\musica
          kumquat-cli apply media.trim video.mp4 --set start=0:10 --set end=0:45
          kumquat-cli apply office.metadata informe.docx --set creator="María Pérez"
        """);
    return 0;
}

await using var services = new ServiceCollection().AddKumquat(logToFile: false).BuildServiceProvider();
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
            Console.WriteLine($"{file} ({format?.Id ?? "desconocido"}): {string.Join(", ", targets)}");
        }

        Console.WriteLine("(* = conversión en varios pasos)");
        return 0;

    case "tools":
        var toolService = services.GetRequiredService<ToolService>();
        foreach (var file in command.Files)
        {
            var format = FormatSniffer.DetectFile(file);
            var tools = format is null ? [] : toolService.ToolsForFormats([format]).Select(t => $"{t.Id} ({t.Label})");
            Console.WriteLine($"{file} ({format?.Id ?? "desconocido"}): {string.Join(", ", tools)}");
        }

        return 0;

    case "apply":
        return await ApplyToolAsync(services.GetRequiredService<ToolService>(), command.Files[0], command.Files.Skip(1).ToList(), command.Options);
}

var target = KnownFormats.FindById(command.Target!) ?? KnownFormats.FindByExtension(command.Target!);
if (target is null)
{
    await Console.Error.WriteLineAsync($"kumquat-cli: formato desconocido '{command.Target}'. Usa 'kumquat-cli formats'.");
    return 2;
}

using var cancel = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancel.Cancel();
};

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
        await Console.Error.WriteLineAsync("kumquat-cli: cancelado.");
        return 130;
    }
    else
    {
        failures++;
        await Console.Error.WriteLineAsync($"kumquat-cli: {file}: {result.Error.Message}");
    }
}

return failures == 0 ? 0 : 1;

static async Task<int> ApplyToolAsync(ToolService tools, string toolId, IReadOnlyList<string> files, IReadOnlyDictionary<string, string>? set)
{
    // Opciones = valores iniciales que propone la herramienta (de los textos solo se cambian los indicados) + --set.
    var parameters = await tools.GetParametersAsync(files, toolId, CancellationToken.None);
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
        var result = await tools.ApplyAsync(batch, toolId, options, new ConsoleProgress(Path.GetFileName(file)), CancellationToken.None);
        await Console.Error.WriteAsync("\r" + new string(' ', 60) + "\r");
        if (result.IsSuccess)
        {
            Console.WriteLine(result.Value);
        }
        else
        {
            failed++;
            await Console.Error.WriteLineAsync($"kumquat-cli: {file}: {result.Error.Message}");
        }
    }

    return failed == 0 ? 0 : 1;
}

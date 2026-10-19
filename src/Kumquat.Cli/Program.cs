// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Cli;
using Kumquat.Core.Conversion;
using Kumquat.Core.Formats;
using Kumquat.Hosting;
using Microsoft.Extensions.DependencyInjection;

// Códigos de salida: 0 ok, 1 alguna conversión falló, 2 uso incorrecto, 130 cancelado.
Console.OutputEncoding = System.Text.Encoding.UTF8;
var command = CliCommand.Parse(args);
if (command.Error is not null)
{
    await Console.Error.WriteLineAsync($"kumquat: {command.Error}  (usa 'kumquat help')");
    return 2;
}

if (string.Equals(command.Name, "help", StringComparison.Ordinal))
{
    Console.WriteLine("""
        Kumquat: conversor local de archivos.

        Uso:
          kumquat convert <archivo>... --to <formato> [--out <carpeta>]
          kumquat targets <archivo>...     formatos a los que se puede convertir
          kumquat formats                  formatos conocidos
          kumquat engines                  estado de los motores

        Ejemplos:
          kumquat convert foto.heic --to jpg
          kumquat convert *.wav --to mp3 --out D:\musica
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
}

var target = KnownFormats.FindById(command.Target!) ?? KnownFormats.FindByExtension(command.Target!);
if (target is null)
{
    await Console.Error.WriteLineAsync($"kumquat: formato desconocido '{command.Target}'. Usa 'kumquat formats'.");
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
        await Console.Error.WriteLineAsync("kumquat: cancelado.");
        return 130;
    }
    else
    {
        failures++;
        await Console.Error.WriteLineAsync($"kumquat: {file}: {result.Error.Message}");
    }
}

return failures == 0 ? 0 : 1;

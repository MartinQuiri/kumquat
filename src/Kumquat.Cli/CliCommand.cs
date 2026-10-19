// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Cli;

/// <summary>Comando ya interpretado de la línea de comandos.</summary>
/// <param name="Name">Nombre del comando (<c>convert</c>, <c>targets</c>, <c>formats</c>, <c>engines</c>, <c>help</c>).</param>
/// <param name="Files">Archivos posicionales.</param>
/// <param name="Target">Valor de <c>--to</c>.</param>
/// <param name="OutputDirectory">Valor de <c>--out</c>.</param>
/// <param name="Error">Error de sintaxis, si lo hubo.</param>
/// <param name="Options">Opciones de herramienta (<c>--set clave=valor</c>).</param>
internal sealed record CliCommand(string Name, IReadOnlyList<string> Files, string? Target, string? OutputDirectory, string? Error, IReadOnlyDictionary<string, string>? Options = null)
{
    /// <summary>Interpreta los argumentos. Nunca lanza: los errores van en <see cref="Error"/>.</summary>
    public static CliCommand Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Count == 0 || args[0] is "help" or "-h" or "--help" or "/?")
        {
            return new CliCommand("help", [], null, null, null);
        }

        var name = args[0];
        var files = new List<string>();
        string? target = null;
        string? output = null;
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--to" or "-t" when i + 1 < args.Count:
                    target = args[++i].TrimStart('.').ToLowerInvariant();
                    break;
                case "--out" or "-o" when i + 1 < args.Count:
                    output = args[++i];
                    break;
                case "--set" or "-s" when i + 1 < args.Count && args[i + 1].IndexOf('=', StringComparison.Ordinal) > 0:
                    var pair = args[++i];
                    var eq = pair.IndexOf('=', StringComparison.Ordinal);
                    options[pair[..eq]] = pair[(eq + 1)..];
                    break;
                case "--set" or "-s":
                    return new CliCommand(name, files, target, output, "Usa --set clave=valor.");
                case "--to" or "-t" or "--out" or "-o":
                    return new CliCommand(name, files, target, output, $"Falta el valor de {args[i]}.");
                case "--":
                    files.AddRange(args.Skip(i + 1));
                    i = args.Count;
                    break;
                case var flag when flag.StartsWith('-') && !File.Exists(flag):
                    return new CliCommand(name, files, target, output, $"Opción desconocida: {flag}");
                default:
                    files.Add(args[i]);
                    break;
            }
        }

        var error = name switch
        {
            "convert" when files.Count == 0 => "Indica al menos un archivo.",
            "convert" when target is null => "Indica el formato de destino con --to.",
            "targets" or "tools" when files.Count == 0 => "Indica al menos un archivo.",
            "apply" when files.Count < 2 => "Uso: apply <herramienta> <archivo>...",
            "convert" or "targets" or "tools" or "apply" or "formats" or "engines" => null,
            _ => $"Comando desconocido: {name}",
        };
        return new CliCommand(name, files, target, output, error, options);
    }
}

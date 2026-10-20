// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kumquat.Core.Conversion;
using Kumquat.Core.Localization;
using Kumquat.Core.Results;
using Kumquat.Core.Text;
using Kumquat.Core.Tools;

namespace Kumquat.Engines.Pdf;

/// <summary>
/// Operaciones de PDF con parámetros: extraer páginas, proteger y desbloquear con contraseña,
/// y editar metadatos. Las contraseñas nunca van en la línea de comandos (otros procesos podrían
/// leerla): qpdf las recibe en un archivo de argumentos temporal que se borra al terminar.
/// </summary>
internal sealed partial class PdfSecureTools(PdfEngines engines, ITempWorkspaceFactory workspaces)
{
    /// <summary>Campos estándar del diccionario Info: clave PDF → etiqueta.</summary>
    public static (string Key, string Label)[] MetadataFields =>
    [
        ("Title", Strings.FieldTitle),
        ("Author", Strings.FieldAuthor),
        ("Subject", Strings.FieldSubject),
        ("Keywords", Strings.FieldKeywords),
        ("Creator", Strings.FieldCreator),
        ("Producer", Strings.FieldProducer),
    ];

    /// <summary>Claves que gestiona el propio PDF y no se ofrecen para editar.</summary>
    private static readonly HashSet<string> Technical = new(StringComparer.Ordinal) { "CreationDate", "ModDate", "Trapped" };

    /// <summary>
    /// Script de <c>mutool run</c>. Modo "read": imprime el diccionario Info en JSON. Modo "write":
    /// aplica un JSON de cambios (valor vacío = borrar) y elimina el XMP del catálogo, que los
    /// lectores priorizan sobre Info y seguiría mostrando los valores viejos.
    /// </summary>
    private const string MetadataScript = """
        var mode = scriptArgs[0];
        var pdf = new PDFDocument(scriptArgs[1]);
        if (pdf.needsPassword()) { throw new Error("password"); }
        function currentInfo() {
          var ref = pdf.getTrailer().get("Info");
          if (ref === null || ref === undefined) return null;
          var dict = ref.resolve();
          return dict !== null && dict.isDictionary() ? dict : null;
        }
        var info = currentInfo();
        if (mode === "read") {
          var out = {};
          if (info !== null) info.forEach(function (v, k) { if (v.isString()) out[k] = v.asString(); });
          print(JSON.stringify(out));
        } else {
          var changes = JSON.parse(read(scriptArgs[3]));
          if (info === null) {
            var created = pdf.addObject(pdf.newDictionary());
            pdf.getTrailer().put("Info", created);
            info = currentInfo();
          }
          for (var k in changes) { if (changes[k] === "") info.delete(k); else info.put(k, pdf.newString(changes[k])); }
          var root = pdf.getTrailer().get("Root");
          if (root !== null && root !== undefined) { root.resolve().delete("Metadata"); }
          pdf.save(scriptArgs[2], "compress");
        }
        """;

    // ---------- Extraer páginas ----------

    public static IReadOnlyList<ToolParameter> ExtractParameters() =>
        [new ToolParameter("pages", Strings.ParamPages, ToolParameterKind.Pages, "1", Strings.HintPages, Required: true)];

    public static string? ValidateExtract(IReadOnlyDictionary<string, string> options)
        => PageRanges.TryNormalize(options.GetValueOrDefault("pages"), out _) ? null : Strings.ValidatePages;

    public Task<Result> ExtractAsync(ToolStep step, CancellationToken cancellationToken)
    {
        if (!PageRanges.TryNormalize(step.Option("pages"), out var pages))
        {
            return Task.FromResult(Result.Failure(new Error("invalid-options", Strings.ErrorInvalidPageRange)));
        }

        return engines.RunQpdfAsync([PdfEngines.Abs(step.InputPath), "--pages", ".", pages, "--", PdfEngines.Abs(step.OutputPath)], cancellationToken);
    }

    // ---------- Proteger / desbloquear ----------

    public static IReadOnlyList<ToolParameter> ProtectParameters() =>
    [
        new ToolParameter("password", Strings.ParamPassword, ToolParameterKind.Password, Required: true),
        new ToolParameter("confirm", Strings.ParamConfirmPassword, ToolParameterKind.Password, Required: true),
    ];

    public static string? ValidateProtect(IReadOnlyDictionary<string, string> options)
    {
        var password = options.GetValueOrDefault("password") ?? string.Empty;
        if (password.Length == 0)
        {
            return Strings.ValidatePasswordEmpty;
        }

        if (password.Contains('\n', StringComparison.Ordinal) || password.Contains('\r', StringComparison.Ordinal))
        {
            return Strings.ValidatePasswordNewline;
        }

        return string.Equals(password, options.GetValueOrDefault("confirm"), StringComparison.Ordinal) ? null : Strings.ValidatePasswordMismatch;
    }

    public Task<Result> ProtectAsync(ToolStep step, CancellationToken cancellationToken)
    {
        var password = step.Option("password") ?? string.Empty;

        // AES-256; la misma contraseña para abrir y para cambiar permisos.
        return RunQpdfWithArgumentFileAsync(["--encrypt", password, password, "256", "--", PdfEngines.Abs(step.InputPath), PdfEngines.Abs(step.OutputPath)], cancellationToken);
    }

    public static IReadOnlyList<ToolParameter> UnlockParameters() =>
        [new ToolParameter("password", Strings.ParamPassword, ToolParameterKind.Password, Hint: Strings.HintUnlockPassword)];

    public static string? ValidateUnlock(IReadOnlyDictionary<string, string> options)
    {
        var password = options.GetValueOrDefault("password") ?? string.Empty;
        return password.Contains('\n', StringComparison.Ordinal) || password.Contains('\r', StringComparison.Ordinal) ? Strings.ValidatePasswordNewline : null;
    }

    public Task<Result> UnlockAsync(ToolStep step, CancellationToken cancellationToken)
        => DecryptAsync(step.InputPath, step.OutputPath, step.Option("password") ?? string.Empty, cancellationToken);

    // ---------- Herramientas sobre un PDF con contraseña ----------

    /// <summary>Clave de la contraseña con que se abre un PDF protegido: las herramientas la piden solo si hace falta.</summary>
    public const string OpenPasswordKey = "pdf-password";

    public static ToolParameter OpenPasswordParameter(PdfPasswordHandling handling) => handling switch
    {
        PdfPasswordHandling.Replace => new ToolParameter(OpenPasswordKey, Strings.ParamCurrentPassword, ToolParameterKind.Password, Required: true),
        PdfPasswordHandling.Drop => new ToolParameter(OpenPasswordKey, Strings.ParamPdfPassword, ToolParameterKind.Password, Hint: Strings.HintPdfPasswordImages, Required: true),
        _ => new ToolParameter(OpenPasswordKey, Strings.ParamPdfPassword, ToolParameterKind.Password, Hint: Strings.HintPdfPasswordKept, Required: true),
    };

    public static string? ValidateOpenPassword(IReadOnlyDictionary<string, string> options)
    {
        var password = options.GetValueOrDefault(OpenPasswordKey) ?? string.Empty;
        return password.Contains('\n', StringComparison.Ordinal) || password.Contains('\r', StringComparison.Ordinal) ? Strings.ValidatePasswordNewline : null;
    }

    /// <summary>
    /// Aplica una herramienta a un PDF con contraseña sin que haga falta desbloquearlo antes: lo abre
    /// en una carpeta temporal (con su mismo nombre, para que las páginas o imágenes que salen se
    /// llamen igual), aplica la herramienta y, según <paramref name="handling"/>, vuelve a proteger cada
    /// PDF resultante con la misma contraseña (AES-256). La copia abierta se borra al terminar.
    /// </summary>
    public async Task<Result> WithOpenPasswordAsync(ToolStep step, string password, PdfPasswordHandling handling, Func<ToolStep, CancellationToken, Task<Result>> apply, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(apply);
        using var workspace = workspaces.Create();
        var open = Path.Combine(Directory.CreateDirectory(Path.Combine(workspace.DirectoryPath, "abierto")).FullName, Path.GetFileName(step.InputPath));
        var opened = await DecryptAsync(step.InputPath, open, password, cancellationToken);
        if (!opened.IsSuccess)
        {
            return opened;
        }

        var result = await apply(step with { Inputs = [step.Inputs[0] with { Path = open }] }, cancellationToken);
        if (!result.IsSuccess || handling != PdfPasswordHandling.Keep)
        {
            return result;
        }

        var outputs = Directory.Exists(step.OutputPath)
            ? Directory.EnumerateFiles(step.OutputPath, "*.pdf", SearchOption.AllDirectories).ToList()
            : [step.OutputPath];
        foreach (var output in outputs)
        {
            var locked = workspace.NewFilePath(".pdf");
            var protectedResult = await RunQpdfWithArgumentFileAsync(["--encrypt", password, password, "256", "--", PdfEngines.Abs(output), PdfEngines.Abs(locked)], cancellationToken);
            if (!protectedResult.IsSuccess)
            {
                return protectedResult;
            }

            File.Move(locked, output, overwrite: true);
        }

        return Result.Success;
    }

    private async Task<Result> DecryptAsync(string input, string output, string password, CancellationToken cancellationToken)
    {
        var result = await RunQpdfWithArgumentFileAsync([$"--password={password}", "--decrypt", PdfEngines.Abs(input), PdfEngines.Abs(output)], cancellationToken);
        return string.Equals(result.Error?.Code, "pdf-encrypted", StringComparison.Ordinal) ? new Error("wrong-password", Strings.ErrorWrongPassword) : result;
    }

    // ---------- Metadatos ----------

    public async Task<IReadOnlyList<ToolParameter>> MetadataParametersAsync(string input, CancellationToken cancellationToken)
    {
        var current = await ReadMetadataAsync(input, cancellationToken);
        var known = MetadataFields.Select(f => new ToolParameter(f.Key, f.Label, ToolParameterKind.Text, current.GetValueOrDefault(f.Key, string.Empty)));
        var custom = current
            .Where(kv => !Technical.Contains(kv.Key) && !MetadataFields.Any(f => string.Equals(f.Key, kv.Key, StringComparison.Ordinal)))
            .Select(kv => new ToolParameter(ToolParameter.CustomPrefix + kv.Key, kv.Key, ToolParameterKind.Text, kv.Value));
        return [.. known, .. custom, new ToolParameter("custom", Strings.ParamCustomFields, ToolParameterKind.CustomFields)];
    }

    public static string? ValidateMetadata(IReadOnlyDictionary<string, string> options)
    {
        var bad = options.Keys.Select(Key).FirstOrDefault(k => !PdfName().IsMatch(k));
        return bad is null ? null : Strings.ValidateFieldName(bad);
    }

    public async Task<Result> WriteMetadataAsync(ToolStep step, CancellationToken cancellationToken)
    {
        using var workspace = workspaces.Create();
        var script = workspace.NewFilePath(".js");
        var changes = workspace.NewFilePath(".json");
        await File.WriteAllTextAsync(script, MetadataScript, new UTF8Encoding(false), cancellationToken);
        var map = (step.Options ?? new Dictionary<string, string>(StringComparer.Ordinal)).ToDictionary(kv => Key(kv.Key), kv => kv.Value, StringComparer.Ordinal);
        await File.WriteAllTextAsync(changes, JsonSerializer.Serialize(map, PdfJson.Default.DictionaryStringString), new UTF8Encoding(false), cancellationToken);
        var result = await engines.RunMutoolAsync(["run", script, "write", PdfEngines.Abs(step.InputPath), PdfEngines.Abs(step.OutputPath), changes], cancellationToken);
        return string.Equals(result.Error?.Code, "pdf-encrypted", StringComparison.Ordinal)
            ? new Error("pdf-encrypted", Strings.ErrorPdfUnlockFirst)
            : result;
    }

    /// <summary>Lee el diccionario Info (vacío si no hay o no se puede leer).</summary>
    public async Task<IReadOnlyDictionary<string, string>> ReadMetadataAsync(string input, CancellationToken cancellationToken)
    {
        using var workspace = workspaces.Create();
        var script = workspace.NewFilePath(".js");
        await File.WriteAllTextAsync(script, MetadataScript, new UTF8Encoding(false), cancellationToken);
        var json = await engines.RunMutoolForOutputAsync(["run", script, "read", PdfEngines.Abs(input)], cancellationToken);
        try
        {
            return json is null ? new Dictionary<string, string>(StringComparer.Ordinal) : JsonSerializer.Deserialize(json, PdfJson.Default.DictionaryStringString) ?? [];
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private static string Key(string key) => key.StartsWith(ToolParameter.CustomPrefix, StringComparison.Ordinal) ? key[ToolParameter.CustomPrefix.Length..] : key;

    private async Task<Result> RunQpdfWithArgumentFileAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        using var workspace = workspaces.Create();
        var argumentFile = workspace.NewFilePath(".args");
        await File.WriteAllLinesAsync(argumentFile, arguments, new UTF8Encoding(false), cancellationToken);
        return await engines.RunQpdfAsync(["@" + argumentFile], cancellationToken);
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 200)]
    private static partial Regex PdfName();
}

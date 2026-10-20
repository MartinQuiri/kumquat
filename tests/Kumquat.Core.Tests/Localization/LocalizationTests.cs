// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Kumquat.Core.Localization;

namespace Kumquat.Core.Tests.Localization;

/// <summary>Cada idioma tiene todos los textos, con los mismos huecos, y el código generado está al día.</summary>
public sealed partial class LocalizationTests
{
    private static readonly string Folder = Path.Combine(RepoRoot(), "src", "Kumquat.Core", "Localization");

    public static TheoryData<string> Translations => new(Directory.GetFiles(Folder, "Strings.*.resx").Select(Path.GetFileName)!);

    [Theory]
    [MemberData(nameof(Translations))]
    public void Traduccion_MismasClavesYHuecos(string file)
    {
        var baseTexts = Read("Strings.resx");
        var translated = Read(file);

        translated.Keys.Order(StringComparer.Ordinal).ShouldBe(baseTexts.Keys.Order(StringComparer.Ordinal), $"{file} debe tener las mismas claves que Strings.resx");
        foreach (var (key, text) in baseTexts)
        {
            Holes(translated[key]).ShouldBe(Holes(text), $"{file}: {key} debe usar los mismos {{n}}");
            translated[key].ShouldNotBeNullOrWhiteSpace(key);
        }
    }

    [Fact]
    public void CodigoGenerado_AlDia()
    {
        var members = typeof(Strings).GetMembers(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m is PropertyInfo || (m is MethodInfo method && !method.IsSpecialName))
            .Select(m => m.Name)
            .Except(["Languages", "Resolve", "Get"])
            .Order(StringComparer.Ordinal);

        members.ShouldBe(Read("Strings.resx").Keys.Order(StringComparer.Ordinal), "regenerar con: python tools/build-strings.py");
        foreach (var (key, text) in Read("Strings.resx"))
        {
            var method = typeof(Strings).GetMethod(key);
            (method?.GetParameters().Length ?? 0).ShouldBe(Holes(text).Count, key);
        }
    }

    [Fact]
    public void Ingles_SeLeeDelSatelite()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            Strings.ErrorCancelled.ShouldBe("The operation was cancelled.");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("es-MX");
            Strings.ErrorCancelled.ShouldBe("La operación fue cancelada.");
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Theory]
    [InlineData("auto", "es-MX", "es-MX")]
    [InlineData("auto", "en-GB", "en-GB")]
    [InlineData("auto", "fr-FR", "en")]
    [InlineData("en", "es-AR", "en")]
    [InlineData("es", "en-US", "es")]
    [InlineData("xx", "es-ES", "es-ES")]
    [InlineData(null, "de-DE", "en")]
    public void Idioma_AutoUsaElDeWindowsSiEstaTraducido(string? choice, string system, string expected)
        => Strings.Resolve(choice, CultureInfo.GetCultureInfo(system)).Name.ShouldBe(expected);

    private static Dictionary<string, string> Read(string file)
        => XDocument.Load(Path.Combine(Folder, file)).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);

    private static List<int> Holes(string text)
        => [.. Hole().Matches(text).Select(m => int.Parse(m.Groups["n"].Value, CultureInfo.InvariantCulture)).Distinct().Order()];

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Kumquat.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
    }

    [GeneratedRegex(@"\{(?<n>\d+)(?:[,:][^}]*)?\}", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Hole();
}

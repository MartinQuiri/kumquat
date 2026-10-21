// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Infrastructure.Engines;

namespace Kumquat.Infrastructure.Tests.Engines;

public sealed class EngineLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kumquat-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Instalado_JuntoALaApp()
    {
        var exe = Touch("pkg", "engines", "ffmpeg", "bin", "ffmpeg.exe");

        new EngineLocator(Path.Combine(_root, "pkg"), overrideRoot: string.Empty).Find("ffmpeg", Path.Combine("bin", "ffmpeg.exe")).ShouldBe(exe);
    }

    [Fact]
    public void Instalado_DesdeSubcarpetaCli()
    {
        var exe = Touch("pkg", "engines", "ffmpeg", "bin", "ffmpeg.exe");
        Directory.CreateDirectory(Path.Combine(_root, "pkg", "cli"));

        new EngineLocator(Path.Combine(_root, "pkg", "cli"), overrideRoot: string.Empty).Find("ffmpeg", Path.Combine("bin", "ffmpeg.exe")).ShouldBe(exe);
    }

    [Fact]
    public void Desarrollo_RepoConCarpetaPorRid()
    {
        var exe = Touch("repo", "engines", "ffmpeg", EngineLocator.RuntimeIdentifier, "bin", "ffmpeg.exe");
        var appBase = Directory.CreateDirectory(Path.Combine(_root, "repo", "src", "App", "bin", "Debug")).FullName;

        new EngineLocator(appBase, overrideRoot: string.Empty).Find("ffmpeg", Path.Combine("bin", "ffmpeg.exe")).ShouldBe(exe);
    }

    [Fact]
    public void Override_TienePrioridad()
    {
        Touch("pkg", "engines", "ffmpeg", "bin", "ffmpeg.exe");
        var forced = Touch("otro", "ffmpeg", "bin", "ffmpeg.exe");

        new EngineLocator(Path.Combine(_root, "pkg"), overrideRoot: Path.Combine(_root, "otro")).Find("ffmpeg", Path.Combine("bin", "ffmpeg.exe")).ShouldBe(forced);
    }

    [Fact]
    public void NoInstalado_DevuelveNull()
    {
        new EngineLocator(Directory.CreateDirectory(Path.Combine(_root, "vacio")).FullName, overrideRoot: string.Empty)
            .Find("ffmpeg", Path.Combine("bin", "ffmpeg.exe")).ShouldBeNull();
    }

    [Fact]
    public void RuntimeIdentifier_EsDelSistemaActual()
    {
        var prefix = OperatingSystem.IsWindows() ? "win-" : OperatingSystem.IsMacOS() ? "osx-" : "linux-";

        EngineLocator.RuntimeIdentifier.ShouldStartWith(prefix);
        EngineLocator.RuntimeIdentifier.ShouldBeOneOf(prefix + "x64", prefix + "arm64");
    }

    [Fact]
    public void Ejecutable_AgregaLaExtensionDelSistema()
    {
        var exe = Touch("pkg", "engines", "ffmpeg", "bin", "ffprobe" + EngineLocator.ExecutableExtension);

        new EngineLocator(Path.Combine(_root, "pkg"), overrideRoot: string.Empty, searchPath: string.Empty).FindExecutable("ffmpeg", "ffprobe", "bin").ShouldBe(exe);
    }

    [Fact]
    public void Ejecutable_PropioAntesQueElDelSistema()
    {
        var own = Touch("pkg", "engines", "qpdf", "bin", "qpdf" + EngineLocator.ExecutableExtension);
        Touch("sistema", "qpdf" + EngineLocator.ExecutableExtension);

        new EngineLocator(Path.Combine(_root, "pkg"), overrideRoot: string.Empty, searchPath: Path.Combine(_root, "sistema")).FindExecutable("qpdf", "qpdf", "bin").ShouldBe(own);
    }

    [Fact]
    public void Ejecutable_DelPath_SoloFueraDeWindowsYConRutasAbsolutas()
    {
        var system = Touch("sistema", "mutool" + EngineLocator.ExecutableExtension);
        var searchPath = string.Join(Path.PathSeparator, "relativa", Path.Combine(_root, "otra"), Path.Combine(_root, "sistema"));
        var locator = new EngineLocator(Directory.CreateDirectory(Path.Combine(_root, "vacio")).FullName, overrideRoot: string.Empty, searchPath: searchPath);

        // En Windows se usan solo los motores propios (verificados por hash); en Linux y macOS, los del gestor de paquetes.
        locator.FindExecutable("mupdf", "mutool").ShouldBe(OperatingSystem.IsWindows() ? null : system);
    }

    private string Touch(params string[] parts)
    {
        var path = Path.Combine([_root, .. parts]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);
        return path;
    }
}

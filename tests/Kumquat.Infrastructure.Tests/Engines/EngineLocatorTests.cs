// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Infrastructure.Engines;

namespace Kumquat.Infrastructure.Tests.Engines;

public sealed class EngineLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kumquat-tests", Guid.NewGuid().ToString("N"));

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Instalado_JuntoALaApp()
    {
        var exe = Touch("pkg", "engines", "ffmpeg", "bin", "ffmpeg.exe");

        new EngineLocator(Path.Combine(_root, "pkg"), overrideRoot: string.Empty).Find("ffmpeg", @"bin\ffmpeg.exe").ShouldBe(exe);
    }

    [Fact]
    public void Instalado_DesdeSubcarpetaCli()
    {
        var exe = Touch("pkg", "engines", "ffmpeg", "bin", "ffmpeg.exe");
        Directory.CreateDirectory(Path.Combine(_root, "pkg", "cli"));

        new EngineLocator(Path.Combine(_root, "pkg", "cli"), overrideRoot: string.Empty).Find("ffmpeg", @"bin\ffmpeg.exe").ShouldBe(exe);
    }

    [Fact]
    public void Desarrollo_RepoConCarpetaPorRid()
    {
        var exe = Touch("repo", "engines", "ffmpeg", EngineLocator.RuntimeIdentifier, "bin", "ffmpeg.exe");
        var appBase = Directory.CreateDirectory(Path.Combine(_root, "repo", "src", "App", "bin", "Debug")).FullName;

        new EngineLocator(appBase, overrideRoot: string.Empty).Find("ffmpeg", @"bin\ffmpeg.exe").ShouldBe(exe);
    }

    [Fact]
    public void Override_TienePrioridad()
    {
        Touch("pkg", "engines", "ffmpeg", "bin", "ffmpeg.exe");
        var forced = Touch("otro", "ffmpeg", "bin", "ffmpeg.exe");

        new EngineLocator(Path.Combine(_root, "pkg"), overrideRoot: Path.Combine(_root, "otro")).Find("ffmpeg", @"bin\ffmpeg.exe").ShouldBe(forced);
    }

    [Fact]
    public void NoInstalado_DevuelveNull()
    {
        new EngineLocator(Directory.CreateDirectory(Path.Combine(_root, "vacio")).FullName, overrideRoot: string.Empty)
            .Find("ffmpeg", @"bin\ffmpeg.exe").ShouldBeNull();
    }

    private string Touch(params string[] parts)
    {
        var path = Path.Combine([_root, .. parts]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);
        return path;
    }
}

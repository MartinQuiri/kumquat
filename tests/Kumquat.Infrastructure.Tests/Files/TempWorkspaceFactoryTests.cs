// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Infrastructure.Files;
using Microsoft.Extensions.Time.Testing;

namespace Kumquat.Infrastructure.Tests.Files;

public sealed class TempWorkspaceFactoryTests : IDisposable
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
    public void Create_YDispose_BorraLaCarpeta()
    {
        var factory = new TempWorkspaceFactory(TimeProvider.System, _root);
        string path;
        using (var workspace = factory.Create())
        {
            path = workspace.DirectoryPath;
            File.WriteAllText(workspace.NewFilePath(".txt"), "x");
            Directory.Exists(path).ShouldBeTrue();
        }

        Directory.Exists(path).ShouldBeFalse();
    }

    [Theory]
    [InlineData("txt")]
    [InlineData(".\\..\\x")]
    [InlineData("./x")]
    [InlineData(".a:b")]
    public void NewFilePath_ExtensionInvalida_Lanza(string extension)
    {
        using var workspace = new TempWorkspaceFactory(TimeProvider.System, _root).Create();

        Should.Throw<ArgumentException>(() => workspace.NewFilePath(extension));
    }

    [Fact]
    public void CleanupStale_BorraSoloLasViejas()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var factory = new TempWorkspaceFactory(clock, _root);
        var old = Directory.CreateDirectory(Path.Combine(_root, "vieja"));
        old.LastWriteTimeUtc = clock.GetUtcNow().UtcDateTime.AddDays(-3);
        var fresh = Directory.CreateDirectory(Path.Combine(_root, "nueva"));

        factory.CleanupStale(TimeSpan.FromDays(1)).ShouldBe(1);

        old.Refresh();
        fresh.Refresh();
        old.Exists.ShouldBeFalse();
        fresh.Exists.ShouldBeTrue();
    }
}

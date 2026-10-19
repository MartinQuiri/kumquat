// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Conversion;

namespace Kumquat.Core.Tests;

/// <summary>Carpeta temporal aislada por test.</summary>
internal sealed class TestDirectory : IDisposable
{
    public TestDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kumquat-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Write(string name, string content)
    {
        var path = System.IO.Path.Combine(Path, name);
        File.WriteAllText(path, content);
        return path;
    }

    public IReadOnlyList<string> LeftoverStagingFiles()
        => Directory.GetFiles(Path, ".kumquat-*", SearchOption.TopDirectoryOnly);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Mejor esfuerzo.
        }
    }
}

internal sealed class TestWorkspaceFactory(string root) : ITempWorkspaceFactory
{
    public string? LastCreated { get; private set; }

    public ITempWorkspace Create()
    {
        var workspace = new TestWorkspace(Path.Combine(root, "ws-" + Guid.NewGuid().ToString("N")));
        LastCreated = workspace.DirectoryPath;
        return workspace;
    }

    private sealed class TestWorkspace : ITempWorkspace
    {
        public TestWorkspace(string path)
        {
            DirectoryPath = path;
            Directory.CreateDirectory(path);
        }

        public string DirectoryPath { get; }

        public string NewFilePath(string extension) => Path.Combine(DirectoryPath, Guid.NewGuid().ToString("N") + extension);

        public void Dispose() => Directory.Delete(DirectoryPath, recursive: true);
    }
}

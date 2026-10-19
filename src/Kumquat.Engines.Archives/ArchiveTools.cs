// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.IO.Compression;
using Kumquat.Core.Formats;
using Kumquat.Core.Results;
using Kumquat.Core.Tools;
using Kumquat.Infrastructure.Files;

namespace Kumquat.Engines.Archives;

/// <summary>Herramientas de archivos comprimidos, sin motores externos.</summary>
public static class ArchiveTools
{
    /// <summary>Herramientas en el orden de la rueda.</summary>
    public static IReadOnlyList<ITool> CreateAll() => [new ZipTool(), new ExtractTool()];

    /// <summary>Comprime archivos y carpetas (juntos) en un ZIP.</summary>
    private sealed class ZipTool : ITool
    {
        public string Id => "archive.zip";

        public string Label => "Comprimir en ZIP";

        public string OutputSuffix => string.Empty;

        public bool IsAvailable => true;

        public ToolMode Mode => ToolMode.Combine;

        public bool AcceptsDirectories => true;

        public bool AppliesTo(FileFormat format) => true;

        public FileFormat? OutputFormat(FileFormat? input) => KnownFormats.Zip;

        public async Task<Result> ApplyAsync(ToolStep step, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(step);
            var files = step.Inputs.SelectMany(Expand).ToList();
            long totalBytes = Math.Max(1, files.Sum(f => f.Length));
            long done = 0;

            await using var stream = new FileStream(step.OutputPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, useAsync: true);
            await using var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);
            foreach (var (path, entryName, length) in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
                entry.LastWriteTime = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
                await using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true))
                await using (var output = await entry.OpenAsync(cancellationToken))
                {
                    await input.CopyToAsync(output, cancellationToken);
                }

                done += length;
                progress?.Report((double)done / totalBytes);
            }

            return Result.Success;
        }

        /// <summary>Archivos con su nombre dentro del ZIP. Las carpetas conservan su nombre como raíz; los enlaces simbólicos se omiten (evita ciclos).</summary>
        private static IEnumerable<(string Path, string EntryName, long Length)> Expand(ToolInput input)
        {
            if (!input.IsDirectory)
            {
                yield return (input.Path, Path.GetFileName(input.Path), new FileInfo(input.Path).Length);
                yield break;
            }

            var root = new DirectoryInfo(input.Path);
            var parent = root.Parent?.FullName ?? root.FullName;
            var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true };
            foreach (var file in root.EnumerateFiles("*", options))
            {
                yield return (file.FullName, Path.GetRelativePath(parent, file.FullName).Replace(Path.DirectorySeparatorChar, '/'), file.Length);
            }
        }
    }

    /// <summary>Extrae ZIP, RAR, 7z, TAR y GZIP a una carpeta junto al archivo.</summary>
    private sealed class ExtractTool : ITool
    {
        private static readonly FileFormat[] Archives = [KnownFormats.Zip, KnownFormats.Rar, KnownFormats.SevenZip, KnownFormats.Tar, KnownFormats.Gzip];

        public string Id => "archive.extract";

        public string Label => "Extraer";

        public string OutputSuffix => string.Empty;

        public bool IsAvailable => true;

        public bool AppliesTo(FileFormat format) => Archives.Contains(format);

        public FileFormat? OutputFormat(FileFormat? input) => null;

        public async Task<Result> ApplyAsync(ToolStep step, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(step);
            var result = await SafeExtractor.ExtractAsync(step.InputPath, step.OutputPath, null, cancellationToken);
            return result.IsSuccess ? Result.Success : result.Error;
        }
    }
}

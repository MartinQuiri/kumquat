// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;
using Kumquat.Interaction.Native;

namespace Kumquat.Interaction.DragDrop;

/// <summary>
/// Archivos "virtuales": lo que se arrastra sin ruta en disco (un adjunto de Outlook, un archivo
/// de dentro de un .zip abierto en el Explorador, una imagen de una página web). Llegan como
/// <c>FileGroupDescriptorW</c> (nombres) + <c>FileContents</c> (contenido) y se copian a una carpeta
/// temporal propia para procesarlos como cualquier archivo.
/// </summary>
/// <remarks>
/// El contenido solo se puede leer mientras dura el drop: se extrae en <c>Drop</c>, no antes (en
/// <c>DragEnter</c> el programa de origen quedaría esperando a que se copie todo).
/// </remarks>
public static unsafe partial class VirtualDrops
{
    /// <summary>Máximo de elementos por arrastre.</summary>
    public const int MaxItems = 64;

    /// <summary>Máximo total a copiar (4 GiB).</summary>
    public const long MaxBytes = 4L * 1024 * 1024 * 1024;

    private const uint DvaspectContent = 1;
    private const uint TymedHglobal = 1;
    private const uint TymedIstream = 4;
    private const int GetDataSlot = 3;
    private const int StreamReadSlot = 3; // ISequentialStream::Read, tras los 3 de IUnknown
    private const int DescriptorSize = 592;
    private const int NameOffset = 72;
    private const int AttributesOffset = 36;
    private const uint FileAttributeDirectory = 0x10;
    private const uint FdAttributes = 0x4;

    private static readonly Lazy<ushort> DescriptorFormat = new(() => (ushort)RegisterClipboardFormatW("FileGroupDescriptorW"));
    private static readonly Lazy<ushort> ContentsFormat = new(() => (ushort)RegisterClipboardFormatW("FileContents"));

    /// <summary>Carpeta donde se copian (se limpia sola al arrancar si quedó algo de una sesión anterior).</summary>
    public static string Root { get; } = Path.Combine(Path.GetTempPath(), "Kumquat", "virtual");

    /// <summary>Indica si una ruta es la copia de un archivo virtual (sus resultados van a otra carpeta).</summary>
    public static bool IsExtracted(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Path.GetFullPath(path).StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Borra la carpeta temporal de un arrastre (mejor esfuerzo).</summary>
    public static void Discard(IReadOnlyList<string> extracted)
    {
        ArgumentNullException.ThrowIfNull(extracted);
        foreach (var folder in extracted.Where(IsExtracted).Select(Path.GetDirectoryName).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                Directory.Delete(folder!, recursive: true);
            }
            catch (IOException)
            {
                // Un archivo abierto todavía: se limpia en el próximo arranque.
            }
            catch (UnauthorizedAccessException)
            {
                // Idem.
            }
        }
    }

    /// <summary>Borra copias de sesiones anteriores (Kumquat se cerró a mitad de un lote).</summary>
    public static int CleanupStale(TimeSpan maxAge)
    {
        if (!Directory.Exists(Root))
        {
            return 0;
        }

        var cleaned = 0;
        foreach (var folder in Directory.EnumerateDirectories(Root))
        {
            try
            {
                if (DateTime.UtcNow - Directory.GetLastWriteTimeUtc(folder) > maxAge)
                {
                    Directory.Delete(folder, recursive: true);
                    cleaned++;
                }
            }
            catch (IOException)
            {
                // En uso: la próxima vez.
            }
            catch (UnauthorizedAccessException)
            {
                // Idem.
            }
        }

        return cleaned;
    }

    /// <summary>
    /// Nombre seguro para un archivo que viene de otro programa: sin carpetas (nada de <c>..\</c>),
    /// sin caracteres inválidos y nunca vacío.
    /// </summary>
    public static string SafeName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var leaf = Path.GetFileName(name.Replace('/', '\\').TrimEnd('\\'));
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string([.. leaf.Select(c => invalid.Contains(c) ? '_' : c)]).Trim().TrimEnd('.');
        return clean.Length == 0 || clean is "." or ".." ? "archivo" : clean;
    }

    /// <summary>Nombres de los archivos virtuales (sin carpetas), o vacío si no hay.</summary>
    internal static IReadOnlyList<string> ReadNames(nint dataObject)
        => [.. ReadDescriptors(dataObject).Select(d => d.Name)];

    /// <summary>Copia todos los archivos virtuales a una carpeta temporal nueva y devuelve sus rutas.</summary>
    internal static IReadOnlyList<string> Extract(nint dataObject)
    {
        var descriptors = ReadDescriptors(dataObject);
        if (descriptors.Count == 0)
        {
            return [];
        }

        var folder = Path.Combine(Root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var paths = new List<string>(descriptors.Count);
        long budget = MaxBytes;
        foreach (var descriptor in descriptors)
        {
            var path = UniquePath(folder, descriptor.Name);
            var written = CopyContents(dataObject, descriptor.Index, path, budget);
            if (written < 0)
            {
                File.Delete(path);
                continue;
            }

            budget -= written;
            paths.Add(path);
        }

        if (paths.Count == 0)
        {
            Directory.Delete(folder, recursive: true);
        }

        return paths;
    }

    private static string UniquePath(string folder, string name)
    {
        var path = Path.Combine(folder, name);
        for (var i = 2; File.Exists(path); i++)
        {
            path = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(name)} ({i}){Path.GetExtension(name)}");
        }

        return path;
    }

    private static List<(int Index, string Name)> ReadDescriptors(nint dataObject)
    {
        var result = new List<(int, string)>();
        var format = new FORMATETC { CfFormat = DescriptorFormat.Value, DwAspect = DvaspectContent, Lindex = -1, Tymed = TymedHglobal };
        if (dataObject == 0 || GetData(dataObject, &format, out var medium) != 0)
        {
            return result;
        }

        try
        {
            var block = (byte*)GlobalLock(medium.Handle);
            if (block is null)
            {
                return result;
            }

            try
            {
                var size = (long)GlobalSize(medium.Handle);
                var count = Math.Min(*(uint*)block, MaxItems);
                for (var i = 0; i < count && 4 + ((i + 1) * (long)DescriptorSize) <= size; i++)
                {
                    var descriptor = block + 4 + (i * DescriptorSize);
                    var flags = *(uint*)descriptor;
                    var attributes = *(uint*)(descriptor + AttributesOffset);
                    if ((flags & FdAttributes) != 0 && (attributes & FileAttributeDirectory) != 0)
                    {
                        continue; // las carpetas no se copian (sus archivos vienen como elementos aparte)
                    }

                    var name = new string((char*)(descriptor + NameOffset)).TrimEnd('\0');
                    result.Add((i, SafeName(name)));
                }
            }
            finally
            {
                GlobalUnlock(medium.Handle);
            }
        }
        finally
        {
            Win32.ReleaseStgMedium(&medium);
        }

        return result;
    }

    /// <summary>Escribe el contenido del elemento; devuelve los bytes escritos o -1 si no se pudo.</summary>
    private static long CopyContents(nint dataObject, int index, string path, long budget)
    {
        var format = new FORMATETC { CfFormat = ContentsFormat.Value, DwAspect = DvaspectContent, Lindex = index, Tymed = TymedHglobal | TymedIstream };
        if (GetData(dataObject, &format, out var medium) != 0)
        {
            return -1;
        }

        try
        {
            using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            return medium.Tymed switch
            {
                TymedHglobal => CopyFromGlobal(medium.Handle, file, budget),
                TymedIstream => CopyFromStream(medium.Handle, file, budget),
                _ => -1,
            };
        }
        finally
        {
            Win32.ReleaseStgMedium(&medium);
        }
    }

    private static long CopyFromGlobal(nint handle, FileStream file, long budget)
    {
        var size = (long)GlobalSize(handle);
        var data = (byte*)GlobalLock(handle);
        if (data is null || size > budget)
        {
            return -1;
        }

        try
        {
            for (long offset = 0; offset < size; offset += int.MaxValue)
            {
                file.Write(new ReadOnlySpan<byte>(data + offset, (int)Math.Min(int.MaxValue, size - offset)));
            }

            return size;
        }
        finally
        {
            GlobalUnlock(handle);
        }
    }

    private static long CopyFromStream(nint stream, FileStream file, long budget)
    {
        var read = (delegate* unmanaged[Stdcall]<nint, byte*, uint, uint*, int>)(*(void***)stream)[StreamReadSlot];
        var buffer = new byte[1 << 20];
        long total = 0;
        fixed (byte* p = buffer)
        {
            while (true)
            {
                uint count;
                var hr = read(stream, p, (uint)buffer.Length, &count);
                if (hr < 0)
                {
                    return -1;
                }

                if (count == 0)
                {
                    return total;
                }

                total += count;
                if (total > budget)
                {
                    return -1;
                }

                file.Write(buffer, 0, (int)count);
            }
        }
    }

    private static int GetData(nint dataObject, FORMATETC* format, out STGMEDIUM medium)
    {
        var getData = (delegate* unmanaged[Stdcall]<nint, FORMATETC*, STGMEDIUM*, int>)(*(void***)dataObject)[GetDataSlot];
        STGMEDIUM result;
        var hr = getData(dataObject, format, &result);
        medium = result;
        return hr;
    }

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint RegisterClipboardFormatW(string name);

    [LibraryImport("kernel32.dll")]
    private static partial nint GlobalLock(nint handle);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalUnlock(nint handle);

    [LibraryImport("kernel32.dll")]
    private static partial nuint GlobalSize(nint handle);
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Kumquat.ShellExtension.Tests;

/// <summary>
/// "Kumquat…" del menú moderno de Windows 11. La DLL nativa (Native AOT) se carga como lo hace el
/// Explorador —DllGetClassObject → IClassFactory → IExplorerCommand— desde una carpeta con un
/// Kumquat.exe falso (el probe) que anota con qué argumentos lo abrieron.
/// </summary>
public sealed partial class ShellExtensionTests : IDisposable
{
    private static readonly Guid IidClassFactory = new("00000001-0000-0000-C000-000000000046");
    private static readonly Guid IidExplorerCommand = new("A08CE4D0-FA25-44AB-B57C-C7B1C323E0B9");
    private static readonly StrategyBasedComWrappers ComWrappers = new();

    private readonly string _dir = Directory.CreateTempSubdirectory("kumquat-shellext-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // La DLL nativa queda cargada (su runtime no se descarga): la carpeta se borra con el resto de %TEMP%.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public void Lotes_NoPasanDelLimiteYConservanElOrden()
    {
        var paths = Enumerable.Range(0, 1000).Select(i => $@"C:\carpeta con espacios\archivo número {i:D4}.png").ToList();

        var batches = WheelLaunch.Batches(paths, @"C:\Kumquat\Kumquat.exe", maxLength: 2000);

        batches.Count.ShouldBeGreaterThan(1);
        batches.SelectMany(b => b).ShouldBe(paths);
        batches.ShouldAllBe(b => @"C:\Kumquat\Kumquat.exe".Length + 13 + b.Sum(p => p.Length + 3) <= 2000);
        WheelLaunch.Batches([], "k.exe").ShouldBeEmpty();
        WheelLaunch.Batches([new string('x', 5000)], "k.exe", maxLength: 2000).ShouldHaveSingleItem("una ruta larguísima va sola, igual");
    }

    [Fact]
    public void DllNativa_TituloIconoYEstado()
    {
        var command = LoadCommand();
        var a = Touch("foto.png");

        String(command.GetTitle(0, out var title), title).ShouldBe("Kumquat…");
        String(command.GetIcon(0, out var icon), icon).ShouldBe(Path.Combine(_dir, "Kumquat.exe") + ",0");
        command.GetCanonicalName(out var name).ShouldBe(0);
        name.ShouldBe(KumquatCommand.CommandName);
        command.GetFlags(out var flags).ShouldBe(0);
        flags.ShouldBe(0u);

        WithItems([a], items =>
        {
            command.GetState(items, 0, out var state);
            return (int)state;
        }).ShouldBe(0, "habilitado para archivos del disco");
    }

    [Fact]
    public void DllNativa_SeOcultaParaLoQueNoEsDelDisco()
    {
        var command = LoadCommand();

        // Panel de control: un elemento del Explorador sin ruta en disco.
        WithItems(["::{26EE0668-A00A-44D7-9371-BEB064C98683}"], items =>
        {
            command.GetState(items, 0, out var state);
            return (int)state;
        }).ShouldBe(2, "ECS_HIDDEN");
        command.GetState(0, 0, out var none);
        none.ShouldBe(2u, "sin selección");
    }

    [Fact]
    public async Task DllNativa_Invoke_AbreKumquatConTodosLosArchivos()
    {
        var command = LoadCommand();
        var record = Path.Combine(_dir, "args.txt");
        Environment.SetEnvironmentVariable("KUMQUAT_PROBE_ARGS", record);
        try
        {
            var a = Touch("uno.png");
            var b = Touch("dos con espacios ñ.pdf");

            WithItems([a, b], items => command.Invoke(items, 0)).ShouldBe(0);

            for (var i = 0; i < 100 && !File.Exists(record); i++)
            {
                await Task.Delay(100, TestContext.Current.CancellationToken);
            }

            (await File.ReadAllLinesAsync(record, TestContext.Current.CancellationToken)).ShouldBe([$"--wheel|{a}|{b}"], "un solo Kumquat con los dos archivos");
        }
        finally
        {
            Environment.SetEnvironmentVariable("KUMQUAT_PROBE_ARGS", null);
        }
    }

    [Fact]
    public unsafe void DllNativa_OtroClsid_NoDisponible()
    {
        var getClassObject = Export();
        var other = Guid.NewGuid();
        var iid = IidClassFactory;
        nint factory;
        getClassObject(&other, &iid, &factory).ShouldBe(unchecked((int)0x80040111), "CLASS_E_CLASSNOTAVAILABLE");
        factory.ShouldBe(0);
    }

    private static string String(int hr, nint text)
    {
        hr.ShouldBe(0);
        try
        {
            return Marshal.PtrToStringUni(text)!;
        }
        finally
        {
            Marshal.FreeCoTaskMem(text);
        }
    }

    /// <summary>Arma un IShellItemArray real (como el que pasa el Explorador) con estas rutas.</summary>
    private static unsafe int WithItems(string[] paths, Func<nint, int> use)
    {
        var pidls = new nint[paths.Length];
        try
        {
            for (var i = 0; i < paths.Length; i++)
            {
                Marshal.ThrowExceptionForHR(NativeMethods.SHParseDisplayName(paths[i], 0, out pidls[i], 0, out _));
            }

            fixed (nint* list = pidls)
            {
                Marshal.ThrowExceptionForHR(NativeMethods.SHCreateShellItemArrayFromIDLists((uint)pidls.Length, list, out var array));
                try
                {
                    return use(array);
                }
                finally
                {
                    Marshal.Release(array);
                }
            }
        }
        finally
        {
            foreach (var pidl in pidls)
            {
                Marshal.FreeCoTaskMem(pidl);
            }
        }
    }

    private static string NativeDll()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Kumquat.slnx")))
        {
            dir = dir.Parent;
        }

        var rid = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "win-arm64" : "win-x64";
        return Path.Combine(dir!.FullName, "artifacts", "shellext", rid, "Kumquat.ShellExtension.dll");
    }

    private string Touch(string name)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, "x");
        return path;
    }

    /// <summary>Copia la DLL nativa y el probe (como Kumquat.exe) a la carpeta de la prueba y la carga.</summary>
    private unsafe delegate* unmanaged<Guid*, Guid*, nint*, int> Export()
    {
        var dll = NativeDll();
        Assert.SkipUnless(File.Exists(dll), $"Sin la DLL nativa: dotnet publish src/Kumquat.ShellExtension -c Release -r <rid> -o artifacts/shellext/<rid> ({dll}).");
        var copy = Path.Combine(_dir, "Kumquat.ShellExtension.dll");
        File.Copy(dll, copy);
        foreach (var file in Directory.GetFiles(AppContext.BaseDirectory, "Kumquat.TestTools.ProcessProbe.*"))
        {
            File.Copy(file, Path.Combine(_dir, Path.GetFileName(file)));
        }

        File.Copy(Path.Combine(AppContext.BaseDirectory, "Kumquat.TestTools.ProcessProbe.exe"), Path.Combine(_dir, "Kumquat.exe"));
        var library = NativeLibrary.Load(copy);
        return (delegate* unmanaged<Guid*, Guid*, nint*, int>)NativeLibrary.GetExport(library, "DllGetClassObject");
    }

    private unsafe IExplorerCommand LoadCommand()
    {
        var getClassObject = Export();
        var clsid = Exports.Clsid;
        var iid = IidClassFactory;
        nint factoryPointer;
        getClassObject(&clsid, &iid, &factoryPointer).ShouldBe(0);
        var factory = (IClassFactory)ComWrappers.GetOrCreateObjectForComInstance(factoryPointer, CreateObjectFlags.None);
        Marshal.Release(factoryPointer);
        factory.CreateInstance(0, IidExplorerCommand, out var commandPointer).ShouldBe(0);
        var command = (IExplorerCommand)ComWrappers.GetOrCreateObjectForComInstance(commandPointer, CreateObjectFlags.None);
        Marshal.Release(commandPointer);
        return command;
    }

    private static partial class NativeMethods
    {
        [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
        public static partial int SHParseDisplayName(string name, nint bindContext, out nint pidl, uint attributesIn, out uint attributesOut);

        [LibraryImport("shell32.dll")]
        public static unsafe partial int SHCreateShellItemArrayFromIDLists(uint count, nint* pidls, out nint array);
    }
}

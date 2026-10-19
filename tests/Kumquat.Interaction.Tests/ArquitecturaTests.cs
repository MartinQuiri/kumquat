// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Reflection.PortableExecutable;

namespace Kumquat.Interaction.Tests;

/// <summary>
/// Todas las librerías de Kumquat son IL puro (AnyCPU): la arquitectura la decide el ejecutable.
/// Regresión: una librería compilada como ARM64 al construir la solución sin plataforma no se
/// podía cargar en x64 (FileNotFoundException al arrancar).
/// </summary>
public sealed class ArquitecturaTests
{
    [Fact]
    public void LibreriasDeKumquat_SonIlPuro()
    {
        var libraries = Directory.GetFiles(AppContext.BaseDirectory, "Kumquat.*.dll");
        libraries.Length.ShouldBeGreaterThan(8);
        foreach (var path in libraries)
        {
            using var reader = new PEReader(File.OpenRead(path));
            var headers = reader.PEHeaders;
            var name = Path.GetFileName(path);
            headers.CorHeader.ShouldNotBeNull(name);
            if (headers.CorHeader.EntryPointTokenOrRelativeVirtualAddress != 0)
            {
                continue; // ejecutable (app, CLI, tests): ese sí elige arquitectura
            }

            headers.CoffHeader.Machine.ShouldBe(Machine.I386, $"{name} está compilada para una arquitectura concreta");
            headers.CorHeader.Flags.HasFlag(CorFlags.ILOnly).ShouldBeTrue(name);
            headers.CorHeader.Flags.HasFlag(CorFlags.Requires32Bit).ShouldBeFalse(name);
        }
    }
}

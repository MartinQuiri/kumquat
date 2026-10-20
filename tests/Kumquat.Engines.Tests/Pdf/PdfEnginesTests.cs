// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Engines.Pdf;

namespace Kumquat.Engines.Tests.Pdf;

public sealed class PdfEnginesTests
{
    [Theory]
    [InlineData("mutool version 1.28.5", 1, 28)]
    [InlineData("mutool version 1.19.0\n", 1, 19)]
    [InlineData("usage: mutool <command>\nmutool version 1.24.10", 1, 24)]
    public void VersionDeMutool_SeLeeDeLaSalida(string output, int major, int minor)
    {
        PdfEngines.ParseMutoolVersion(output).ShouldBe(new Version(major, minor));
    }

    [Fact]
    public void VersionDeMutool_IlegibleDaNull()
    {
        PdfEngines.ParseMutoolVersion("mutool: command not found").ShouldBeNull();
    }

    [Theory]
    [InlineData("mutool version 1.19.0", false)]
    [InlineData("mutool version 1.23.10", false)]
    [InlineData("mutool version 1.24.0", true)]
    [InlineData("mutool version 2.0.1", true)]
    public void VersionMinima_ExcluyeLosMuPdfViejosDeLasDistribuciones(string output, bool supported)
    {
        (PdfEngines.ParseMutoolVersion(output) >= PdfEngines.MinimumMupdfVersion).ShouldBe(supported);
    }
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Globalization;
using Kumquat.Core.Text;

namespace Kumquat.Core.Tests.Text;

public sealed class FileSizeTextTests
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-CL");

    [Theory]
    [InlineData(0, "0 bytes")]
    [InlineData(1, "1 byte")]
    [InlineData(1023, "1023 bytes")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1,5 KB")]
    [InlineData(12_345, "12 KB")]
    [InlineData(2_516_582, "2,4 MB")]
    [InlineData(25_165_824, "24 MB")]
    [InlineData(1_288_490_189, "1,2 GB")]
    public void Formato_ComoElExplorador(long bytes, string expected) => FileSizeText.Format(bytes, Spanish).ShouldBe(expected);

    [Fact]
    public void Formato_RespetaLaCultura() => FileSizeText.Format(1536, CultureInfo.InvariantCulture).ShouldBe("1.5 KB");
}

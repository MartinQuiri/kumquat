// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using CsCheck;
using Kumquat.Core.Text;

namespace Kumquat.Core.Tests.Text;

public sealed class TextParsingTests
{
    [Theory]
    [InlineData("83", 83.0)]
    [InlineData("1:23", 83.0)]
    [InlineData("1:23.5", 83.5)]
    [InlineData("1:23,5", 83.5)]
    [InlineData("01:02:03", 3723.0)]
    [InlineData(" 0:00 ", 0.0)]
    [InlineData("90:00", 5400.0)]
    public void Tiempo_Valido(string text, double seconds)
    {
        TimeText.TryParse(text, out var value).ShouldBeTrue();
        value.TotalSeconds.ShouldBe(seconds, tolerance: 1e-9);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1:60")]
    [InlineData("1:70:00")]
    [InlineData("-5")]
    [InlineData("1::2")]
    [InlineData("1:2:3:4")]
    public void Tiempo_Invalido(string text) => TimeText.TryParse(text, out _).ShouldBeFalse();

    [Fact]
    public void Tiempo_FormatoYLecturaSonInversos()
    {
        Gen.Int[0, 50_000].Select(tenths => TimeSpan.FromMilliseconds(tenths * 100)).Sample(value =>
        {
            TimeText.TryParse(TimeText.Format(value), out var parsed).ShouldBeTrue();
            parsed.ShouldBe(value);
        });
    }

    [Theory]
    [InlineData("1-3, 5", "1-3,5")]
    [InlineData("8-fin", "8-z")]
    [InlineData("2", "2")]
    [InlineData(" 1 - 2 ; 4 ", "1-2,4")]
    public void Paginas_Validas(string text, string expected)
    {
        PageRanges.TryNormalize(text, out var normalized).ShouldBeTrue();
        normalized.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("1-")]
    [InlineData("a-b")]
    [InlineData("1,,2")]
    [InlineData("--encrypt")]
    [InlineData("1 --decrypt")]
    public void Paginas_Invalidas_NuncaLleganAlMotor(string text) => PageRanges.TryNormalize(text, out _).ShouldBeFalse();
}

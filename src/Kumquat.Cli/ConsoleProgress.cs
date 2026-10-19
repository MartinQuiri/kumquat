// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Globalization;

namespace Kumquat.Cli;

/// <summary>Progreso síncrono en stderr (Progress&lt;T&gt; publicaría en el pool, fuera de orden).</summary>
internal sealed class ConsoleProgress(string name) : IProgress<double>
{
    private int _lastPercent = -1;

    public void Report(double value)
    {
        var percent = (int)(value * 100);
        if (percent != _lastPercent)
        {
            _lastPercent = percent;
            Console.Error.Write(string.Create(CultureInfo.InvariantCulture, $"\r{name}: {percent,3} %"));
        }
    }
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

// Proceso de prueba para ProcessRunner. Modos:
//   echo <args...>      imprime cada argumento en una línea de stdout
//   stderr <n>          escribe n líneas en stderr
//   exit <code>         termina con ese código
//   sleep <ms>          duerme
//   alloc <mb>          reserva y toca esa cantidad de memoria
//   spawn-sleep <ms>    lanza un hijo que duerme y espera
using System.Diagnostics;
using System.Globalization;

Console.OutputEncoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
var mode = args.Length > 0 ? args[0] : "echo";
switch (mode)
{
    case "echo":
        foreach (var arg in args.Skip(1))
        {
            Console.Out.WriteLine(arg);
        }

        return 0;
    case "env":
        Console.Out.WriteLine(Environment.GetEnvironmentVariable(args[1]) ?? "(sin valor)");
        return 0;
    case "stderr":
        for (var i = 0; i < int.Parse(args[1], CultureInfo.InvariantCulture); i++)
        {
            Console.Error.WriteLine($"line {i}");
        }

        return 0;
    case "exit":
        return int.Parse(args[1], CultureInfo.InvariantCulture);
    case "sleep":
        Thread.Sleep(int.Parse(args[1], CultureInfo.InvariantCulture));
        return 0;
    case "alloc":
        var blocks = new List<byte[]>();
        for (var i = 0; i < int.Parse(args[1], CultureInfo.InvariantCulture); i++)
        {
            var block = new byte[1024 * 1024];
            Array.Fill(block, (byte)1);
            blocks.Add(block);
        }

        Console.Out.WriteLine(blocks.Count);
        return 0;
    case "spawn-sleep":
        var self = Environment.ProcessPath!;
        var child = new ProcessStartInfo(self) { UseShellExecute = false };
        child.ArgumentList.Add("sleep");
        child.ArgumentList.Add(args[1]);
        using (var process = Process.Start(child)!)
        {
            Console.Out.WriteLine(process.Id);
            Console.Out.Flush();
            process.WaitForExit();
        }

        return 0;
    default:
        return 64;
}

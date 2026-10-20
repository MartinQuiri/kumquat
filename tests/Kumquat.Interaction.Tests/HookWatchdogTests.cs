// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;
using Kumquat.Interaction.Input;

namespace Kumquat.Interaction.Tests;

/// <summary>Si Windows quita los hooks en silencio, Kumquat lo nota y los vuelve a poner.</summary>
public sealed partial class HookWatchdogTests
{
    [Theory]
    [InlineData(10_000u, 10_000u, 0u, 10_500u, false)] // los hooks vieron lo último
    [InlineData(12_000u, 11_000u, 0u, 12_100u, false)] // 1 s de diferencia: todavía no
    [InlineData(13_000u, 11_000u, 0u, 13_100u, true)] // Windows vio 2 s de entrada que los hooks no
    [InlineData(13_000u, 11_000u, 10_000u, 13_100u, false)] // se reinstaló hace poco
    [InlineData(5u, 4_294_960_000u, 4_294_900_000u, 100u, true)] // los ticks dieron la vuelta (49 días)
    public void Decide(uint lastSystemInput, uint lastHookEvent, uint lastReinstall, uint now, bool expected)
        => HookWatchdog.ShouldReinstall(lastSystemInput, lastHookEvent, lastReinstall, now).ShouldBe(expected);

    [Fact]
    public async Task WindowsQuitaLosHooks_SeReinstalanSolos()
    {
        Assert.SkipUnless(Environment.UserInteractive && Environment.GetEnvironmentVariable("CI") is null, "Requiere un escritorio interactivo (envía la tecla F24).");
        var reinstalled = new TaskCompletionSource();
        using var hook = new GlobalInputHook(new DragGestureTracker(TriggerKey.Shift, 4, 4), (_, _) => { })
        {
            Trace = message => reinstalled.TrySetResult(),
        };
        hook.Start();

        hook.SimulateSystemRemovalForTests();
        await Task.Delay(300, TestContext.Current.CancellationToken);
        var beforeRemoval = hook.LastHookEventForTests;
        PressF24(); // Windows la registra; los hooks (quitados) no
        await Task.Delay(300, TestContext.Current.CancellationToken);
        hook.LastHookEventForTests.ShouldBe(beforeRemoval, "sin hooks no llega nada");

        // Sigue habiendo entrada que los hooks no ven: el vigilante (cada 1 s) los reinstala.
        for (var i = 0; i < 20 && !reinstalled.Task.IsCompleted; i++)
        {
            await Task.Delay(250, TestContext.Current.CancellationToken);
            PressF24();
        }

        reinstalled.Task.IsCompleted.ShouldBeTrue("el vigilante no reinstaló los hooks");
        var afterReinstall = hook.LastHookEventForTests;
        await Task.Delay(50, TestContext.Current.CancellationToken);
        PressF24();
        await Task.Delay(300, TestContext.Current.CancellationToken);
        hook.LastHookEventForTests.ShouldNotBe(afterReinstall, "los hooks reinstalados vuelven a recibir entrada");
    }

    /// <summary>F24: una tecla que ningún teclado real tiene ni ninguna app usa.</summary>
    private static void PressF24()
    {
        const ushort VkF24 = 0x87;
        keybd_event((byte)VkF24, 0, 0, 0);
        keybd_event((byte)VkF24, 0, 2, 0);
    }

    [LibraryImport("user32.dll")]
    private static partial void keybd_event(byte vk, byte scan, uint flags, nuint extra);
}

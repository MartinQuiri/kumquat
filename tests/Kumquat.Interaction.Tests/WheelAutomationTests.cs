// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Windows.Automation;
using Kumquat.Interaction.Wheel;

namespace Kumquat.Interaction.Tests;

/// <summary>
/// La rueda vista por un lector de pantalla, con UI Automation de verdad: la ventana corre en su
/// propio hilo de UI (como en la app) y el test la consulta desde afuera, como Narrador o NVDA.
/// </summary>
[Collection(WpfWindows.Name)]
public sealed class WheelAutomationTests
{
    [Fact]
    public void Rueda_TieneNombreYTipoParaElLector()
        => WithWheel((_, element) =>
        {
            element.Current.Name.ShouldBe("Rueda de Kumquat");
            element.Current.ControlType.ShouldBe(ControlType.Menu);
            element.Current.IsKeyboardFocusable.ShouldBeFalse("la rueda nunca toma el foco");
        });

    [Fact]
    public void Anuncio_LlegaAlLector()
        => WithWheel((post, element) =>
        {
            var heard = new List<string>();
            using var received = new ManualResetEventSlim();
            AutomationEventHandler handler = (_, e) =>
            {
                if (e is NotificationEventArgs notification)
                {
                    lock (heard)
                    {
                        heard.Add(notification.DisplayString);
                    }

                    received.Set();
                }
            };
            Automation.AddAutomationEventHandler(AutomationElement.NotificationEvent, element, TreeScope.Element, handler);
            try
            {
                post(wheel => wheel.Announce("Convertir a JPG"));
                received.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue("el anuncio no llegó");
                lock (heard)
                {
                    heard.ShouldContain("Convertir a JPG");
                }
            }
            finally
            {
                Automation.RemoveAutomationEventHandler(AutomationElement.NotificationEvent, element, handler);
            }
        });

    private static void WithWheel(Action<Action<Action<WheelWindow>>, AutomationElement> test)
    {
        UiThread? ui = null;
        WheelWindow? wheel = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            using var thread = new UiThread();
            using var window = new WheelWindow();
            ui = thread;
            wheel = window;
            ready.Set();
            UiThread.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
        try
        {
            test(action => ui!.Post(() => action(wheel!)), AutomationElement.FromHandle(wheel!.Handle));
        }
        finally
        {
            ui!.Quit();
            thread.Join(TimeSpan.FromSeconds(10)).ShouldBeTrue();
        }
    }
}

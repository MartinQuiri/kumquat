// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.App;
using Kumquat.Core.Jobs;
using Kumquat.Core.Results;

namespace Kumquat.Interaction.Tests.App;

public sealed class BatchNotifierTests
{
    private readonly List<(string Title, string Message, bool IsError)> _notifications = [];

    [Fact]
    public void UnArchivo_NotificaConSuNombre()
    {
        var notifier = new BatchNotifier((t, m, e) => _notifications.Add((t, m, e)));
        var id = Guid.NewGuid();
        notifier.Track([id], BatchText.Converted("PNG"));

        notifier.OnJobChanged(Snapshot(id, JobState.Running));
        _notifications.ShouldBeEmpty();
        notifier.OnJobChanged(Snapshot(id, JobState.Succeeded, output: @"C:\x\foto.png"));

        _notifications.ShouldHaveSingleItem().ShouldBe(("Listo", "foto.png convertido a PNG", false));
    }

    [Fact]
    public void VariosArchivos_UnaSolaNotificacionAlTerminarTodos()
    {
        var notifier = new BatchNotifier((t, m, e) => _notifications.Add((t, m, e)));
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        notifier.Track(ids, BatchText.Converted("MP3"));

        notifier.OnJobChanged(Snapshot(ids[0], JobState.Succeeded, output: "a.mp3"));
        notifier.OnJobChanged(Snapshot(ids[1], JobState.Succeeded, output: "b.mp3"));
        _notifications.ShouldBeEmpty();
        notifier.OnJobChanged(Snapshot(ids[2], JobState.Succeeded, output: "c.mp3"));

        _notifications.ShouldHaveSingleItem().Message.ShouldBe("3 archivos convertidos a MP3");
    }

    [Fact]
    public void ConErrores_InformaElPrimero()
    {
        var notifier = new BatchNotifier((t, m, e) => _notifications.Add((t, m, e)));
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        notifier.Track(ids, BatchText.Converted("JPG"));

        notifier.OnJobChanged(Snapshot(ids[0], JobState.Succeeded, output: "a.jpg"));
        notifier.OnJobChanged(Snapshot(ids[1], JobState.Failed, error: new Error("engine-failed", "archivo dañado")));

        var n = _notifications.ShouldHaveSingleItem();
        n.Title.ShouldBe("1 de 2 listos");
        n.Message.ShouldBe("in.txt: archivo dañado");
        n.IsError.ShouldBeTrue();
    }

    [Fact]
    public void Herramienta_RedaccionPropia()
    {
        var notifier = new BatchNotifier((t, m, e) => _notifications.Add((t, m, e)));
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        notifier.Track(ids, BatchText.ToolDone("Comprimir"));

        notifier.OnJobChanged(Snapshot(ids[0], JobState.Succeeded, output: "a.jpg"));
        notifier.OnJobChanged(Snapshot(ids[1], JobState.Succeeded, output: "b.jpg"));

        _notifications.ShouldHaveSingleItem().Message.ShouldBe("2 archivos listos (Comprimir)");
    }

    [Fact]
    public void EnIngles_FrasesPropias()
    {
        var previous = System.Globalization.CultureInfo.CurrentUICulture;
        System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        try
        {
            var notifier = new BatchNotifier((t, m, e) => _notifications.Add((t, m, e)));
            var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
            notifier.Track([ids[0]], BatchText.Transferred(move: true, "Photos"));
            notifier.Track([ids[1]], BatchText.Converted("PNG"));

            notifier.OnJobChanged(Snapshot(ids[0], JobState.Succeeded, output: @"C:\x\a.jpg"));
            notifier.OnJobChanged(Snapshot(ids[1], JobState.Failed, error: new Error("engine-failed", "broken")));

            _notifications.ShouldBe([("Done", "a.jpg moved to Photos", false), ("Could not finish", "in.txt: broken", true)]);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void TrabajosDesconocidos_SeIgnoran()
    {
        var notifier = new BatchNotifier((t, m, e) => _notifications.Add((t, m, e)));

        notifier.OnJobChanged(Snapshot(Guid.NewGuid(), JobState.Succeeded, output: "x"));

        _notifications.ShouldBeEmpty();
    }

    private static JobSnapshot Snapshot(Guid id, JobState state, string? output = null, Error? error = null)
        => new(id, new JobRequest(@"C:\x\in.txt", "a PNG", (_, _) => Task.FromResult(Result<string>.Success("x"))), state, state == JobState.Succeeded ? 1 : 0, output, error);
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Jobs;
using Kumquat.Core.Results;
using Microsoft.Extensions.Time.Testing;

namespace Kumquat.Core.Tests.Jobs;

public sealed class ProgressBoardTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly ProgressBoard _board;

    public ProgressBoardTests() => _board = new ProgressBoard(_time);

    [Fact]
    public void TrabajoInstantaneo_NoMuestraElPanel()
    {
        var id = Add("foto.png", "a JPG");
        Set(id, JobState.Running, 0.5);
        _time.Advance(TimeSpan.FromMilliseconds(200));
        Set(id, JobState.Succeeded, 1);

        _board.View().Visible.ShouldBeFalse();
    }

    [Fact]
    public void TrabajoLargo_ApareceYSeVaPocoDespuesDeTerminar()
    {
        var id = Add(Path.Combine(Path.GetTempPath(), "videos", "viaje.mp4"), "Comprimir");
        Set(id, JobState.Running, 0);
        _time.Advance(ProgressBoard.ShowDelay);

        var view = _board.View();
        view.Visible.ShouldBeTrue();
        view.Rows.ShouldHaveSingleItem().FileName.ShouldBe("viaje.mp4");
        view.Rows[0].Description.ShouldBe("Comprimir");
        view.Rows[0].Progress.ShouldBeNull("todavía sin avance");

        Set(id, JobState.Succeeded, 1);
        _time.Advance(TimeSpan.FromSeconds(1));
        _board.View().Visible.ShouldBeTrue("queda un momento a la vista");
        _time.Advance(ProgressBoard.HideDelay);
        _board.View().Visible.ShouldBeFalse();
    }

    [Fact]
    public void TiempoRestante_SaleDelRitmoDeAvance()
    {
        var id = Add("video.mkv", "a MP4");
        Set(id, JobState.Running, 0);
        _time.Advance(TimeSpan.FromSeconds(1));
        Set(id, JobState.Running, 0.10);
        _board.View().Rows[0].Remaining.ShouldBeNull("con un solo dato no hay ritmo");

        // 10 % por segundo: del 20 % al 100 % faltan 8 s.
        _time.Advance(TimeSpan.FromSeconds(1));
        Set(id, JobState.Running, 0.20);
        var row = _board.View().Rows[0];
        row.Progress.ShouldBe(0.20);
        row.Remaining!.Value.TotalSeconds.ShouldBe(8, tolerance: 0.01);

        // Sin avisos nuevos, la cuenta sigue bajando.
        _time.Advance(TimeSpan.FromSeconds(3));
        _board.View().Rows[0].Remaining!.Value.TotalSeconds.ShouldBe(5, tolerance: 0.01);
    }

    [Fact]
    public void TiempoRestante_SuavizaLosSaltos()
    {
        var id = Add("audio.wav", "a MP3");
        Set(id, JobState.Running, 0.05);
        _time.Advance(TimeSpan.FromSeconds(2));
        Set(id, JobState.Running, 0.25); // 10 %/s
        var before = _board.View().Rows[0].Remaining!.Value;

        _time.Advance(TimeSpan.FromSeconds(0.1));
        Set(id, JobState.Running, 0.45); // un salto brusco: 20 % en una décima

        var after = _board.View().Rows[0].Remaining!.Value;
        after.ShouldBeLessThan(before);
        after.TotalSeconds.ShouldBeGreaterThan(1, "un salto no lleva la estimación a casi cero");
    }

    [Fact]
    public void Muchos_EnCursoPrimeroYElRestoResumido()
    {
        var ids = Enumerable.Range(1, 7).Select(i => Add($"f{i}.png", "a WebP")).ToList();
        Set(ids[5], JobState.Running, 0.5);
        Set(ids[6], JobState.Running, 0.1);
        _time.Advance(ProgressBoard.ShowDelay);

        var view = _board.View();
        view.Running.ShouldBe(2);
        view.Queued.ShouldBe(5);
        view.Rows.Count.ShouldBe(ProgressBoard.MaxRows);
        view.Rows.Take(2).ShouldAllBe(r => r.IsRunning);
        view.Rows[2].FileName.ShouldBe("f1.png");
        view.Hidden.ShouldBe(3);
    }

    [Fact]
    public void Ocultar_HastaQueTermineLoActual_ElProximoLoteVuelveAMostrarlo()
    {
        var a = Add("a.mp4", "Comprimir");
        Set(a, JobState.Running, 0.1);
        _time.Advance(ProgressBoard.ShowDelay);
        _board.Dismiss();
        _board.View().Visible.ShouldBeFalse();

        Set(a, JobState.Succeeded, 1);
        var b = Add("b.mp4", "Comprimir");
        Set(b, JobState.Running, 0.1);
        _time.Advance(ProgressBoard.ShowDelay);

        _board.View().Visible.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null, "calculando…")]
    [InlineData(3.0, "casi listo")]
    [InlineData(11.2, "quedan ~12 s")]
    [InlineData(59.5, "quedan ~60 s")]
    [InlineData(61.0, "quedan ~2 min")]
    [InlineData(3725.0, "quedan ~1 h 3 min")]
    public void TextoDelTiempo_RedondeaHaciaArriba(double? seconds, string expected)
        => Kumquat.Core.Text.RemainingText.Format(seconds is { } s ? TimeSpan.FromSeconds(s) : null).ShouldBe(expected);

    [Fact]
    public void FalloOCancelacion_SacanLaFila()
    {
        var a = Add("a.pdf", "Comprimir");
        var b = Add("b.pdf", "Comprimir");
        Set(a, JobState.Running, 0.3);
        Set(b, JobState.Running, 0.3);
        _time.Advance(ProgressBoard.ShowDelay);

        Set(a, JobState.Failed, 0);
        Set(b, JobState.Cancelled, 0);

        _board.View().Rows.ShouldBeEmpty();
    }

    private Guid Add(string path, string description)
    {
        var id = Guid.NewGuid();
        _board.OnJobChanged(new JobSnapshot(id, Request(path, description), JobState.Queued, 0, null, null));
        return id;
    }

    private void Set(Guid id, JobState state, double progress)
        => _board.OnJobChanged(new JobSnapshot(id, Request("x", "x"), state, progress, null, null));

    private static JobRequest Request(string path, string description)
        => new(path, description, (_, _) => Task.FromResult(Result<string>.Success("x")));
}

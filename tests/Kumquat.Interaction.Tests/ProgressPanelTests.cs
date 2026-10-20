// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Jobs;
using Kumquat.Dialogs.Progress;

namespace Kumquat.Interaction.Tests;

[Collection(WpfWindows.Name)]
public sealed class ProgressPanelTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();

    [Fact]
    public void Filas_PorcentajeYTiempo_ColaYResumen()
    {
        var (header, rows, more) = OnSta(() =>
        {
            var panel = ProgressPanel.CreateForTests();
            panel.Update(new ProgressView(true,
            [
                new(A, "vacaciones.mp4", "Comprimir", true, 0.42, TimeSpan.FromSeconds(11.3)),
                new(B, "entrevista.wav", "a MP3", true, null, null),
                new(C, "informe.docx", "a PDF", false, null, null),
            ], Running: 2, Queued: 3, Hidden: 2));
            return (panel.HeaderForTests, panel.RowTextsForTests, panel.MoreForTests);
        });

        header.ShouldBe("2 en curso · 3 en cola");
        rows.ShouldBe(["vacaciones.mp4 | Comprimir · quedan ~12 s", "entrevista.wav | a MP3 · calculando…", "informe.docx | a PDF · En cola"]);
        more.ShouldBe("y 2 más en la cola");
    }

    [Fact]
    public void Actualizar_QuitaLoTerminadoYReordena()
    {
        var rows = OnSta(() =>
        {
            var panel = ProgressPanel.CreateForTests();
            panel.Update(new ProgressView(true, [new(A, "a.png", "a JPG", true, 0.5, null), new(B, "b.png", "a JPG", false, null, null)], 1, 1, 0));
            panel.Update(new ProgressView(true, [new(B, "b.png", "a JPG", true, 0.1, null)], 1, 0, 0));
            return panel.RowTextsForTests;
        });

        rows.ShouldBe(["b.png | a JPG · calculando…"]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void VistaPrevia(bool dark)
    {
        var path = Path.Combine(RepoImages(), dark ? "progreso-oscuro.png" : "progreso-claro.png");
        OnSta(() =>
        {
            var panel = ProgressPanel.CreateForTests(dark);
            panel.Update(new ProgressView(true,
            [
                new(A, "vacaciones en la playa.mp4", "Comprimir", true, 0.64, TimeSpan.FromSeconds(38)),
                new(B, "fotos del viaje.heic", "a JPG", true, 0.18, TimeSpan.FromMinutes(2.2)),
                new(C, "informe anual.docx", "a PDF", false, null, null),
            ], Running: 2, Queued: 2, Hidden: 1));
            ProgressPanel.RenderPreview(panel, path, dark);
            return 0;
        });
        new FileInfo(path).Length.ShouldBeGreaterThan(1000);
    }

    private static T OnSta<T>(Func<T> action)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
#pragma warning disable CA1031 // Se relanza en el hilo del test.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return failure is null ? result : throw new InvalidOperationException("Falló en el hilo STA.", failure);
    }

    private static string RepoImages()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Kumquat.slnx")))
        {
            dir = dir.Parent;
        }

        return Directory.CreateDirectory(Path.Combine(dir?.FullName ?? Path.GetTempPath(), "docs", "images")).FullName;
    }
}

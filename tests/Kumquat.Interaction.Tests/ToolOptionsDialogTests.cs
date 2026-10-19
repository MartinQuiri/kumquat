// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Tools;
using Kumquat.Dialogs;

namespace Kumquat.Interaction.Tests;

public sealed class ToolOptionsDialogTests
{
    [Fact]
    public void SinCambios_SoloEnviaTiemposYContraseñas_NoLosTextos()
    {
        var values = OnStaThread(() => ToolOptionsDialog.CreateForTests(
        [
            new ToolParameter("author", "Autor", ToolParameterKind.Text, "Ana"),
            new ToolParameter("start", "Desde", ToolParameterKind.Time, "0:00"),
            new ToolParameter("password", "Contraseña", ToolParameterKind.Password, "x"),
            new ToolParameter("custom", "Campos personalizados", ToolParameterKind.CustomFields),
        ]).Collect());

        // Un texto no modificado no se envía: al editar metadatos de varios archivos no se pisan sus valores.
        values.ShouldNotContainKey("author");
        values["start"].ShouldBe("0:00");
        values["password"].ShouldBe("x");
        values.Keys.ShouldNotContain(k => k.StartsWith(ToolParameter.CustomPrefix, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void VistaPrevia_Metadatos(bool dark)
    {
        var path = Path.Combine(RepoImages(), dark ? "opciones-oscuro.png" : "opciones-claro.png");
        OnStaThread(() =>
        {
            ToolOptionsDialog.RenderPreview("Metadatos", "informe.pdf",
            [
                new ToolParameter("Title", "Título", ToolParameterKind.Text, "Informe anual"),
                new ToolParameter("Author", "Autor", ToolParameterKind.Text, "María Pérez"),
                new ToolParameter("Subject", "Asunto", ToolParameterKind.Text, string.Empty),
                new ToolParameter("Keywords", "Palabras clave", ToolParameterKind.Text, "ventas, 2026"),
                new ToolParameter("custom:Proyecto", "Proyecto", ToolParameterKind.Text, "Kumquat"),
                new ToolParameter("custom", "Campos personalizados", ToolParameterKind.CustomFields),
            ], path, dark);
            return 0;
        });

        new FileInfo(path).Length.ShouldBeGreaterThan(1000);
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

    private static T OnStaThread<T>(Func<T> action)
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
}

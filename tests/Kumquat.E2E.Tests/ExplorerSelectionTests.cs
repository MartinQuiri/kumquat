// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.E2E.Tests;

/// <summary>
/// Abre el Explorador con un archivo seleccionado y comprueba que Kumquat lee esa selección
/// (lo que usan el atajo Ctrl+Alt+K y el menú contextual). Requiere escritorio interactivo.
/// </summary>
[Trait("Category", "E2E")]
public sealed class ExplorerSelectionTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("kumquat-sel-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // El Explorador puede tener la carpeta abierta un momento más.
        }
    }

    [Fact]
    public async Task ArchivoSeleccionado_EnElExploradorAlFrente_SeLee()
    {
        Assert.SkipUnless(Environment.UserInteractive && Environment.GetEnvironmentVariable("CI") is null, "Requiere un escritorio interactivo.");
        var file = Path.Combine(_dir, "elegido ñ.txt");
        await File.WriteAllTextAsync(file, "Kumquat", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_dir, "otro.txt"), "no", TestContext.Current.CancellationToken);

        using var explorer = await ExplorerWindow.OpenSelectingAsync(file, TestContext.Current.CancellationToken);
        IReadOnlyList<string> selection = [];
        for (var i = 0; i < 20 && selection.Count == 0; i++)
        {
            explorer.Activate(); // otro test pudo dejar el foco en otra ventana
            selection = await OnStaAsync(Kumquat.Interaction.Shell.ExplorerSelection.Get);
            await Task.Delay(250, TestContext.Current.CancellationToken);
        }

        selection.ShouldBe([file]);
    }

    private static Task<T> OnStaAsync<T>(Func<T> action)
    {
        var completion = new TaskCompletionSource<T>();
        var thread = new Thread(() =>
        {
            try
            {
                completion.SetResult(action());
            }
#pragma warning disable CA1031 // Se entrega al test.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                completion.SetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}

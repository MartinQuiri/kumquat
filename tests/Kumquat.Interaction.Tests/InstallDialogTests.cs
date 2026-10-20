// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.App;
using Kumquat.Dialogs.Install;

namespace Kumquat.Interaction.Tests;

[Collection(WpfWindows.Name)]
public sealed class InstallDialogTests
{
    [Fact]
    public void DiceQueEsYParaQue()
    {
        var offer = EngineOffers.LibreOffice;
        var body = OnSta(() => InstallDialog.CreateForTests(new InstallRequest(offer.Name, offer.Purpose, offer.Publisher, offer.License)).Body);

        body.ShouldBe("Para esto hace falta LibreOffice, un programa gratuito que Kumquat usa para convertir documentos de Word, Excel y PowerPoint a PDF, y PDF a Word. No está en este equipo.");
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
}

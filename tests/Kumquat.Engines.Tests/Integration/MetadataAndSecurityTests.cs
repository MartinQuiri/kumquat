// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Formats;
using Kumquat.Core.Tools;
using Kumquat.Engines.Pdf;
using Kumquat.Engines.Vips;
using NetVips;

namespace Kumquat.Engines.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class MetadataAndSecurityTests : IDisposable
{
    private static readonly ToolService Service = new([.. VipsTool.CreateAll(), .. PdfTool.CreateAll(TestEngines.Pdf, TestEngines.ImagePdf, TestEngines.Workspaces)]);

    private readonly string _dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "kumquat-meta", Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task MetadatosJpeg_NoRecomprimenLaImagen()
    {
        var photo = PhotoWithOrientation(6);

        var result = await Service.ApplyAsync([photo], "image.metadata", Options(("author", "María Pérez"), ("title", "Ñandú")), null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
        ImageData(result.Value!).ShouldBe(ImageData(photo), "Los datos de imagen (desde SOS) deben quedar idénticos byte a byte.");
        using var image = Image.NewFromFile(result.Value!);
        ((int)image.Get("orientation")).ShouldBe(6, "La orientación EXIF debe conservarse.");
        image.Contains("exif-ifd0-Artist").ShouldBeFalse("Con tildes no se escribe Artist (ASCII): el Explorador lo priorizaría y mostraría 'Maria'.");
        ImageMetadataTool.ReadForTests(result.Value!)["author"].ShouldBe("María Pérez");
    }

    [Fact]
    public async Task MetadatosJpeg_AutorAscii_TambienVaAExif()
    {
        var photo = PhotoWithOrientation(1);

        var result = await Service.ApplyAsync([photo], "image.metadata", Options(("author", "Ana Soto")), null, TestContext.Current.CancellationToken);

        using var image = Image.NewFromFile(result.Value!);
        ((string)image.Get("exif-ifd0-Artist")).ShouldStartWith("Ana Soto");
    }

    [Fact]
    public async Task MetadatosJpeg_VaciarUnCampo_LoBorra()
    {
        var photo = PhotoWithOrientation(1);
        var first = await Service.ApplyAsync([photo], "image.metadata", Options(("author", "Ana Soto"), ("copyright", "CC0")), null, TestContext.Current.CancellationToken);

        var second = await Service.ApplyAsync([first.Value!], "image.metadata", Options(("author", string.Empty)), null, TestContext.Current.CancellationToken);

        var values = ImageMetadataTool.ReadForTests(second.Value!);
        values.ContainsKey("author").ShouldBeFalse();
        values["copyright"].ShouldBe("CC0", "Los campos no tocados se conservan.");
    }

    [Fact]
    public async Task ProtegerYDesbloquear_ConLaContraseñaCorrecta()
    {
        var pdf = await SamplePdfAsync();

        var locked = await Service.ApplyAsync([pdf], "pdf.protect", Options(("password", "clave ñ"), ("confirm", "clave ñ")), null, TestContext.Current.CancellationToken);
        var wrong = await Service.ApplyAsync([locked.Value!], "pdf.unlock", Options(("password", "otra")), null, TestContext.Current.CancellationToken);
        var unlocked = await Service.ApplyAsync([locked.Value!], "pdf.unlock", Options(("password", "clave ñ")), null, TestContext.Current.CancellationToken);

        locked.IsSuccess.ShouldBeTrue(locked.Error?.ToString());
        wrong.Error.ShouldNotBeNull().Code.ShouldBe("wrong-password");
        unlocked.IsSuccess.ShouldBeTrue(unlocked.Error?.ToString());
        (await TestEngines.Pdf.RunQpdfAsync(["--is-encrypted", unlocked.Value!], TestContext.Current.CancellationToken)).IsSuccess.ShouldBeFalse("Debe quedar sin cifrar.");
    }

    [Theory]
    [InlineData("pdf.protect", "a", "b", "no coinciden")]
    [InlineData("pdf.protect", "", "", "Escribe una contraseña")]
    public void Proteger_ValidaLaContraseña(string tool, string password, string confirm, string message)
    {
        Service.Validate(tool, Options(("password", password), ("confirm", confirm))).ShouldNotBeNull().ShouldContain(message);
    }

    [Fact]
    public async Task MetadatosPdf_ProtegidoSinDesbloquear_DaErrorClaro()
    {
        var pdf = await SamplePdfAsync();
        var locked = await Service.ApplyAsync([pdf], "pdf.protect", Options(("password", "x"), ("confirm", "x")), null, TestContext.Current.CancellationToken);

        var result = await Service.ApplyAsync([locked.Value!], "pdf.metadata", Options(("Author", "Yo")), null, TestContext.Current.CancellationToken);

        result.Error.ShouldNotBeNull().Code.ShouldBe("pdf-encrypted");
    }

    [Fact]
    public async Task ConContraseña_LaPideSoloSiElPdfLaNecesita()
    {
        var pdf = await SamplePdfAsync();
        var locked = await LockAsync(pdf, "clave ñ");

        var plain = await Service.GetParametersAsync([pdf], "pdf.compress", TestContext.Current.CancellationToken);
        var asked = await Service.GetParametersAsync([locked], "pdf.compress", TestContext.Current.CancellationToken);
        var extract = await Service.GetParametersAsync([locked], "pdf.extract", TestContext.Current.CancellationToken);

        plain.ShouldBeEmpty("un PDF sin contraseña se comprime sin preguntar nada");
        var password = asked.ShouldHaveSingleItem();
        password.Key.ShouldBe("pdf-password");
        password.Kind.ShouldBe(ToolParameterKind.Password);
        password.Required.ShouldBeTrue();
        // La contraseña primero y después lo propio de la herramienta.
        extract.Select(p => p.Key).ShouldBe(["pdf-password", "pages"]);
    }

    [Theory]
    [InlineData("pdf.compress")]
    [InlineData("pdf.rotate")]
    public async Task ConContraseña_ElResultadoQuedaProtegidoConLaMisma(string tool)
    {
        var locked = await LockAsync(await SamplePdfAsync(), "clave ñ");

        var result = await Service.ApplyAsync([locked], tool, Options(("pdf-password", "clave ñ")), null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
        (await TestEngines.Pdf.RequiresPasswordAsync(result.Value!, TestContext.Current.CancellationToken)).ShouldBeTrue("el resultado sigue protegido");
        (await Service.ApplyAsync([result.Value!], "pdf.unlock", Options(("password", "otra")), null, TestContext.Current.CancellationToken)).Error.ShouldNotBeNull().Code.ShouldBe("wrong-password");
        (await Service.ApplyAsync([result.Value!], "pdf.unlock", Options(("password", "clave ñ")), null, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue("se abre con la misma contraseña");
        (await TestEngines.Pdf.RequiresPasswordAsync(locked, TestContext.Current.CancellationToken)).ShouldBeTrue("el original no se toca");
    }

    [Fact]
    public async Task ConContraseña_ExtraerPaginas_Funciona()
    {
        var locked = await LockAsync(await SamplePdfAsync(), "x");

        var result = await Service.ApplyAsync([locked], "pdf.extract", Options(("pdf-password", "x"), ("pages", "1")), null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
        (await TestEngines.Pdf.RequiresPasswordAsync(result.Value!, TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    [Fact]
    public async Task ConContraseña_Dividir_CadaPaginaQuedaProtegida()
    {
        var locked = await LockAsync(await SamplePdfAsync(), "x");

        var result = await Service.ApplyAsync([locked], "pdf.split", Options(("pdf-password", "x")), null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
        var pages = Directory.GetFiles(result.Value!, "*.pdf");
        pages.ShouldNotBeEmpty();
        foreach (var page in pages)
        {
            // Las páginas se llaman como el original, no como la copia temporal.
            Path.GetFileName(page).ShouldStartWith(Path.GetFileNameWithoutExtension(locked), Case.Sensitive);
            (await TestEngines.Pdf.RequiresPasswordAsync(page, TestContext.Current.CancellationToken)).ShouldBeTrue();
        }
    }

    [Fact]
    public async Task ConContraseña_PaginasAJpg_SacaLasImagenes()
    {
        Assert.SkipUnless(VipsConverter.IsLibraryAvailable, "libvips no está disponible.");
        var locked = await LockAsync(await SamplePdfAsync(), "x");

        var result = await Service.ApplyAsync([locked], "pdf.to-jpg", Options(("pdf-password", "x")), null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
        Directory.GetFiles(result.Value!, "*.jpg").ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ConContraseña_Proteger_LaCambia()
    {
        var locked = await LockAsync(await SamplePdfAsync(), "vieja");

        var result = await Service.ApplyAsync([locked], "pdf.protect", Options(("pdf-password", "vieja"), ("password", "nueva"), ("confirm", "nueva")), null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
        (await Service.ApplyAsync([result.Value!], "pdf.unlock", Options(("password", "vieja")), null, TestContext.Current.CancellationToken)).Error.ShouldNotBeNull().Code.ShouldBe("wrong-password");
        (await Service.ApplyAsync([result.Value!], "pdf.unlock", Options(("password", "nueva")), null, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ConContraseñaIncorrecta_ErrorClaroYNoDejaNada()
    {
        var locked = await LockAsync(await SamplePdfAsync(), "clave");
        var before = Directory.GetFileSystemEntries(_dir).Order().ToArray();

        var result = await Service.ApplyAsync([locked], "pdf.compress", Options(("pdf-password", "otra")), null, TestContext.Current.CancellationToken);

        result.Error.ShouldNotBeNull().Code.ShouldBe("wrong-password");
        // Ni resultado a medias ni copia abierta junto al original.
        Directory.GetFileSystemEntries(_dir).Order().ShouldBe(before);
    }

    [Fact]
    public async Task ConContraseña_SinEscribirla_PideDesbloquear()
    {
        var locked = await LockAsync(await SamplePdfAsync(), "clave");

        var result = await Service.ApplyAsync([locked], "pdf.compress", null, null, TestContext.Current.CancellationToken);

        result.Error.ShouldNotBeNull().Code.ShouldBe("pdf-encrypted");
    }

    [Fact]
    public void ContraseñaDelPdf_ConSaltoDeLinea_SeRechaza()
        => Service.Validate("pdf.compress", Options(("pdf-password", "a\nb"))).ShouldNotBeNull();

    private static async Task<string> LockAsync(string pdf, string password)
    {
        var locked = await Service.ApplyAsync([pdf], "pdf.protect", Options(("password", password), ("confirm", password)), null, TestContext.Current.CancellationToken);
        locked.IsSuccess.ShouldBeTrue(locked.Error?.ToString());
        (await TestEngines.Pdf.RequiresPasswordAsync(locked.Value!, TestContext.Current.CancellationToken)).ShouldBeTrue();
        return locked.Value!;
    }

    private static Dictionary<string, string> Options(params (string Key, string Value)[] pairs)
        => pairs.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);

    /// <summary>Bytes desde el marcador SOS (FFDA): los datos de imagen comprimidos.</summary>
    private static byte[] ImageData(string jpeg)
    {
        var bytes = File.ReadAllBytes(jpeg);
        var sos = bytes.AsSpan().IndexOf([(byte)0xFF, (byte)0xDA]);
        sos.ShouldBeGreaterThan(0);
        return bytes[sos..];
    }

    private string PhotoWithOrientation(int orientation)
    {
        var path = Path.Combine(_dir, $"foto-{Guid.NewGuid():N}.jpg");
        using var xyz = Image.Xyz(800, 600);
        using var bands = xyz[0].Bandjoin(xyz[1], xyz[0]);
        using var rgb = bands.Cast(Enums.BandFormat.Uchar).Copy(interpretation: Enums.Interpretation.Srgb);
        using var tagged = rgb.Mutate(m => m.Set(GValue.GIntType, "orientation", orientation));
        tagged.Jpegsave(path, q: 85);
        return path;
    }

    private async Task<string> SamplePdfAsync()
    {
        var sample = (await Corpus.GetAsync(KnownFormats.Pdf)).ShouldNotBeNull();
        var path = Path.Combine(_dir, $"doc-{Guid.NewGuid():N}.pdf");
        File.Copy(sample, path);
        return path;
    }
}

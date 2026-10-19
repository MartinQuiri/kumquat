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

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Text;

namespace Kumquat.Engines.Vips;

/// <summary>
/// Reemplaza los bloques EXIF y XMP (segmentos APP1) de un JPEG sin tocar los datos de imagen:
/// editar metadatos no debe recomprimir la foto (cada recompresión pierde calidad).
/// </summary>
internal static class JpegSegments
{
    private const byte Marker = 0xFF;
    private const byte Soi = 0xD8;
    private const byte Sos = 0xDA;
    private const byte App0 = 0xE0;
    private const byte App1 = 0xE1;

    /// <summary>Cabecera del payload APP1 EXIF.</summary>
    public static ReadOnlySpan<byte> ExifHeader => "Exif\0\0"u8;

    /// <summary>Cabecera del payload APP1 XMP.</summary>
    public static ReadOnlySpan<byte> XmpHeader => "http://ns.adobe.com/xap/1.0/\0"u8;

    /// <summary>
    /// Devuelve el JPEG con los bloques EXIF/XMP reemplazados (o quitados si son <c>null</c>).
    /// Los nuevos se insertan tras SOI y APP0 (JFIF), como manda el estándar.
    /// </summary>
    /// <exception cref="InvalidDataException">Si no es un JPEG válido o un bloque supera 64 KB.</exception>
    public static byte[] Replace(ReadOnlySpan<byte> jpeg, byte[]? exifPayload, byte[]? xmpPayload)
    {
        if (jpeg.Length < 4 || jpeg[0] != Marker || jpeg[1] != Soi)
        {
            throw new InvalidDataException("No es un JPEG válido.");
        }

        using var output = new MemoryStream(jpeg.Length + 4096);
        output.Write([Marker, Soi]);
        var inserted = false;
        var pos = 2;
        while (pos < jpeg.Length)
        {
            if (jpeg[pos] != Marker)
            {
                throw new InvalidDataException("Estructura JPEG inesperada.");
            }

            var marker = jpeg[pos + 1];
            if (marker == Marker)
            {
                pos++; // Relleno 0xFF entre segmentos.
                continue;
            }

            if (marker == Sos)
            {
                if (!inserted)
                {
                    WriteNewSegments(output, exifPayload, xmpPayload);
                }

                output.Write(jpeg[pos..]); // Datos de imagen comprimidos: se copian intactos.
                return output.ToArray();
            }

            var length = (jpeg[pos + 2] << 8) | jpeg[pos + 3];
            if (length < 2 || pos + 2 + length > jpeg.Length)
            {
                throw new InvalidDataException("Segmento JPEG truncado.");
            }

            var segment = jpeg.Slice(pos, 2 + length);
            var payload = segment[4..];
            var isOldMetadata = marker == App1 && (payload.StartsWith(ExifHeader) || payload.StartsWith(XmpHeader));
            if (!inserted && marker != App0)
            {
                WriteNewSegments(output, exifPayload, xmpPayload);
                inserted = true;
            }

            if (!isOldMetadata)
            {
                output.Write(segment);
            }

            pos += 2 + length;
        }

        throw new InvalidDataException("El JPEG no tiene datos de imagen.");
    }

    /// <summary>Payload (con cabecera "Exif\0\0") del primer APP1 EXIF, o <c>null</c>.</summary>
    public static byte[]? FindExif(ReadOnlySpan<byte> jpeg)
    {
        var pos = 2;
        while (pos + 4 <= jpeg.Length && jpeg[pos] == Marker)
        {
            var marker = jpeg[pos + 1];
            if (marker == Sos)
            {
                break;
            }

            var length = (jpeg[pos + 2] << 8) | jpeg[pos + 3];
            if (length < 2 || pos + 2 + length > jpeg.Length)
            {
                break;
            }

            var payload = jpeg.Slice(pos + 4, length - 2);
            if (marker == App1 && payload.StartsWith(ExifHeader))
            {
                return payload.ToArray();
            }

            pos += 2 + length;
        }

        return null;
    }

    /// <summary>Payload APP1 XMP para un paquete XMP en texto.</summary>
    public static byte[] XmpPayload(string xmp) => [.. XmpHeader, .. Encoding.UTF8.GetBytes(xmp)];

    private static void WriteNewSegments(MemoryStream output, byte[]? exifPayload, byte[]? xmpPayload)
    {
        foreach (var payload in new[] { exifPayload, xmpPayload })
        {
            if (payload is null)
            {
                continue;
            }

            if (payload.Length + 2 > ushort.MaxValue)
            {
                throw new InvalidDataException("Los metadatos superan los 64 KB que admite un bloque JPEG.");
            }

            var length = payload.Length + 2;
            output.Write([Marker, App1, (byte)(length >> 8), (byte)(length & 0xFF)]);
            output.Write(payload);
        }
    }
}

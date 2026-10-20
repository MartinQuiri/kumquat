// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.App;

/// <summary>
/// Otras palabras con las que se encuentra cada herramienta o acción en el buscador: las que usa
/// una persona aunque no sean el nombre exacto ("achicar" → Comprimir, "cortar" → Recortar).
/// </summary>
internal static class CommandKeywords
{
    private static readonly Dictionary<string, string[]> Words = new(StringComparer.Ordinal)
    {
        ["image.compress"] = ["reducir", "achicar", "peso", "optimizar"],
        ["pdf.compress"] = ["reducir", "achicar", "peso", "optimizar"],
        ["video.compress"] = ["reducir", "achicar", "peso", "optimizar"],
        ["image.rotate"] = ["girar", "voltear"],
        ["pdf.rotate"] = ["girar"],
        ["video.rotate"] = ["girar"],
        ["image.flip"] = ["espejo", "reflejar", "invertir"],
        ["image.fit"] = ["redimensionar", "tamaño", "escalar", "achicar"],
        ["image.gray"] = ["grises", "monocromo", "blanco y negro"],
        ["image.metadata"] = ["autor", "titulo", "exif", "propiedades"],
        ["pdf.metadata"] = ["autor", "titulo", "propiedades"],
        ["media.metadata"] = ["autor", "artista", "titulo", "etiquetas", "tags"],
        ["office.metadata"] = ["autor", "titulo", "propiedades"],
        ["image.strip"] = ["privacidad", "exif", "gps", "limpiar"],
        ["image.to-pdf"] = ["pdf", "juntar"],
        ["pdf.merge"] = ["juntar", "combinar", "unir"],
        ["pdf.split"] = ["separar", "partir", "dividir"],
        ["pdf.to-jpg"] = ["imagenes", "fotos", "jpg"],
        ["pdf.extract"] = ["sacar", "paginas", "seleccionar"],
        ["pdf.protect"] = ["contraseña", "clave", "cifrar", "bloquear"],
        ["pdf.unlock"] = ["contraseña", "quitar clave", "descifrar"],
        ["pdf.ocr"] = ["ocr", "escaneo", "escaneado", "buscable", "texto"],
        ["image.ocr-text"] = ["ocr", "leer", "copiar texto", "escaneo"],
        ["image.ocr-pdf"] = ["ocr", "escaneo", "buscable"],
        ["video.mute"] = ["silenciar", "sin sonido", "audio"],
        ["audio.normalize"] = ["volumen", "nivelar", "loudness"],
        ["video.frame"] = ["captura", "foto", "imagen", "screenshot"],
        ["video.merge"] = ["juntar", "combinar", "unir"],
        ["audio.merge"] = ["juntar", "combinar", "unir"],
        ["media.trim"] = ["cortar", "tramo", "fragmento", "clip"],
        ["archive.zip"] = ["comprimir", "empaquetar"],
        ["archive.extract"] = ["descomprimir", "unzip", "abrir"],
        ["action.save"] = ["guardar", "descargar", "extraer"],
        ["action.copy-path"] = ["ruta", "path", "direccion"],
        ["action.copy-to"] = ["duplicar", "copia"],
        ["action.move-to"] = ["trasladar", "mover"],
        ["action.show-in-folder"] = ["explorador", "carpeta", "abrir"],
        ["action.terminal"] = ["consola", "cmd", "powershell", "shell"],
        ["action.install.libreoffice"] = ["instalar", "office", "word", "excel", "powerpoint", "descargar"],
    };

    /// <summary>Palabras extra de un id (vacío si no tiene).</summary>
    public static IReadOnlyList<string> For(string id) => Words.GetValueOrDefault(id) ?? [];
}

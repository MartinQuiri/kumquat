// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.App;

/// <summary>
/// Otras palabras con las que se encuentra cada herramienta o acción en el buscador: las que usa
/// una persona aunque no sean el nombre exacto ("achicar" → Comprimir, "cortar" → Recortar). Van
/// en español y en inglés a la vez: se encuentra igual escribiendo en cualquiera de los dos.
/// </summary>
internal static class CommandKeywords
{
    private static readonly string[] Shrink = ["reducir", "achicar", "peso", "optimizar", "compress", "reduce", "shrink", "optimize", "size"];
    private static readonly string[] Join = ["juntar", "combinar", "unir", "merge", "join", "combine"];

    private static readonly Dictionary<string, string[]> Words = new(StringComparer.Ordinal)
    {
        ["image.compress"] = Shrink,
        ["pdf.compress"] = Shrink,
        ["video.compress"] = Shrink,
        ["image.rotate"] = ["girar", "voltear", "rotate", "turn"],
        ["pdf.rotate"] = ["girar", "rotate", "turn"],
        ["video.rotate"] = ["girar", "rotate", "turn"],
        ["image.flip"] = ["espejo", "reflejar", "invertir", "flip", "mirror"],
        ["image.fit"] = ["redimensionar", "tamaño", "escalar", "achicar", "resize", "scale", "smaller"],
        ["image.gray"] = ["grises", "monocromo", "blanco y negro", "grayscale", "greyscale", "black and white", "monochrome"],
        ["image.metadata"] = ["autor", "titulo", "exif", "propiedades", "author", "title", "properties"],
        ["pdf.metadata"] = ["autor", "titulo", "propiedades", "author", "title", "properties"],
        ["media.metadata"] = ["autor", "artista", "titulo", "etiquetas", "tags", "author", "artist", "title"],
        ["office.metadata"] = ["autor", "titulo", "propiedades", "author", "title", "properties"],
        ["image.strip"] = ["privacidad", "exif", "gps", "limpiar", "privacy", "clean", "strip"],
        ["image.to-pdf"] = ["pdf", "juntar", "combine", "join"],
        ["pdf.merge"] = Join,
        ["pdf.split"] = ["separar", "partir", "dividir", "split", "separate"],
        ["pdf.to-jpg"] = ["imagenes", "fotos", "jpg", "images", "pictures"],
        ["pdf.extract"] = ["sacar", "paginas", "seleccionar", "extract", "pages", "select"],
        ["pdf.protect"] = ["contraseña", "clave", "cifrar", "bloquear", "password", "encrypt", "lock"],
        ["pdf.unlock"] = ["contraseña", "quitar clave", "descifrar", "password", "decrypt", "unlock"],
        ["pdf.ocr"] = ["ocr", "escaneo", "escaneado", "buscable", "texto", "scan", "searchable", "text"],
        ["image.ocr-text"] = ["ocr", "leer", "copiar texto", "escaneo", "read", "copy text", "scan"],
        ["image.ocr-pdf"] = ["ocr", "escaneo", "buscable", "scan", "searchable"],
        ["video.mute"] = ["silenciar", "sin sonido", "audio", "mute", "silent", "sound"],
        ["audio.normalize"] = ["volumen", "nivelar", "loudness", "volume", "level"],
        ["video.frame"] = ["captura", "foto", "imagen", "screenshot", "still", "picture", "image"],
        ["video.merge"] = Join,
        ["audio.merge"] = Join,
        ["media.trim"] = ["cortar", "tramo", "fragmento", "clip", "cut", "trim", "segment"],
        ["archive.zip"] = ["comprimir", "empaquetar", "compress", "pack"],
        ["archive.extract"] = ["descomprimir", "unzip", "abrir", "extract", "open"],
        ["action.save"] = ["guardar", "descargar", "extraer", "save", "download"],
        ["action.copy-path"] = ["ruta", "path", "direccion", "location"],
        ["action.copy-to"] = ["duplicar", "copia", "copy", "duplicate"],
        ["action.move-to"] = ["trasladar", "mover", "move"],
        ["action.show-in-folder"] = ["explorador", "carpeta", "abrir", "explorer", "folder", "reveal"],
        ["action.terminal"] = ["consola", "cmd", "powershell", "shell", "console", "terminal"],
        ["action.install.libreoffice"] = ["instalar", "office", "word", "excel", "powerpoint", "descargar", "install", "download"],
        ["action.install.calibre"] = ["instalar", "kindle", "ebook", "libro", "descargar", "install", "book", "download"],
    };

    /// <summary>Palabras extra de un id (vacío si no tiene).</summary>
    public static IReadOnlyList<string> For(string id) => Words.GetValueOrDefault(id) ?? [];
}

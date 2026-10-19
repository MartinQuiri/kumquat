# Kumquat 🍊

**Convierte archivos sin abrir nada.** Arrastra un archivo, presiona **Shift** y suéltalo sobre el formato que quieras en la rueda que aparece junto al cursor. Todo ocurre en tu PC: nada se sube a internet.

Kumquat es una alternativa libre (GPL-3.0) para Windows inspirada en la interacción de [Tangerine para Mac](https://tangerineformac.com). No está afiliado a Tangerine ni a Leap Studio.

> **Estado: pre-alfa (0.1).** La rueda de conversión con imágenes, audio y video funciona; PDF, documentos, herramientas y acciones vienen en las fases siguientes (ver [hoja de ruta](#hoja-de-ruta)).

## Cómo se usa

1. Ejecuta `Kumquat.exe`: aparece un 🍊 en el área de notificación.
2. Empieza a arrastrar un archivo desde el Explorador (o cualquier app).
3. Sin soltar, toca **Shift**. Aparece la rueda bajo el cursor.
4. Suelta sobre un formato. La copia convertida aparece junto al original (el original nunca se toca).

Soltar en el centro (✕) o presionar **Esc** cancela. El gesto se puede cambiar a Ctrl, Bloq Mayús o clic derecho desde el menú del ícono.

También hay una CLI:

```powershell
kumquat-cli convert foto.heic --to jpg
kumquat-cli convert *.wav --to mp3 --out D:\musica
kumquat-cli targets video.mkv      # a qué formatos se puede convertir
```

## Formatos (fase 1)

| Familia | Entrada | Salida |
|---|---|---|
| Imágenes | JPG, PNG, WebP, GIF, HEIC, AVIF, TIFF, BMP, SVG | JPG, PNG, WebP, GIF, AVIF, TIFF, BMP |
| Audio | MP3, M4A, WAV, FLAC, OGG, Opus, AIFF, WMA | las mismas |
| Video | MP4, MOV, MKV, WebM, AVI, WMV, GIF | las mismas + extraer audio + GIF |

Las conversiones de varios pasos se planifican solas (p. ej. BMP → PNG → WebP), y nunca se ofrecen destinos sin sentido (una foto no se convierte a MP3).

## Compilar

Requisitos: Windows 10 22H2+ o 11, [.NET SDK 10](https://dotnet.microsoft.com/download) (la versión exacta está en `global.json`).

```powershell
./tools/fetch-engines.ps1          # descarga FFmpeg verificando su SHA-256
dotnet build Kumquat.slnx
dotnet test --solution Kumquat.slnx --filter-not-trait "Category=E2E" --ignore-exit-code 8
dotnet run --project src/Kumquat.App
```

Para generar el paquete distribuible (self-contained, recortado, ~93 MB en zip, arranque ~0,3 s):

```powershell
./tools/publish.ps1 -Runtime win-x64     # o win-arm64
```

Los tests E2E hacen un arrastre real moviendo el mouse: córrelos solo cuando no estés usando el PC (`dotnet test --project tests/Kumquat.E2E.Tests`).

## Calidad

- Toda advertencia del compilador y de los analizadores (.NET, Meziantou, reglas propias) es un error.
- APIs peligrosas prohibidas en compilación: lanzar procesos con argumentos concatenados, extraer ZIP sin validar rutas, etc. ([BannedSymbols.txt](BannedSymbols.txt)).
- **Un test de integración por cada conversión directa**, generado desde el grafo de capacidades: si alguien agrega una conversión, queda probada automáticamente. Las salidas se verifican por contenido (bytes mágicos) y con ffprobe (duración, dimensiones, pistas).
- Tests basados en propiedades (CsCheck) para el planificador, la detección de formatos y la geometría de la rueda.
- Los motores corren como procesos aparte dentro de un *Job Object* de Windows (memoria acotada, mueren con Kumquat), con prioridad baja.
- CI en x64 y ARM64, CodeQL, OpenSSF Scorecard, REUSE y Dependabot.

Detalles en [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) y [CONTRIBUTING.md](CONTRIBUTING.md).

## Hoja de ruta

- [x] Fase 0–1: rueda de conversión, imágenes/audio/video, bandeja, CLI.
- [ ] Fase 2: PDF (MuPDF, Ghostscript), documentos (Pandoc), herramientas (comprimir, recortar, cortar, unir/dividir), atajo de teclado sobre la selección del Explorador, menú contextual de Windows 11.
- [ ] Fase 3: Office (LibreOffice), ebooks (Calibre), rueda de acciones (copiar ruta, mover, compartir, terminal), instalador firmado (SignPath), winget y Microsoft Store.
- [ ] Fase 4: sandbox de motores (baja integridad / AppContainer), fuzzing continuo.

## Licencia

[GPL-3.0-or-later](LICENSE). Kumquat incluye o descarga componentes de terceros con sus propias licencias: ver [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

**Patentes:** los códecs H.264, HEVC y AAC pueden estar sujetos a patentes en algunos países. Kumquat se distribuye como software libre sin licencias de patentes; revisa la situación en tu jurisdicción si lo redistribuyes comercialmente.

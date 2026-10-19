# Componentes de terceros

Kumquat (GPL-3.0-or-later) usa los siguientes componentes. Sus licencias se respetan íntegramente; donde la licencia lo exige, el código fuente correspondiente está disponible en el enlace indicado.

## Motores nativos (descargados por `tools/fetch-engines.ps1`, hash fijado en `tools/engines.json`)

| Componente | Versión | Licencia | Código fuente |
|---|---|---|---|
| FFmpeg (build de BtbN, variante GPL shared) | n8.1.3-18-g46d899d7a0 | GPL-3.0-or-later (incluye x264, x265, SVT-AV1, libvpx, LAME, Opus, Vorbis, libwebp, dav1d y otros) | https://github.com/FFmpeg/FFmpeg/commit/46d899d7a0 · scripts de build: https://github.com/BtbN/FFmpeg-Builds |

| qpdf | 12.4.2 | Apache-2.0 | https://github.com/qpdf/qpdf/tree/v12.4.2 |
| MuPDF (mutool) | 1.28.5 | AGPL-3.0-or-later (compatible con GPL-3.0, sección 13) | https://mupdf.com/downloads/archive/mupdf-1.28.5-source.tar.gz |
| Tesseract (compilado por Kumquat, estático) | 5.5.3 | Apache-2.0 | https://github.com/tesseract-ocr/tesseract/tree/5.5.3 · receta: `tools/ocr/vcpkg.json` + `tools/build-tesseract.ps1` |
| Modelos tessdata_best (español, inglés) | commit e12c65a | Apache-2.0 | https://github.com/tesseract-ocr/tessdata_best |

Tesseract incluye, enlazadas estáticamente, Leptonica 1.87 (BSD-2-Clause), libjpeg-turbo (IJG/BSD-3-Clause/Zlib), libpng y libspng (libpng-2.0/BSD-2-Clause), libtiff (libtiff), libwebp (BSD-3-Clause), OpenJPEG (BSD-2-Clause), giflib (MIT), zlib (Zlib), libarchive (BSD-2-Clause), bzip2, liblzma (0BSD), lz4 (BSD-2-Clause), zstd (BSD-3-Clause), curl (curl) y OpenSSL (Apache-2.0). El texto de cada licencia va en `engines/tesseract/licenses/`. Kumquat solo le pasa a Tesseract rutas locales que crea él mismo: nunca lee URLs.

Cada instalación del motor incluye un `SOURCE.txt` con su versión, licencia y enlace al código fuente.

## Paquetes NuGet distribuidos con la aplicación

| Paquete | Licencia | Proyecto |
|---|---|---|
| NetVips | MIT | https://github.com/kleisauke/net-vips |
| NetVips.Native (libvips y sus dependencias) | LGPL-2.1-or-later (libvips) y licencias de sus dependencias | https://github.com/kleisauke/libvips-packaging |
| Serilog, Serilog.Extensions.Logging, Serilog.Sinks.File | Apache-2.0 | https://github.com/serilog/serilog |
| SharpCompress | MIT | https://github.com/adamhathcock/sharpcompress |
| Microsoft.Extensions.* , System.Drawing.Common | MIT | https://github.com/dotnet/runtime |

## Diseño (en `assets/`, distribuidos con la aplicación)

| Recurso | Licencia | Proyecto |
|---|---|---|
| Tipografía Inter 4.1 | OFL-1.1 (`assets/fonts/Inter-LICENSE.txt`) | https://github.com/rsms/inter |
| Íconos Phosphor 2.1.1 (regular; trazados en `src/Kumquat.Design/IconPaths.g.cs`) | MIT (`assets/Phosphor-LICENSE.txt`) | https://github.com/phosphor-icons/core |

## Solo para desarrollo (no se distribuyen)

xunit.v3 (Apache-2.0), Shouldly (BSD-3-Clause), CsCheck (Apache-2.0), Meziantou.Analyzer (MIT), Microsoft.CodeAnalysis.BannedApiAnalyzers (MIT), Microsoft.Testing.Extensions.CodeCoverage (licencia de Microsoft para herramientas de desarrollo).

## Patentes

H.264, HEVC y AAC pueden estar cubiertos por patentes en algunas jurisdicciones. Estos avisos no otorgan licencias de patentes.

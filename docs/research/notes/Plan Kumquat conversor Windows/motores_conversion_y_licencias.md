# Conversion engines and licensing for Kumquat (Windows, C#/.NET, offline, closed-source / paid on Microsoft Store)

> Not legal advice. Research date: 2026-10-18.
> **How to read the tags:** **[V]** means the page was fetched or searched in this session. **[K]** means the claim comes from the researcher's prior knowledge. Each [K] claim cites the canonical URL where it can be checked, but that page was NOT re-read this session. Re-check every [K] claim before relying on it.

## 1. FFmpeg (audio, video, GIF, subtitles): LGPL vs GPL, encoders, builds, wrappers, patents

### Takeaway
An **LGPL, shared-DLL FFmpeg build** without `--enable-gpl` or `--enable-nonfree` can be shipped with a closed-source paid app, as long as Kumquat meets the FFmpeg LGPL checklist (source offer, notices, EULA wording, no DLL renaming). That rules out x264, x265 and fdk-aac. For H.264/HEVC/AAC encoding, use the Windows Media Foundation encoders built into the OS (via FFmpeg's `h264_mf`/`hevc_mf`/`aac_mf`) or OpenH264. The gyan.dev builds are all GPLv3, so use BtbN `lgpl-shared` or a custom build. Do not use Xabe.FFmpeg: it is non-commercial unless you buy a license.

### Cited Findings
**FFmpeg license rules [V]**
- FFmpeg is LGPL 2.1 or later. Some optional parts are GPL v2 or later, and "if those parts get used the GPL applies to all of FFmpeg." FFmpeg is "not available under any other licensing terms", so there is no commercial license to buy. — [ffmpeg.org/legal](https://ffmpeg.org/legal.html)
- The LGPL checklist on that page asks you to:
  - compile without `--enable-gpl` and `--enable-nonfree`;
  - link dynamically (on Windows, use DLLs);
  - distribute the exact matching source as a tarball or zip, on the same web server as your binary;
  - include a `git diff` of your changes and a text file with the configure line;
  - put an FFmpeg/LGPLv2.1 notice and source link on every download page, and mention FFmpeg in the About box and EULA;
  - in the EULA, not claim ownership of FFmpeg and remove any ban on reverse engineering;
  - keep the DLL names (adding a prefix or suffix is fine);
  - repeat these steps for any other LGPL library compiled in, such as LAME;
  - use no GPL libraries.
  — [ffmpeg.org/legal](https://ffmpeg.org/legal.html)
- The page names libx264 as a GPL library you must not use. — [ffmpeg.org/legal](https://ffmpeg.org/legal.html)

**Patents [V]**
- FFmpeg does not know whether it infringes patents. For commercial products it warns of possible licensing demands and says MPEG LA actively collects for MPEG technologies. — [ffmpeg.org/legal](https://ffmpeg.org/legal.html)

**Prebuilt Windows builds [V]**
- BtbN/FFmpeg-Builds offers these variants: `gpl`, `lgpl`, `nonfree`, and `-shared` versions of each, which provide the libav* shared libraries.
  - The `lgpl` variant leaves out GPL-only libraries, "most prominently libx264 and libx265".
  - The `nonfree` variant adds fdk-aac.
  - Auto-builds are win64 and winarm64. Only Windows 10 22H2 and later is supported.
  - The build scripts are under the MIT license.
  — [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds)
- gyan.dev: "All builds are 64-bit, static and licensed as GPLv3."
  - No LGPL build of FFmpeg is offered.
  - A shared build exists only for the release `full` variant.
  - Release essentials is 34 MB as .7z and 109 MB as .zip.
  — [gyan.dev builds](https://www.gyan.dev/ffmpeg/builds/)

**Encoders and their licenses [K]** (check against each project's license file)
- **GPL (avoid):** x264 ([VideoLAN x264](https://www.videolan.org/developers/x264.html)) and x265 ([x265 licensing](https://www.x265.org/x265-licensing/)). Both are GPL-2 with commercial licenses for sale; x265's is from MulticoreWare.
- **Nonfree (avoid):** fdk-aac. Its license is incompatible with the GPL, and FFmpeg treats it as nonfree.
- **BSD-style:**
  - OpenH264 (Cisco), [github.com/cisco/openh264](https://github.com/cisco/openh264). Cisco pays the MPEG LA H.264 royalties *only* for its own prebuilt binary downloaded from Cisco ([openh264.org](https://www.openh264.org/)). If you bundle that binary inside your installer, Cisco's royalty coverage arguably no longer applies; the intended model is to download it at runtime.
  - libvpx (VP8/VP9)
  - libaom and SVT-AV1 (AV1)
  - dav1d (AV1 decoding)
  - libopus
  - libvorbis and libogg
  - FLAC
- **LGPL:** LAME (MP3), [lame.sourceforge.io](https://lame.sourceforge.io/). MP3 patents have expired.
- **Windows Media Foundation encoders [K]:** FFmpeg wrappers `h264_mf`, `hevc_mf` and `aac_mf` ([FFmpeg docs](https://ffmpeg.org/ffmpeg-codecs.html)). These use encoders that ship with Windows, so Microsoft has already handled the patent licensing for them. The HEVC encoder needs the HEVC Video Extensions or hardware support (see section 2).
- **FFmpeg's built-in `aac` encoder [K]:** LGPL. AAC patents are still pooled by Via LA, so patent exposure remains.

**.NET wrappers**
- Xabe.FFmpeg is licensed CC BY-NC-SA 3.0 for non-commercial use. Commercial use needs a license from the vendor; no public price was found. — [NuGet Xabe.FFmpeg](https://www.nuget.org/packages/Xabe.FFmpeg/5.1.0); [libraries.io](https://libraries.io/nuget/Xabe.FFMpeg) [V]
- FFMpegCore is MIT-licensed. It runs the `ffmpeg.exe`/`ffprobe.exe` command-line tools as child processes and does not bundle FFmpeg binaries. — [github.com/rosenbjerg/FFMpegCore](https://github.com/rosenbjerg/FFMpegCore) [K]. The fetch of a mistaken URL returned 404, so this is unverified.
- FFmpeg.AutoGen is LGPL-3 and provides P/Invoke bindings for in-process use. — [github.com/Ruslan-B/FFmpeg.AutoGen](https://github.com/Ruslan-B/FFmpeg.AutoGen) [K]
- Sdcb.FFmpeg is LGPL and is an in-process binding with NuGet runtime packages. — [github.com/sdcb/Sdcb.FFmpeg](https://github.com/sdcb/Sdcb.FFmpeg) [K]
- Using an LGPL .NET binding as an unmodified, separate DLL is generally considered compatible with closed-source apps, but this is less clear-cut than calling an executable. [K]

### Inferences
- **Recommended setup:** BtbN `lgpl-shared` win64 (or a custom minimal build). Call `ffmpeg.exe` out of process through FFMpegCore, or through a small custom process runner.
  - Running FFmpeg as a separate process also isolates crashes and malformed-file exploits from the UI.
  - It keeps the linking boundary obvious, and users can swap in their own DLLs, which satisfies LGPL relinking.
- **Codec choices:**
  - H.264: `h264_mf` with OpenH264 as a fallback.
  - HEVC: `hevc_mf` only.
  - AAC: `aac_mf` (OS-licensed) or FFmpeg's native `aac`, which carries patent risk.
  - Also: libmp3lame, libopus, libvorbis, flac, libvpx/libaom/SVT-AV1 for WebM, and native PCM and AIFF.
  - WMA/WMV encoding is native in FFmpeg (`wmav2`, `wmv2`).
- **Size:** expect roughly 80–150 MB uncompressed for the full LGPL shared DLL set. A trimmed custom build with only the needed codecs could be roughly 20–40 MB. These are estimates, not measured.
- **GIF output:** use FFmpeg's `palettegen`/`paletteuse` filters.
- **Video to MP3:** use `-vn` with libmp3lame.
- **Trim:** stream copy with `-ss`/`-to`.
- **Split and merge:** the segment muxer and the concat demuxer.

### Gaps
- No exact size was found for the BtbN lgpl-shared archive.
- It was not confirmed which external libraries (libopus, libvpx, LAME, OpenH264) are in BtbN `lgpl` builds. The list is in the repo's `scripts.d` folder, which was not fetched.
- Whether a desktop app must pay Via LA for AAC or HEVC when it uses the OS Media Foundation encoders is not settled by an authoritative source here. Get legal review.

## 2. Images: libvips/NetVips, Magick.NET, ImageSharp, SkiaSharp, WIC, HEIC, AVIF, SVG

### Takeaway
- **Avoid ImageSharp** unless revenue stays under $1M; above that it needs a commercial license from $799/year.
- **NetVips** (MIT wrapper, LGPL libvips with natives included) or **Magick.NET** (Apache-2) are both viable for closed-source use.
- **HEIC** is the hard format because of HEVC patents. The cleanest legal route is decoding through WIC with the user-installed Microsoft HEIF + HEVC extensions (the HEVC one is $0.99). Bundling libheif/libde265 (LGPL) is an alternative but carries patent exposure.

### Cited Findings
**ImageSharp [V]**
- A commercial license is required when ImageSharp is a direct dependency in closed-source software of a for-profit business "earning more than 1M USD in annual gross revenue". Below that, it is free under the Apache-2 terms of the Six Labors Split License.
- Prices: Boutique $799/year (up to 10 developers), Agency $1,299/year, Enterprise $4,999/year. A Community tier is available by application.
- — [sixlabors.com/pricing](https://sixlabors.com/pricing/)

**HEIC on Windows [V]**
- HEIF Image Extensions is free, but it cannot read or write .heic files without HEVC Video Extensions, which costs $0.99 in the Store.
- A free "HEVC Video Extensions from Device Manufacturer" listing also exists.
- — [Windows Central](https://windowscentral.com/microsoft-now-charging-hevc-video-extensions); [Adobe HEVC help](https://helpx.adobe.com/premiere-elements/using/hevc-decoding-on-windows.html)
- Pricing pages are from 2018–2024. A 2024 article still lists $0.99. The current price was not verified in the Store.

**Library licenses [K]** (canonical sources, not fetched)
- **libvips** is LGPL-2.1 or later ([github.com/libvips/libvips](https://github.com/libvips/libvips)). **NetVips** is MIT, and its NetVips.Native packages bundle libvips plus its dependencies ([github.com/kleisauke/net-vips](https://github.com/kleisauke/net-vips)). The latest NetVips.Native.win-x64 is 8.18.2, updated 2026-04-03 [V] ([NuGet profile](https://www.nuget.org/profiles/kleisauke)).
- **Magick.NET** is Apache-2.0 ([github.com/dlemstra/Magick.NET](https://github.com/dlemstra/Magick.NET)), and ImageMagick uses an Apache-2-style license ([imagemagick.org/license](https://imagemagick.org/script/license.php)). It is large (tens of MB per platform) and links many delegate libraries. Its PDF reading needs Ghostscript, which is AGPL.
- **SkiaSharp** is MIT ([github.com/mono/SkiaSharp](https://github.com/mono/SkiaSharp)). It has no HEIC, AVIF or TIFF support and limited WebP encoding.
- **WIC (Windows Imaging Component)** is built into Windows. Codecs include JPEG, PNG, TIFF, BMP, GIF, ICO, JPEG-XR and DDS. WebP, HEIF and AVIF depend on extensions installed from the Store ([WIC native codecs](https://learn.microsoft.com/en-us/windows/win32/wic/native-wic-codecs)).
- **libheif** is LGPL-3 and **libde265** is LGPL-3 ([github.com/strukturag/libheif](https://github.com/strukturag/libheif)). HEVC encoding inside libheif needs x265 (GPL) or kvazaar (BSD-3), and HEVC patents apply either way.
- **AVIF:** libavif is BSD-2, dav1d is BSD-2, and libaom is BSD-2 with the AOM patent license.
- **SVG:** resvg is Apache-2/MIT (Rust, has a C API), Svg.Skia is MIT, and librsvg is LGPL-2.1+.

**Security [V]**
- CVE-2023-4863 is a libwebp Huffman heap overflow affecting versions before 1.3.2. It was exploited in the wild, and Magick.NET packages are listed as affected. — [Tarlogic](https://www.tarlogic.com/blog/cve-2023-4863/); [GitLab advisory for Magick.NET](https://advisories.gitlab.com/nuget/magick.net-q16-anycpu/CVE-2023-4863/)

### Inferences
- **Primary engine:** NetVips. It is fast, uses little memory, and natively handles JPEG, PNG, WebP, TIFF, GIF, HEIF/AVIF and SVG (via librsvg), with PDF loading via PDFium/poppler depending on the build. The natives are estimated at about 15–25 MB.
  - Check whether the NetVips.Native build includes an HEVC decoder. It is believed to ship libheif with AVIF only (dav1d/aom) because of patents. This is unverified.
- **HEIC:** decode through WIC when the Microsoft extensions are present. Otherwise prompt the user to install the HEIF and HEVC extensions.
- **HEIC encoding:** avoid at launch, or use the WIC HEIF encoder when it is available.
- **PDF export from images:** PDFsharp (MIT) or QuestPDF (see section 3).
- **Image compress and crop:** NetVips operations.

### Gaps
- No exact NetVips.Native.win-x64 package size was found.
- It was not confirmed whether the NetVips natives include libde265 or HEVC.
- WIC's HEIF encoder availability and API were not verified.

## 3. PDF: PDFium, MuPDF, Ghostscript, QPDF, PdfPig, PDFsharp, iText, QuestPDF, PDF→Office, OCR

### Takeaway
- **Use permissive engines only:** PDFium for rendering PDFs to JPG/PNG and for text, PdfPig for structured text extraction, and PDFsharp or QPDF for merge, split and compress.
- **Avoid AGPL engines** (MuPDF, Ghostscript, iText) unless you buy a commercial license; Artifex prices are quote-only.
- **Avoid QuestPDF** unless revenue stays under $1M; the Professional license is $1,999/year.
- **PDF→DOCX/PPTX/XLSX** at good quality realistically needs LibreOffice's PDF import (limited quality) or a paid SDK.

### Cited Findings
**Artifex: MuPDF and Ghostscript [V]**
- Most products are dual-licensed AGPLv3 or commercial. The commercial OEM license is a "per-copy cost with a quarterly minimum fee", and no public prices are given. — [artifex.com/licensing](https://artifex.com/licensing)

**QuestPDF [V]**
- The Community License covers businesses with under $1,000,000 revenue in their most recently completed fiscal year, measured across entities under common control. Publicly traded companies are excluded. — [QuestPDF license guide](https://www.questpdf.com/license/guide.html)
- Professional costs $1,999/year for one entity, with unlimited developers, royalty-free desktop redistribution and offline use. Enterprise costs $4,999/year. — [QuestPDF pricing](https://www.questpdf.com/pricing.html)

**Other libraries [K]** (canonical sources, not fetched)
- **PDFium** is BSD-3/Apache-2 ([pdfium.googlesource.com](https://pdfium.googlesource.com/pdfium/)). Prebuilt Windows binaries come from bblanchon/pdfium-binaries ([GitHub](https://github.com/bblanchon/pdfium-binaries)), at roughly 5–8 MB per architecture.
  - .NET bindings: PDFiumCore (MIT), Docnet.Core (MIT), and PdfiumViewer (Apache-2, archived).
- **QPDF** is Apache-2 ([github.com/qpdf/qpdf](https://github.com/qpdf/qpdf)). It is a CLI that does merge, split, linearize and object-stream compression, but it does not recompress images.
- **PdfPig** is Apache-2 ([github.com/UglyToad/PdfPig](https://github.com/UglyToad/PdfPig)).
- **PDFsharp** 6.x is MIT ([github.com/empira/PDFsharp](https://github.com/empira/PDFsharp)).
- **iText** is AGPL or commercial ([itextpdf.com](https://itextpdf.com/how-buy/AGPLv3-license)).
- **pdf2docx** (Python) is AGPL-3.0 per its repository ([github.com/ArtifexSoftware/pdf2docx](https://github.com/ArtifexSoftware/pdf2docx)). It is now under Artifex and depends on PyMuPDF (AGPL), so avoid it.
- **Tesseract** is Apache-2 ([github.com/tesseract-ocr/tesseract](https://github.com/tesseract-ocr/tesseract)). The .NET wrapper `Tesseract` (charlesw) is Apache-2. Language data adds about 10–20 MB per language with tessdata_fast.
- **Windows.Media.Ocr** is a built-in Windows OCR API with no extra size ([docs](https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr)).
- **Syncfusion Community License:** free for companies under $1M revenue with 5 or fewer developers and at most 10 employees ([syncfusion.com](https://www.syncfusion.com/products/communitylicense)). Aspose is commercial-only, with per-developer or OEM prices in the thousands of USD ([purchase.aspose.com](https://purchase.aspose.com/pricing)). The Aspose figure is unverified this session.

### Inferences
- **PDF→JPG/PNG:** PDFium.
- **PDF→TXT:** PdfPig.
- **PDF→MD:** PdfPig plus heuristics.
- **PDF→EPUB:** text extraction, then build the EPUB yourself.
- **Merge and split:** PDFsharp in-process, or the QPDF CLI.
- **Compress:** rasterize or downsample images with PDFium and NetVips, then rewrite with PDFsharp. Without Ghostscript, true "compress" quality is the hardest feature.
- **PDF→DOCX/PPTX/XLSX:** first version via LibreOffice (`--infilter="writer_pdf_import"`), whose output quality is mediocre. For quality, consider a Syncfusion Community license while revenue is small, then budget for a commercial SDK. A paid Store app growing past $1M must switch.

### Gaps
- No public MuPDF commercial price was found.
- Syncfusion Community terms were not re-fetched this session.

## 4. Documents, slides, spreadsheets: Pandoc, LibreOffice, OpenXML SDK, ClosedXML, NPOI, Markdig, EPUB, Office COM

### Takeaway
- **Pandoc** (GPL-2+) can be shipped as a separate executable called at arm's length, per the FSF's "aggregate" and command-line guidance. The GPL obligations still apply to pandoc.exe itself: provide its source and the license.
- **LibreOffice headless** (MPL-2) is the only realistic offline engine for PPT/PPTX→PDF/JPG and DOC/XLS/ODS→PDF. It is big, so download it on demand or ship it as an optional component.
- **Pure .NET MIT/Apache libraries** cover the rest.

### Cited Findings
**GPL and separate executables [V]**
- An "aggregate" of separate programs may be distributed together "even when the licenses of the other software are nonfree".
- "Pipes, sockets and command-line arguments are communication mechanisms normally used between two separate programs." The exception is when the communication is "intimate enough, exchanging complex internal data structures".
- — [GNU GPL FAQ](https://www.gnu.org/licenses/gpl-faq.html)

**Library licenses [K]** (canonical sources, not fetched)
- **Pandoc** is GPL-2.0-or-later ([github.com/jgm/pandoc](https://github.com/jgm/pandoc)). The Windows pandoc.exe is a single binary of roughly 100–200 MB uncompressed.
- **LibreOffice** is MPL-2.0 ([libreoffice.org/about-us/licenses](https://www.libreoffice.org/about-us/licenses)). The Windows install is roughly 300–400 MB+.
  - Run it headless with `soffice --headless --convert-to`.
  - unoserver (MIT) keeps a warm listener to avoid per-file startup cost, which is several seconds cold ([github.com/unoconv/unoserver](https://github.com/unoconv/unoserver)).
  - Disable macros by passing a fresh user profile (`-env:UserInstallation=`) with macro security set to high.
- **MIT:** DocumentFormat.OpenXml (OpenXML SDK) ([GitHub](https://github.com/dotnet/Open-XML-SDK)), ClosedXML ([GitHub](https://github.com/ClosedXML/ClosedXML)), ExcelDataReader, which reads XLS/XLSX/CSV ([GitHub](https://github.com/ExcelDataReader/ExcelDataReader)), VersOne.Epub ([GitHub](https://github.com/vers-one/EpubReader)), and RtfPipe ([GitHub](https://github.com/erdomke/RtfPipe)).
- **Apache-2:** NPOI, which reads and writes XLS/XLSX/DOCX ([GitHub](https://github.com/nissl-lab/npoi)), and MiniExcel ([GitHub](https://github.com/mini-software/MiniExcel)).
- **BSD-2:** Markdig ([GitHub](https://github.com/xoofx/markdig)).
- **Microsoft Office COM automation:** Microsoft does not recommend or support server-side or unattended Office automation ([KB257757](https://support.microsoft.com/en-us/topic/considerations-for-server-side-automation-of-office-48bcfe93-8a89-47f1-0bce-017433ad79e2)). Desktop interactive use is acceptable as an optional "if Office is installed" path, for example `SaveAs` PDF from PowerPoint, which gives the best PPTX fidelity.

### Inferences
- **Markdown↔HTML:** Markdig.
- **DOCX read/write:** OpenXML SDK. DOCX→MD/TXT/HTML needs a custom walker, or use Pandoc.
- **EPUB:** VersOne.Epub to read; write it as a ZIP of XHTML.
- **RTF:** RtfPipe, RTF→HTML only.
- **XLSX/CSV/TSV:** ClosedXML/MiniExcel. **XLS input:** ExcelDataReader or NPOI. **ODS:** LibreOffice or a custom parser of ODS's XML.
- **Spreadsheet/document→PDF and PPT/PPTX→PDF/JPG/PNG:** LibreOffice, or Office COM when present.
- **Pandoc:** ship it as an optional download, which keeps the Store package small and makes the GPL boundary obvious. Include the GPL text and a source link.

### Gaps
- Exact sizes of a minimal LibreOffice install for Windows were not measured.
- No official "portable minimal" build was found.

## 5. Archives and subtitles

### Takeaway
- Use **System.IO.Compression** (built in) for ZIP and GZip, and **System.Formats.Tar** (.NET 7+) for TAR.
- Use **SharpCompress** (MIT) to read RAR. Do not bundle 7-Zip's unRAR code to *create* RAR; the unRAR license forbids that.
- For subtitles, write a small custom SRT↔VTT parser, or use FFmpeg.

### Cited Findings [K]
- SharpCompress is MIT and can read RAR, 7z, zip, tar, gzip and bzip2 ([github.com/adamhathaway/sharpcompress](https://github.com/adamhathaway/sharpcompress)).
- **7-Zip:** LGPL-2.1, plus the unRAR restriction (you may not use the code to re-create the RAR compression algorithm) and BSD-3 for some parts ([7-zip.org/license.txt](https://www.7-zip.org/license.txt)).
- System.Formats.Tar has been in .NET since 7 ([docs](https://learn.microsoft.com/en-us/dotnet/api/system.formats.tar)).
- FFmpeg converts between srt, webvtt and ass subtitle formats ([FFmpeg formats](https://ffmpeg.org/ffmpeg-formats.html)).

### Inferences
- All archive formats can be handled in-process at under 1 MB of extra size.
- Guard against path traversal ("zip slip") and decompression bombs.

### Gaps
- None critical.

## 6. Bundling vs on-demand download, Store package limits, prior art

### Takeaway
- The Store allows MSIX packages up to **25 GB**, so size is not a hard blocker.
- **EXE/MSI submissions must be offline installers with no downloads during setup.**
- Downloading engines after install (on first use) is the pattern apps like Kumquat would use for LibreOffice and Pandoc. Confirm it against Store policy 10.2.x on dynamically downloaded code before relying on it.

### Cited Findings
**Store packaging [V]**
- Limits are 25 GB per .msixbundle and 25 GB per .msix package for Windows 10/11.
- EXE/MSI installers must be offline, with "no downloads during setup", and must not bundle unrelated software.
- The Store re-signs MSIX packages for free.
- — [MS Learn: app package requirements](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements)

**Prior art [V]**
- Tichau File Converter is GPL-3 itself and bundles ffmpeg 8.0.1, ImageMagick and Ghostscript 10.02.1. Because it is open source, it can use GPL/AGPL engines; a closed-source app cannot copy this setup. — [github.com/Tichau/FileConverter](https://github.com/Tichau/FileConverter)

**Prior art [K]**
- HandBrake is GPL-2 and bundles x264/x265 ([handbrake.fr](https://handbrake.fr/)). It can do so only because it is GPL itself.
- Stirling-PDF and ConvertX are server or Docker apps that bundle LibreOffice, Ghostscript, Pandoc, etc. They are open source under MIT/AGPL ([Stirling-PDF](https://github.com/Stirling-Tools/Stirling-PDF), [ConvertX](https://github.com/C4illin/ConvertX)).

### Inferences
- **Core package, estimated 60–120 MB:**
  - .NET app
  - LGPL FFmpeg shared build
  - NetVips natives
  - PDFium
  - managed libraries
- **Optional "packs" downloaded on demand and checksum-verified:**
  - LibreOffice (about 350 MB) for slides, ODS/XLS→PDF and PDF→Office
  - Pandoc (about 150 MB)
  - Tesseract language data
  - OpenH264 from Cisco, which also keeps Cisco's royalty coverage
- The Store policy on downloading executables at runtime (policy 10.2.2) was not verified; it needs checking.

### Gaps
- The Microsoft Store policy text on dynamically downloaded code was not fetched.

## 7. Security on untrusted files and keeping engines updated

### Takeaway
- Run every native engine **out of process**, with a timeout and memory limit (a Job Object), in a low-privilege temp directory.
- Disable risky coders and macros.
- Track CVEs for libwebp, libvips/ImageMagick, PDFium, FFmpeg and LibreOffice, and ship engine updates through Store updates.

### Cited Findings
**libwebp [V]**
- CVE-2023-4863 affects libwebp before 1.3.2. Crafted WebP files cause out-of-bounds writes, and it was exploited in the wild. It affected Magick.NET packages. — [Tarlogic](https://www.tarlogic.com/blog/cve-2023-4863/); [GitLab advisory](https://advisories.gitlab.com/nuget/magick.net-q16-anycpu/CVE-2023-4863/)

**Other engines [K]** (canonical sources, not fetched)
- **ImageMagick:** "ImageTragick" (CVE-2016-3714) allowed command injection through delegates. Mitigate with `policy.xml` to disable the MVG, MSL, EPHEMERAL, URL, PS/EPS/PDF and SVG coders ([imagetragick.com](https://imagetragick.com/); [ImageMagick security policy](https://imagemagick.org/script/security-policy.php)).
- **Ghostscript:** repeated sandbox bypass and RCE CVEs, for example CVE-2023-36664 and CVE-2024-29510 ([NVD](https://nvd.nist.gov/vuln/detail/CVE-2024-29510)).
- **LibreOffice:** macro-execution CVEs, for example CVE-2023-2255 and CVE-2023-6186 ([libreoffice.org/about-us/security](https://www.libreoffice.org/about-us/security/advisories/)).

### Inferences
- Avoiding Ghostscript and ImageMagick (for licensing reasons) also shrinks the attack surface.
- Rendering PDFs through PDFium, which Chromium maintains, is safer.
- Disable network access for all engines. LibreOffice should not fetch remote links; set its security options.

### Gaps
- An exact CVE count per engine for 2025–2026 was not gathered.

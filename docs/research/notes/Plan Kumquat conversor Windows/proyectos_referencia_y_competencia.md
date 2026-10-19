# Reference projects and competitive landscape for Kumquat (a Windows clone of Tangerine)

Research date: 2026-10-18. GitHub stats (stars, language, SPDX license, last push, open issues) were pulled live with the GitHub REST API (`gh api repos/<owner>/<repo>`) on this date. Each repo URL below is the source for its stats.

## 1. Which open-source Windows apps are most similar, and what can be learned from their architecture and code quality?

### Takeaway
No open-source Windows app combines drag-plus-modifier, a radial wheel and multi-domain conversion, except one new, unknown, 0-star Python/Qt clone ("tangerine-windows"). The closest functional analogue is File Converter (C#/WPF, GPL-3.0, about 15.4k stars). It wraps bundled FFmpeg, ImageMagick and Ghostscript behind an Explorer context menu, and its biggest complaint is that it is missing from the Windows 11 context menu. That is exactly the integration risk that PowerToys solves with dual registration (a legacy IContextMenu handler plus a sparse-MSIX IExplorerCommand). For engine coverage, ConvertX and VERT give a ready-made per-format engine map. Stirling-PDF shows which PDF engines are needed.

### Cited Findings

**GitHub snapshot (2026-10-18)**

| Repo | Stars | Language | License (SPDX per API) | Last push | Open issues |
|---|---|---|---|---|---|
| [microsoft/PowerToys](https://github.com/microsoft/PowerToys) | 139,467 | C (mixed C++/C#) | MIT | 2026-10-18 | 7,777 |
| [Stirling-Tools/Stirling-PDF](https://github.com/Stirling-Tools/Stirling-PDF) | 94,455 | TypeScript/Java | NOASSERTION ("open-core") | 2026-10-19 | 544 |
| [jgm/pandoc](https://github.com/jgm/pandoc) | 46,626 | Haskell | GPL-2.0 | 2026-10-18 | 1,044 |
| [files-community/Files](https://github.com/files-community/Files) | 46,068 | C# | MIT | 2026-10-18 | 465 |
| [ShareX/ShareX](https://github.com/ShareX/ShareX) | 40,176 | C# | GPL-3.0 | 2026-10-18 | 621 |
| [QL-Win/QuickLook](https://github.com/QL-Win/QuickLook) | 25,375 | C# | GPL-3.0 | 2026-10-14 | 217 |
| [HandBrake/HandBrake](https://github.com/HandBrake/HandBrake) | 24,709 | C | NOASSERTION (GPL-2 COPYING file) | 2026-10-18 | 293 |
| [BCUninstaller/Bulk-Crap-Uninstaller](https://github.com/Klocman/Bulk-Crap-Uninstaller) | 21,938 | C# | Apache-2.0 | 2026-09-07 | 179 |
| [C4illin/ConvertX](https://github.com/C4illin/ConvertX) | 19,155 | TypeScript | AGPL-3.0 | 2026-10-18 | 114 |
| [VERT-sh/VERT](https://github.com/VERT-sh/VERT) | 16,015 | TypeScript | AGPL-3.0 | 2026-10-18 | 43 |
| [Flow-Launcher/Flow.Launcher](https://github.com/Flow-Launcher/Flow.Launcher) | 15,819 | C# | MIT | 2026-10-18 | 303 |
| [Tichau/FileConverter](https://github.com/Tichau/FileConverter) | 15,411 | C# | GPL-3.0 | 2026-02-27 | 364 |
| [d2phap/ImageGlass](https://github.com/d2phap/ImageGlass) | 14,656 | C# | NOASSERTION | 2026-10-17 | 230 |
| [kando-menu/kando](https://github.com/kando-menu/kando) | 6,476 | TypeScript | NOASSERTION (REUSE, multi-license) | 2026-10-14 | 77 |
| [cyanfish/naps2](https://github.com/cyanfish/naps2) | 4,650 | C# | NOASSERTION | 2026-10-15 | 249 |
| [oliverschwendener/ueli](https://github.com/oliverschwendener/ueli) | 4,610 | TypeScript | MIT | 2026-10-16 | 75 |
| [paulpacifico/shutter-encoder](https://github.com/paulpacifico/shutter-encoder) | 2,835 | Java | GPL-3.0 | 2026-09-28 | 237 |
| [eibols/ffmpeg_batch](https://github.com/eibols/ffmpeg_batch) | 1,584 | C# | LGPL-2.1 | 2026-07-09 | 19 |
| [Faster3ck/Converseen](https://github.com/Faster3ck/Converseen) | 1,174 | C++ (Qt) | GPL-3.0 | 2026-10-07 | 91 |
| [GameGodS3/DropPoint](https://github.com/GameGodS3/DropPoint) | 1,098 | JavaScript (Electron) | GPL-3.0 | 2026-08-04 | 27 |

**Direct Tangerine clones (all created September/October 2026, very low adoption)**
- [CristianO2601/tangerine-windows](https://github.com/CristianO2601/tangerine-windows): 0 stars, Python, MIT, created 2026-09-16. It is a Python 3 + PySide6 tray app for Windows 11. Holding Shift while dragging out of Explorer shows a radial wheel, and Alt+Shift shows tools. The README describes it as an independent reimplementation that is "not affiliated" with Tangerine — [README](https://github.com/CristianO2601/tangerine-windows)
  - Drag detection: the app "watches for drag sessions in Explorer (using public Win32 state and the OLE drag clipboard — no input hooks)". Output is always a new sibling file, and "the drop itself is copy-only" — [README](https://github.com/CristianO2601/tangerine-windows)
  - Engines: it needs an external FFmpeg/ffprobe (via `winget install Gyan.FFmpeg`) and optionally WinRAR for .rar. It also uses Pillow, pillow-heif, pypdf, pypdfium2 and python-docx, plus optional RapidOCR (onnxruntime) for scanned PDFs — [README](https://github.com/CristianO2601/tangerine-windows)
  - Quality: a pytest suite of 183 tests that "synthesizes its own folders" and does not need FFmpeg. The Inno Setup installer is per-user and unsigned, and the README warns that "Windows SmartScreen may" block it. The wheel is suppressed over full-screen apps and known game-engine windows. The app also adds "Send to" shortcuts in the right-click menu — [README](https://github.com/CristianO2601/tangerine-windows)
- Mac clones: [nftstory/orbitdrop](https://github.com/nftstory/orbitdrop) (Swift, MIT, 0 stars, describes itself as an "early implementation") and [ishanmalu/daisy](https://github.com/ishanmalu/daisy) (Swift, 1 star; "hold Shift, drag a file, drop it on a petal").

**File Converter (Tichau): the closest Windows analogue**
- It is "a very simple tool which allows you to convert and compress one or several file(s) using the context menu of windows explorer". The project started in 2014, is GPL-3.0, and its site is file-converter.io — [README](https://github.com/Tichau/FileConverter)
- Bundled middlewares: ffmpeg v8.0.1, ImageMagick (via the dlemstra C# wrapper, Magick.NET), Ghostscript 10.02.1, SharpShell for the context-menu extension, Ripper/yeti.mmedia for CD audio, Markdown.XAML and WpfAnimatedGif (WPF UI) — [README](https://github.com/Tichau/FileConverter)
- Repo layout: `Application/` (FileConverter, FileConverterExtension), `Middleware/` (ffmpeg, gs, DLLs committed as binaries), `Installer/` (WiX 5, signed with the Windows SDK signing tools) and `PresetSamples/`. There is no `.github/workflows` folder (the API returns 404), so there is no visible GitHub Actions CI — [repo](https://github.com/Tichau/FileConverter), [README](https://github.com/Tichau/FileConverter)
- The latest release is v2.2 (2026-02-13) — [releases](https://github.com/Tichau/FileConverter/releases)
- Most-discussed issues: #633 "does not appear in the context menu" (28 comments, open), #382 "Can't Install" (25), #465 "stopped appearing in the context menu" (15, open), #226 AVIF support (14, open), #673 localization broken (13), #96 HEIC request (12) and #211 "Windows 11 compatibility" (12, open) — [issues](https://github.com/Tichau/FileConverter/issues?q=is%3Aissue+sort%3Acomments-desc)
- In #633, Win11 25H2 users report that the entry only appears under "Show more options" or not at all, even after reinstalling. One user asked for "a fallback UI when started manually from the start menu" — [issue #633](https://github.com/Tichau/FileConverter/issues/633)

**PowerToys: patterns for modules, context menus and CI**
- Runner architecture: `PowerToys.exe` manages the tray icon, loads module-interface DLLs, enables and disables modules, handles global hotkeys (low-level keyboard hooks), updates and settings, and runs a Windows message loop with a single-instance mutex — [runner.md](https://github.com/microsoft/PowerToys/blob/main/doc/devdocs/core/runner.md)
- Context menus come in two kinds. Legacy handlers are registry-registered `IContextMenu` COM objects. Windows 11 handlers are sparse MSIX packages that expose `IExplorerCommand`, are "registered for all file types and filtered in code" (`GetState`), and "require signing to be installed". With dual registration (ImageResizer, PowerRename), both are registered so the feature works "even if Windows 11 handler fails to appear", at the cost of a duplicate entry under "Show more options". The package is installed with `PackageManager.AddPackageAsync` when the module is enabled, and Explorer may need a restart — [context-menus.md](https://github.com/microsoft/PowerToys/blob/main/doc/devdocs/common/context-menus.md)
- Image Resizer is made of a shell-extension DLL, a UI app and a core library. It uses C++/WinRT, WPF and the Windows Imaging Component (WIC). Visibility is decided with `AssocGetPerceivedType()`. It respects Group Policy (`GPOWrapper`), and batches arrive through the command line (`ResizeBatch.FromCommandLine()`) — [imageresizer.md](https://github.com/microsoft/PowerToys/blob/main/doc/devdocs/modules/imageresizer.md)
- Developer docs include logging, telemetry events (`events.md`), "watson-queries", a disk-usage footprint page, CLI conventions, run-as-admin detection and embedded MSIX — [doc/devdocs](https://github.com/microsoft/PowerToys/tree/main/doc/devdocs)
- CI and signing: `.pipelines/` holds ESRP signing configs, version and sign checks, NuGet/NOTICE verification, UI-test scripts (WinAppDriver), CIPolicy.xml and loc. GitHub workflows include a `telemetry-pr-check`, spelling, dependency review, issue triage and dedupe, and MS Store submissions — [.pipelines](https://github.com/microsoft/PowerToys/tree/main/.pipelines), [workflows](https://github.com/microsoft/PowerToys/tree/main/.github/workflows)
- Modules include imageresizer, FileLocksmith, peek, powerrename, cmdpal (Command Palette), NewPlus, PowerOCR and AdvancedPaste — [src/modules](https://github.com/microsoft/PowerToys/tree/main/src/modules)
- The devdocs list the prerequisites as VS 2026 or 2022, .NET 8 and the WinUI workload. They also note that some modules (PowerRename, ImageResizer, Explorer extensions) "will not be available unless you also build the installer and install PowerToys" — [devdocs readme](https://github.com/microsoft/PowerToys/blob/main/doc/devdocs/readme.md)

**Universal converters: engine map per format**
- ConvertX is a TypeScript/Bun/Elysia self-hosted converter with "over a thousand different formats". Its engines are FFmpeg (~472 in / ~199 out), ImageMagick (245/183), GraphicsMagick, Vips (45/23), libheif, libjxl, resvg, Inkscape, Pandoc (43/65), LibreOffice (41/22), Calibre (26/19), XeLaTeX, DjVuLibre, Dasel (data), msgconvert (Outlook), Assimp (3D), Potrace/VTracer (raster to vector), Markitdown and pdftops/poppler. Engines are invoked as CLI processes inside a Docker image, with knobs such as `FFMPEG_ARGS` (hardware acceleration) and `MAX_CONVERT_PROCESS` — [README](https://github.com/C4illin/ConvertX)
- ConvertX CI covers lint (Biome/ESLint), `run-bun-test`, autofix, conventional-commit labels and Docker publishing — [workflows](https://github.com/C4illin/ConvertX/tree/main/.github/workflows). Its top issues are Docker and login problems (#235, #272), LibreOffice PDF to DOCX failing (#425), a request for FFmpeg hardware acceleration (#190) and HEIC to PNG errors (#202) — [issues](https://github.com/C4illin/ConvertX/issues?q=is%3Aissue+sort%3Acomments-desc)
- VERT converts "on your device instead of a cloud" with WebAssembly, in Svelte/TS, for "250+" formats. Its converter modules are `ffmpeg`, `magick`, `mediabunny`, `pandoc` and `vertd` (a server daemon for video). Dependencies include `@ffmpeg/ffmpeg` 0.12, `@imagemagick/magick-wasm` and `vert-wasm`. It admits that some video conversion "may require server-side processing" — [README](https://github.com/VERT-sh/VERT), [converters dir](https://github.com/VERT-sh/VERT/tree/main/src/lib/converters), [package.json](https://github.com/VERT-sh/VERT/blob/main/package.json). Its top issues include "PDF support" (#145, open) and "Error in VIPS worker" (#68) — [issues](https://github.com/VERT-sh/VERT/issues?q=is%3Aissue+sort%3Acomments-desc)
- Stirling-PDF offers "50+ PDF tools" as a desktop client, browser UI and server API, and describes itself as "open-core" — [README](https://github.com/Stirling-Tools/Stirling-PDF). Its base Docker image pre-builds "Calibre, Ghostscript, QPDF, ImageMagick, Python venv with AI/OCR packages" and poppler-utils — [docker/base/Dockerfile](https://github.com/Stirling-Tools/Stirling-PDF/blob/main/docker/base/Dockerfile). There is a dedicated `unoserver` (LibreOffice) container and a LibreOffice sandbox (`lo-sandbox.c`) — [docker dir](https://github.com/Stirling-Tools/Stirling-PDF/tree/main/docker). The Java backend uses Apache PDFBox (preflight, xmpbox, jbig2), BouncyCastle, commonmark and Batik — [app/core/build.gradle](https://github.com/Stirling-Tools/Stirling-PDF/blob/main/app/core/build.gradle). Its CI is very extensive (about 50 workflows): a `tauri-build` desktop build, e2e, a11y, OpenSSF scorecards, license checks, a PR size limit and multi-OS releases — [workflows](https://github.com/Stirling-Tools/Stirling-PDF/tree/main/.github/workflows)

**Other reference apps: structure, CI and complaints**
- ShareX is split into multiple library projects: HelpersLib, HistoryLib, ImageEffectsLib, ScreenCaptureLib, ScreenRecordingLib, UploadersLib, ImageEditor, Setup, Steam and NativeMessagingHost. It also has a `ShareX.Avalonia` folder. CI uses `build.yml`, `pr.yml` and `stale.yml` — [repo](https://github.com/ShareX/ShareX)
- HandBrake keeps the core library (`libhb`) separate from per-platform UIs (`win`, `macosx`, `gtk`) and has per-OS workflows (windows.yml, mac.yml, linux.yml, flatpak.yml) — [repo](https://github.com/HandBrake/HandBrake)
- Flow Launcher has a separate `Flow.Launcher.Plugin` SDK project, `Flow.Launcher.Core` and `Flow.Launcher.Infrastructure`, a `Flow.Launcher.Test` project and built-in plugins as separate projects (Explorer, Shell, Calculator...). Releases are deployed to winget and translations go through Crowdin — [repo](https://github.com/Flow-Launcher/Flow.Launcher), [Plugins](https://github.com/Flow-Launcher/Flow.Launcher/tree/main/Plugins), [workflows](https://github.com/Flow-Launcher/Flow.Launcher/tree/main/.github/workflows)
- Files (WinUI 3) has separate CD pipelines for sideload and Microsoft Store (preview and stable), CI, XAML formatting and a `tests/` folder. Its license switched to MIT after a 198-comment issue (#7625). Other top issues are "Missing icons on Windows 10" and shell-address errors — [repo](https://github.com/files-community/Files), [workflows](https://github.com/files-community/Files/tree/main/.github/workflows), [issues](https://github.com/files-community/Files/issues?q=is%3Aissue+sort%3Acomments-desc)
- QuickLook has a plugin model (`QuickLook.Plugin`, `QuickLook.Common`), a native helper (`QuickLook.Native`), an Appx/Store package and an installer. Top issues are "Do not Lock file" (#603), popup broken after upgrading to Windows 11 (#955), no ICC awareness (#329) and a white rectangle on Intel HD 4000 (#442) — [repo](https://github.com/QL-Win/QuickLook), [issues](https://github.com/QL-Win/QuickLook/issues?q=is%3Aissue+sort%3Acomments-desc)
- NAPS2 separates `NAPS2.Sdk`, `NAPS2.Lib`, per-platform UI projects (WinForms, Gtk, Mac), a `NAPS2.App.Worker` (separate worker process), console app and test projects (Lib.Tests, Sdk.Tests, App.Tests) — [repo](https://github.com/cyanfish/naps2)
- Bulk Crap Uninstaller has `ci.yaml` and `winget.yml` workflows plus a PrivacyPolicy.txt — [repo](https://github.com/Klocman/Bulk-Crap-Uninstaller)
- Shutter Encoder (Java) commits a `Library` folder and a prebuilt `Shutter Encoder.jar`. It has no GitHub workflows (the API returns 404) — [repo](https://github.com/paulpacifico/shutter-encoder)
- Converseen (C++/Qt, built on ImageMagick) has a GitHub Actions `windows-codesigned-release.yaml` along with AppImage, Snap and macOS workflows, and still has `appveyor.yml` — [workflows](https://github.com/Faster3ck/Converseen/tree/master/.github/workflows)
- DropPoint (Electron) is a cross-platform drag-and-drop helper for Windows 10+, macOS 12+ and Linux. It ships Playwright tests (`playwright.config.js`) and one `build.yml` — [repo](https://github.com/GameGodS3/DropPoint). AlternativeTo flags it as "Discontinued", but the repo was pushed in August 2026, so the two sources conflict — [AlternativeTo](https://alternativeto.net/software/dropover)

### Inferences
- Context-menu integration is the main reliability pitfall on Windows 11. File Converter's top issues are almost all about it. PowerToys' answer (sparse MSIX + `IExplorerCommand`, which requires a code-signing certificate, plus a legacy fallback) is the pattern to copy. Kumquat's drag-plus-key wheel avoids Explorer menu registration altogether, which is a real differentiator, but it should still ship a menu entry and a "Send to" entry as fallback entry points. The tangerine-windows clone already does the "Send to" part.
- The tangerine-windows clone shows that drag detection can work without global hooks (Win32 state + OLE drag clipboard). Its open problems (unsigned installer, dependency on an external FFmpeg install, Python packaging) are exactly where a polished Kumquat can win: bundled engines, signed MSIX or installer, and a native C#/WinUI or C++ stack.
- A shared architecture pattern recurs: a core engine library separate from the UI (HandBrake libhb, NAPS2 Sdk/Lib, ShareX *Lib, Flow Launcher Plugin SDK), with out-of-process workers (NAPS2.App.Worker) and CLI processes per engine (ConvertX, File Converter). Kumquat should keep an engine-adapter layer, one adapter per engine (FFmpeg, ImageMagick/libvips, Ghostscript/PDFium/qpdf, LibreOffice headless, Pandoc, Calibre, 7-Zip). Each adapter would declare a capability matrix (input format to output format) so the wheel can show only valid targets.
- License caution: FFmpeg builds with GPL components, Ghostscript (AGPL), ConvertX/VERT (AGPL) and File Converter (GPL-3.0) constrain a closed-source or paid Kumquat. Code can be studied but not copied unless Kumquat is GPL or AGPL-compatible. Prefer LGPL FFmpeg builds or permissive engines (libvips is LGPL, PDFium is BSD-style, qpdf is Apache-2.0) when targeting a proprietary license.

### Gaps
- I did not inspect individual issue trackers for HandBrake, ShareX, ImageGlass, Converseen, Shutter Encoder, FFmpeg Batch, Ueli or BCU (budget).
- I did not verify progress and cancellation implementations in code (for example, how File Converter parses ffmpeg stderr). This needs a code read.
- Telemetry practices other than PowerToys' (events.md, telemetry-pr-check) were not examined.
- I did not research Pandoc GUIs specifically.
- ImageGlass `.github/workflows` returned 404, so its CI could not be determined.

## 2. Radial and pie menu implementations, and lessons from HCI research

### Takeaway
Kando (TypeScript/Electron, actively maintained, v3.0.0 released 2026-09-23, Windows installer signed through the SignPath Foundation) is the reference radial-menu implementation on Windows. Its "Turbo Mode" (hold a modifier, release to select) is almost exactly Tangerine's interaction. The Kurtenbach and Buxton research gives hard design limits: about 8 items per level, at most 2 levels, with frequent items on the axes.

### Cited Findings
- Kando is "a cross-platform pie menu for your desktop" for Windows, macOS and Linux, supporting mouse, stylus, touch, controller and keyboard. It is by Simon Schneegans, whose earlier projects were Gnome-Pie and Fly-Pie. Code signing for Windows uses SignPath.io and a free SignPath Foundation certificate. Translations go through Weblate and the repo is REUSE-compliant — [README](https://github.com/kando-menu/kando)
- Kando's source is split into `main` (backends per OS, actions, settings, workflow-executor, menu-window, achievements), `menu-renderer`, `settings-renderer` and `common`. CI runs `checks.yml`, CodeQL and `deploy_to_winget.yml` — [src](https://github.com/kando-menu/kando/tree/main/src), [workflows](https://github.com/kando-menu/kando/tree/main/.github/workflows). The latest release is v3.0.0 (2026-09-23) — [releases](https://github.com/kando-menu/kando/releases)
- Kando's top issues are mostly about Linux and Wayland: compositor offsets, dbus crashes and multi-monitor on Hyprland. Windows-relevant ones are "Flashes black on start for a split second" (#423), "Shortcut not triggering kando menu" (#442) and custom icons (#383) — [issues](https://github.com/kando-menu/kando/issues?q=is%3Aissue+sort%3Acomments-desc)
- Kando interaction modes — [Kando usage guide](https://kando.menu/usage/):
  - Point and click: "anywhere in the item's wedge will do!"
  - Marking Mode: drag-gesture selection on a pause or sharp turn; sharp zig-zag strokes are recommended.
  - Turbo Mode: holding Ctrl, Shift, Alt or Meta acts as a held button, and releasing selects.
  - Hover Mode: fastest, but risks accidental selection.
  - Centered and Anchored modes for touch and gamepad.
  - Stated principles: muscle memory and "The menu reacts to your movements. Not the other way around!"
- Kurtenbach & Buxton, "The limits of expert performance using hierarchic marking menus", InterCHI '93, pp. 482–487 — [billbuxton.com](https://www.billbuxton.com/MMExpert.html), [Autodesk PDF](https://research.autodesk.com/app/uploads/2023/03/the-limits-of-expert.pdf_recyw5wL6ekPqcE4Q.pdf):
  - 12 subjects; breadths of 4, 8 and 12, and depths 1–4.
  - Error rates stayed below 10% up to breadth 8 at depth 2; 4-item menus stayed under 10% even at depth 4.
  - The pen (1.69 s) beat the mouse (2.07 s).
  - On-axis items were significantly faster and more accurate than off-axis items.
  - Recommendation: about 8 items per level, 2 levels, with frequent items on-axis.
- Kurtenbach & Buxton CHI '94 field study: marks were on average 3.5 times faster than menu selection, and experts still pop the menu up to refresh their memory. A third-party summary notes that the field study had only two users — [Autodesk publication](https://www-pt.autodesk.com/research/publications/user-learning-and-performance), [Berkeley summary](https://people.eecs.berkeley.edu/~fox/summaries/ui/marking_menus.html), [billbuxton.com](https://billbuxton.com/MMUserLearn.html)
- Pie menus select by direction rather than distance — [Wikipedia: Pie menu](https://en.wikipedia.org/wiki/Pie_menu)
- AutoHotPie is an MIT-licensed AutoHotkey-based radial menu for Windows. The author warns "there will be bugs". It was last pushed in February 2025 and has 108 open issues — [GitHub](https://github.com/dumbeau/AutoHotPie)

### Inferences
- Tangerine's 275 conversion options cannot fit in an 8-slice wheel. Kumquat should filter by source type (Tangerine already shows "only relevant formats", per the [press kit](https://tangerineformac.com/press)). It should cap the first ring at 8 items, use stable positions so muscle memory works, put the most common targets (JPG, PNG, PDF, MP4, MP3) on the cardinal axes, and push the rest into a "More" submenu, keeping depth at 2 or less.
- Because Tangerine opens a wheel during an OLE drag, selection happens on drop, which makes it a marking-menu-like gesture. Wedge-wide hit areas (Kando) make the targets effectively large, which helps in Fitts's-law terms.

### Gaps
- "Radial Menu v4" could not be found or confirmed, and no AutoHotkey project of that name was located.
- I found no published Kando design write-up that cites Fitts's law explicitly.
- I did not research Fitts's-law literature specific to pie menus (for example, Callahan et al. 1988).

## 3. Commercial competitors on Windows (drag-and-drop conversion, shelf apps, Permute equivalents) and the gap

### Takeaway
Windows has paid all-in-one converters (Wondershare UniConverter, Any Video Converter, Freemake), single-domain tools (XnConvert, Switch, Foxit), cloud converters (CloudConvert, Zamzar, Convertio) and a few shelf apps (Shelfit, Dropshelf, Tokri). None of the sources found offers a cursor-level, zero-click radial conversion, so Tangerine's interaction appears to be unoccupied on Windows apart from hobby clones.

### Cited Findings
- Prices from a vendor-authored roundup (How to Convert; conflict of interest) — [howtoconvert.co](https://howtoconvert.co/blog/best-file-converter-windows-apps):
  - Wondershare UniConverter: $29.99/year or $55.99 one-time.
  - Any Video Converter Ultimate: $49.95.
  - Freemake: $29.95/year or $59.95 lifetime (free version watermarked).
  - XnConvert: free for personal use, $27 commercial.
  - IrfanView: $12 commercial.
  - Switch Audio Converter: $29.99.
  - Foxit PDF Editor Pro: $149.99/year.
  - CloudConvert: 25 free conversions/day, from $9/month.
  - Zamzar: from $18/month (25 MB free).
  - PDF24 and Format Factory: free.
- How to Convert (the author's own product): $29 one-time for Mac, Windows and Linux on up to 5 devices, local processing. Its claimed "6,098 conversion pairs" contradicts "5,438" on the same page — [howtoconvert.co](https://howtoconvert.co/blog/best-permute-alternatives)
- Permute is Mac-only at $14.99, or available via Setapp — [howtoconvert.co](https://howtoconvert.co/blog/best-permute-alternatives), [App Store](https://apps.apple.com/app/id1444998321)
- Morphjet presents itself as an on-device drag-and-drop converter for Mac and Windows. Its pricing was unclear ("One-time, launching this July") — [morphjet.com](https://morphjet.com/alternatives/permute/)
- Convertio has paid plans from $14.99/month, and FreeConvert is free up to 1 GB per file — [search summary of moonb.io/guideflow](https://www.moonb.io/blog/convert-video-formats) (secondary; not verified on vendor pages)
- Shelf apps on Windows:
  - Shelfit is on the Microsoft Store. It notes that drag-and-drop "doesn't seem to work on some configurations of Windows 10" and that it is unsupported on Windows S. The price was not visible — [Microsoft Store](https://apps.microsoft.com/detail/9npz3rhk24wd?hl=en-US)
  - Dropshelf is listed as a Windows shelf — [dropshelf repo](https://redirect.github.com/williamckha/dropshelf-repo), [AlternativeTo](https://alternativeto.net/software/dropshelf-windows-)
  - Tokri is MIT-licensed and cross-platform — [AlternativeTo](https://alternativeto.net/software/dropover)
- File Converter (free, GPL) is the de facto free context-menu converter on Windows — [file-converter.io via README](https://github.com/Tichau/FileConverter)

### Inferences
- Price anchor: one-time prices of about $29–30 are common (Tangerine $29.99, How to Convert $29, Switch $29.99). Subscriptions dominate in video suites and cloud tools, so a one-time "local, no upload, no subscription" offer is a credible position.
- The gap is the interaction (a zero-click wheel at the cursor), breadth across domains in a single tool (PDF to Office plus video plus EPUB plus subtitles plus archives), and polish (signed installer, Windows 11-native look). Free File Converter already covers basic media and image conversion via the context menu, so Kumquat must beat it on UX, PDF/Office/EPUB conversions and reliability, not just on format count.

### Gaps
- I did not check Microsoft Store listings for converter apps (for example, "File Converter" store clones), so their ratings and pricing are unknown.
- I did not verify Shelfit and Dropshelf prices.
- Third-party prices come mostly from a vendor-authored page and should be re-verified on vendor sites.
- I found no Windows app with a Dropover-style shelf combined with conversion.

## 4. Tangerine itself: pricing, reviews, launch, features and complaints

### Takeaway
Tangerine is by Leap Studio (developer handle thameem / thmmhnsn). It publicly launched on 2026-09-07 for Apple-silicon Macs running macOS 13+. It is free for the first 20 files, then $29.99 one-time. Version 2.0 (October 2026) added an Actions wheel and Office/EPUB support. There is little independent discussion so far: one Product Hunt review, a viral X launch post, and a handful of App Store reviews.

### Cited Findings
- Counts: "275 conversion options, 26 advanced file tools, 6 file actions" (AirDrop, Float, Copy, Move, Copy Path, Terminal) — [tangerineformac.com](https://tangerineformac.com)
- Keys: Shift = formats, Command = tools, Control = actions. "The wheel stays open until you drop or cancel." Enter on a selected file opens the menu, arrow keys choose, Enter applies and Esc cancels. Keys are configurable, and there is touch support (hold briefly to convert) — [tangerineformac.com](https://tangerineformac.com)
  - The App Store description instead says Option+Shift opens the tools wheel. The sources conflict, so verify the key in the current version — [App Store](https://apps.apple.com/us/app/tangerine-file-converter/id6813034065?mt=12)
- Formats — [tangerineformac.com](https://tangerineformac.com):
  - Images: JPG, PNG, WebP, HEIC, TIFF, AVIF, BMP, PDF export; SVG input only.
  - Audio: MP3, M4A, WAV, FLAC, OGG, Opus, AIFF, WMA.
  - Video: MP4, MOV, MKV, WebM, AVI, WMV, GIF; MP3 export.
  - PDF to DOCX, Pages, PPTX, XLSX, JPG, PNG, EPUB, TXT and MD (300 DPI images).
  - Documents: DOCX, Pages, MD, TXT, EPUB; RTF/RTFD input; HTML export.
  - Slides: PPT/PPTX to PDF, JPG, PNG; PDF to PPTX.
  - Spreadsheets: XLSX, ODS, CSV, TSV; PDF export; XLS input only.
  - Subtitles: SRT, VTT; TXT export.
  - Archives: ZIP, TAR, GZIP; RAR input only.
- "Conversions and edits save a new copy beside the original", and "File actions work with folders and mixed selections" — [press kit](https://tangerineformac.com/press)
- Processing is local; files are "never uploaded" — [tangerineformac.com](https://tangerineformac.com)
- Developer: LEAP STUDIO. Size 97.6 MB. Requires macOS 13+ and M1 or later. Category: Utilities — [App Store](https://apps.apple.com/us/app/tangerine-file-converter/id6813034065?mt=12)
- Pricing: free to download, "free for your first 20 files", then "Tangerine Full Unlock" for $29.99 as a one-time in-app purchase — [App Store](https://apps.apple.com/us/app/tangerine-file-converter/id6813034065?mt=12). The press kit says $29.99 one-time — [press kit](https://tangerineformac.com/press). The website mentions a "conversion allowance" and says file actions don't count against it — [tangerineformac.com](https://tangerineformac.com)
- Launch: the press kit gives a public launch date of 7 September 2026 (press kit version 1.4) — [press kit](https://tangerineformac.com/press). The developer's X launch post ("convert files with ZERO clicks: just hold shift and drag the file") reportedly showed a 50% launch discount and about 117k video views — [X/@th2049](https://x.com/th2049/status/2097015553508725184) (figures via search snippet, not verified directly)
- Version history: 1.4 (Sep 24) fixed hidden conversion options. 2.0 added the Actions wheel, EPUB/Word/Pages/PowerPoint/spreadsheet support, a Float feature and percentage-based compression. 2.0.1 fixed hidden options again — [App Store](https://apps.apple.com/us/app/tangerine-file-converter/id6813034065?mt=12)
- Reviews — [App Store](https://apps.apple.com/us/app/tangerine-file-converter/id6813034065?mt=12):
  - There are not yet enough ratings to show an overall score.
  - Praise: HEIC to JPG was faster than finding a web converter; "Exactly what I needed!"
  - Request: PDF to EPUB was missing (the developer said it was coming; it is now listed on the site).
- Product Hunt ("Tangerine: Offline file modifier for Mac"): one review. It praises the Shift-drag and local processing and asks for better onboarding for the Option-key tools menu — [Product Hunt](https://www.producthunt.com/products/tangerine-zero-click-file-converter), [reviews](https://www.producthunt.com/products/tangerine-zero-click-file-converter/reviews)
- Third-party mentions: [Automatool](https://automatool.app/tools/tangerine), [whatships launch video](https://whatships.com/videos/tangerine/), [oneshotted.io](https://oneshotted.io/v/tangerine-convert-files-with-zero-clicks)
- Several clones appeared within weeks of launch: tangerine-windows (Python), OrbitDrop and Daisy (Swift) — [tangerine-windows](https://github.com/CristianO2601/tangerine-windows), [orbitdrop](https://github.com/nftstory/orbitdrop), [daisy](https://github.com/ishanmalu/daisy)

### Inferences
- The concept is easy to copy (three clones in about six weeks), so Kumquat's moat must be execution: reliable drag detection on Windows, a signed installer, bundled engines and fast cold start.
- The early complaints point to discoverability of the modifier keys (onboarding) and the bug of conversion options not showing (fixed twice). Kumquat should invest in an onboarding tutorial overlay and in a tested capability matrix, so that no valid option is hidden.
- The pricing model (a free trial by file count, then about $30 one-time) is a good reference for a Windows equivalent.

### Gaps
- No Hacker News or Reddit threads about Tangerine were found; independent discussion seems limited as of October 2026.
- No App Store star rating is available yet.
- The exact free allowance shown on the website could not be confirmed beyond the App Store's "first 20 files".
- Which engines Tangerine uses internally (for example, Apple frameworks vs bundled FFmpeg or LibreOffice) is not disclosed. The 97.6 MB app size suggests bundled engines, but this is unconfirmed.

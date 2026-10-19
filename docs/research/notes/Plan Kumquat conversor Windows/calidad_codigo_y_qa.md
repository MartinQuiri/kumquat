# Code quality, architecture and QA blueprint for Kumquat (Windows drag-and-drop converter, .NET)

Research date: 2026-10-18. Convention: links marked "(fetched)" were opened and checked in this session. Links marked "(canonical, not fetched)" point to official docs or repos for well-known tools; the writer should spot-check version numbers before publishing.

---

## 1. Platform baseline and architecture (converter registry, DI, MVVM, job queue, errors, logging, settings, localization)

### Takeaway
Target **.NET 10 LTS**, which is supported until Nov 14 2028. .NET 8 and .NET 9 both reach end of support on Nov 10 2026, so a new app shouldn't start on them. Build Kumquat as a Generic Host app (`Microsoft.Extensions.Hosting`) made of a UI-agnostic core: a converter registry modeled as a capability graph, a job engine built on `Channel<T>`, and Result-typed errors. A thin WPF or WinUI 3 shell uses CommunityToolkit.Mvvm. Engines plug in through interfaces registered in DI.

### Cited Findings
- .NET 8 (LTS) and .NET 9 (STS) both end support **November 10, 2026**. .NET 10 (LTS, released Nov 11 2025) is supported until **November 14, 2028**. .NET 11 is at RC2 (Oct 13 2026) with no GA date published yet. — [.NET support policy (fetched)](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
- Files (files-community), a top-tier OSS WinUI 3 file manager, targets `net10.0-windows10.0.26100.0` with a minimum OS of `10.0.19041.0`. It sets `LangVersion=preview`, sets `WarningsAsErrors=nullable` (nullable warnings fail the build) and `Features=strict`, sets `CsWinRTAotWarningLevel=3` for trimming/AOT diagnostics, and uses `EnableMSTestRunner=true` (Microsoft.Testing.Platform). — [Files Directory.Build.props (fetched)](https://raw.githubusercontent.com/files-community/Files/main/Directory.Build.props)
- PowerToys' root Directory.Build.props sets `Platforms=x64;ARM64`, `EnableNETAnalyzers=true`, `AnalysisMode=Recommended`, `TreatWarningsAsErrors=true` for .csproj, and `NuGetAuditMode=direct`. Non-test projects get StyleCop.Analyzers and Microsoft.CodeAnalysis.NetAnalyzers as PrivateAssets, plus a shared `GlobalSuppressions.cs` and `StyleCop.json`. Tests run on Microsoft.Testing.Platform (`EnableMSTestRunner`, `--report-trx`). An optional MSBuildCache is available. — [PowerToys Directory.Build.props (fetched)](https://raw.githubusercontent.com/microsoft/PowerToys/main/Directory.Build.props)
- Generic Host, DI, logging, configuration and options are the standard .NET building blocks and work in desktop apps too. — [.NET Generic Host (canonical, not fetched)](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host); [Dependency injection in .NET (canonical, not fetched)](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
- CommunityToolkit.Mvvm provides source generators (`[ObservableProperty]`, `[RelayCommand]`, async commands with cancellation) and `IMessenger`. It is the MVVM library used by Microsoft Store and many WinUI apps. — [MVVM Toolkit docs (canonical, not fetched)](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
- `System.Threading.Channels` offers bounded and unbounded producer/consumer queues with backpressure (`BoundedChannelFullMode`), so it fits a job queue with N workers. — [Channels docs (canonical, not fetched)](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels)
- Cancellation should flow from the UI through the job and on to the external process, using `CancellationToken` and `Process.WaitForExitAsync(token)`. — [Cancellation in managed threads (canonical, not fetched)](https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads)
- Atomic output: write to a temp file in the **same directory/volume** as the target, then `File.Move(tmp, final, overwrite: true)` or `File.Replace` so readers never see a half-written file. — [File.Replace (canonical, not fetched)](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.replace)
- Serilog plugs into Microsoft.Extensions.Logging via `Serilog.Extensions.Hosting`. The rolling file sink suits `%LOCALAPPDATA%\Kumquat\logs`. — [Serilog.Extensions.Hosting (canonical, not fetched)](https://github.com/serilog/serilog-extensions-hosting)
- Localization: WPF uses .resx + `IStringLocalizer`/ResourceManager. WinUI 3 uses .resw via `Microsoft.Windows.ApplicationModel.Resources` (MRT Core). — [Localize WinUI apps (canonical, not fetched)](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/localize-winui3-app)

### Inferences (recommended design, opinionated)
- **Solution layout** (clean/hexagonal):
  - `Kumquat.Core`: domain only, no Windows/UI references. Contains `FileFormat` (id, MIME, extensions, magic-byte signature), `ConversionEdge`, `IConverter`, `ConversionGraph`, `JobSpec`, `Result<T>`, and `Error` records.
  - `Kumquat.Engines.*`: one project per engine (FFmpeg, Vips, PDFium, Pandoc, LibreOffice, ImageMagick, ...). Each implements `IConverter` and exposes `IEnumerable<ConversionEdge> Capabilities`, with a cost and quality-loss weight per edge.
  - `Kumquat.Infrastructure`: process runner (Job Objects, ArgumentList), temp workspace, settings store, Serilog setup, update client.
  - `Kumquat.App`: WPF or WinUI 3 shell, tray, global hooks, radial overlay, ViewModels.
  - `Kumquat.Cli`: headless CLI on the same core. It enables integration tests and power users.
  - Tests: `tests/Kumquat.Core.Tests`, `tests/Kumquat.Engines.IntegrationTests`, `tests/Kumquat.UiTests`, `fuzz/Kumquat.Fuzz`.
- **Capability graph**: nodes are formats and edges are converter capabilities with weights (cost = time estimate + lossiness penalty). Multi-hop paths come from Dijkstra over the graph, with a hop limit (≤3) and a "lossy→lossless→lossy" penalty. Build the graph at startup from all `IConverter` registrations (`services.AddSingleton<IConverter, FfmpegConverter>()`, ...) and freeze it as an immutable structure. Unit-test path selection with property-based tests (see section 2).
- **Format detection** should use magic bytes rather than the extension. A format sniffer belongs in Core and gets fuzzed.
- **Job engine**: a bounded `Channel<ConversionJob>` read by `Environment.ProcessorCount/2` workers for CPU-bound engines. Give GPU-encode jobs (NVENC/QSV/AMF) a separate `SemaphoreSlim(1-2)`, since consumer GPUs cap concurrent encode sessions. Report progress via `IProgress<JobProgress>` by parsing `ffmpeg -progress pipe:1` key=value output. Each job gets a temp workspace `%TEMP%\Kumquat\{jobId}`, deleted in `finally`. On startup, sweep stale workspaces older than 24h.
- **Errors**: use Result types for *expected* failures (unsupported format, engine missing, engine exit code ≠ 0, user cancel) and exceptions only for bugs. Options include a small hand-rolled `Result<T, Error>` (recommended, zero dependencies), FluentResults, or ErrorOr. Map `Error` codes to localized resx strings in the UI.
- **Settings**: a JSON file in `%LOCALAPPDATA%\Kumquat\settings.json`, accessed through `IOptionsMonitor<KumquatSettings>` plus a writable wrapper. Use atomic write (temp + replace) and a schema version field with migrations. If MSIX is used, `ApplicationData` is redirected automatically (see section 5).
- **Localization**: author strings in English as the neutral resource with es (or es-CL/es-419) satellites. Add a CI check that fails when a key exists in en but not in es (a simple test that diffs resx keys). Use a pseudo-localization build for truncation testing.

### Gaps
- No public "reference architecture" for multi-hop converter graphs in .NET OSS was found. The design above is synthesized, and the graph/Dijkstra approach is an inference rather than a cited practice.
- WPF vs WinUI 3: this research didn't benchmark them. Files and PowerToys both use WinUI 3 for newer UI, but WPF still has the more mature tray/overlay story (notably the H.NotifyIcon library supports both). That claim was not fetched this session.

---

## 2. Testing strategy (unit, snapshot/golden, media assertions, property-based, fuzzing, corpus, integration, UI automation, mutation, coverage)

### Takeaway
Use **xUnit v3 (or MSTest) on Microsoft.Testing.Platform** with **Shouldly or AwesomeAssertions**, not FluentAssertions v8+, which needs a paid license for commercial use. Supporting layers:
- **Verify** for golden files, with Verify.ImageMagick for perceptual image/PDF comparison.
- **ffprobe-based assertions** for media.
- **CsCheck/FsCheck** for the graph planner.
- **SharpFuzz** (libFuzzer on Windows) for format sniffers and metadata parsers.
- **FlaUI** (UIA3) for UI smoke tests. WinAppDriver is effectively abandoned.

### Cited Findings
**Frameworks and assertion libraries**
- FluentAssertions: from **v8.0 (Feb 3 2025)** it is under the Xceed Community License, free only for non-commercial use, with a paid license required otherwise. **v7 stays Apache 2.0 indefinitely.** The latest listed version is 8.9.0 (Jan 26 2026). — [Xceed FA FAQ](https://xceed.com/?p=2556); [DevClass report](https://www.devclass.com/development/2025/01/16/another-open-source-project-shifts-to-restrictive-license-fluent-assertions-following-xceed-partnership/1621343); [ComponentSource releases](https://www.componentsource.com/es/product/xceed-fluent-assertions/releases)
- **AwesomeAssertions** is a community fork built from the Apache-2.0 code. It keeps the `FluentAssertions` namespaces (drop-in) and was at v9.x by 2026. Adoption of its newest releases looks modest on NuGet. — [AwesomeAssertions on NuGet](https://www.nuget.org/packages/awesomeassertions)
- Shouldly (BSD-licensed, fully OSS) is the other common replacement. — [Shouldly repo (canonical, not fetched)](https://github.com/shouldly/shouldly)
- xUnit v3 supports Microsoft.Testing.Platform. Stable 3.x uses MTP v1 integration (3.2.0 released 2025-11-02). A 4.0 line (prerelease `4.0.0-pre.128` installs `xunit.v3.mtp-v2`) moves to MTP v2. A commercial blog claims 4.0 shipped on 14 Aug 2026 with Native AOT test projects and MTP v1 dropped, but this was **not confirmed** against xunit.net. — [xUnit MTP docs](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform); [xunit.v3 4.0 pre on NuGet](https://www.nuget.org/packages/xunit.v3/4.0.0-pre.128); [xUnit 3.2.0 release notes](https://xunit.net/releases/v3/3.2.0); [eCorpIT blog (unverified)](https://ecorpit.com/xunit-v3-4-0-native-aot-parallelization-migration-2026/)
- PowerToys and Files both use **MSTest on Microsoft.Testing.Platform** (`EnableMSTestRunner`). — [PowerToys props (fetched)](https://raw.githubusercontent.com/microsoft/PowerToys/main/Directory.Build.props); [Files props (fetched)](https://raw.githubusercontent.com/files-community/Files/main/Directory.Build.props)

**Snapshot and golden-file testing**
- Verify.ImageMagick replaces Verify's binary comparison with ImageMagick-based comparers for PNG/JPG/BMP/TIFF, with a configurable `threshold`. It renders PDFs to per-page PNGs plus a page-count `.verified.txt`, and renders SVG to PNG. Setup is `VerifyImageMagick.Initialize(); VerifyImageMagick.RegisterComparers(threshold: 0.5);` in a `[ModuleInitializer]`. — [Verify.ImageMagick README (fetched)](https://github.com/VerifyTests/Verify.ImageMagick)
- Verify (core) stores `*.verified.*` files next to the tests and diffs `*.received.*` on failure. It has scrubbers for non-deterministic values such as dates and GUIDs. — [Verify repo (canonical, not fetched)](https://github.com/VerifyTests/Verify)

**Fuzzing**
- SharpFuzz brings AFL to .NET and needs .NET 8+. AFL runs on Linux/macOS and on Windows via WSL, and **libFuzzer is recommended for native Windows**. The harness is `Fuzzer.OutOfProcess.Run(...)`. It has found bugs in CoreFX, ImageSharp, Markdig, Json.NET and others, including CVE-2019-0980/0981 and CVE-2025-11573. — [SharpFuzz README (fetched)](https://github.com/Metalnem/sharpfuzz)

**UI automation**
- WinAppDriver: the last release (2021) needs .NET 5. Users on Microsoft Q&A describe it as abandoned, and it has more than 1,150 open issues. — [Microsoft Q&A "Is WinAppDriver dead"](https://learn.microsoft.com/en-au/answers/questions/1455246/is-the-tool-winappdriver-dead-or-not)
- The Appium Windows driver is a proxy in front of WinAppDriver, so it inherits WinAppDriver's stagnation. FlaUI.WebDriver is a W3C WebDriver built on FlaUI that supports UIA3 only and is labelled **experimental**. — [FlaUI.WebDriver README](https://github.com/FlaUI/FlaUI.WebDriver)
- FlaUI is a .NET library that wraps UI Automation (UIA2/UIA3) for Win32, WinForms, WPF and Store apps. — [FlaUI repo (canonical, not fetched)](https://github.com/FlaUI/FlaUI)
- PowerToys has a dedicated UI-test framework (`src/common/UITestAutomation`). A migration skill published from the PowerToys repo describes moving from WinAppDriver/Selenium to `Microsoft.PowerToys.UITest.Next`, built on `winappcli`, with no WinAppDriver server. That source is secondary and the timing is unverified. — [PowerToys UI tests docs](https://microsoft.github.io/PowerToys/development/ui-tests/); [PowerToys ui-tests-migration skill (secondary)](https://tessl.io/registry/skills/github/microsoft/PowerToys/ui-tests-migration)

**Other tools (canonical, not fetched)**
- Property-based testing: CsCheck (C#-first, fast shrinking) — [CsCheck](https://github.com/AnthonyLloyd/CsCheck); FsCheck — [FsCheck](https://github.com/fscheck/FsCheck)
- Mutation testing: Stryker.NET (`dotnet stryker`) — [Stryker.NET docs](https://stryker-mutator.io/docs/stryker-net/introduction/)
- Coverage: coverlet (collector/MSBuild), or Microsoft.Testing.Extensions.CodeCoverage on MTP; ReportGenerator for HTML; Codecov free for OSS — [coverlet](https://github.com/coverlet-coverage/coverlet); [ReportGenerator](https://github.com/danielpalme/ReportGenerator); [MTP code coverage](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-extensions-code-coverage)
- Accessibility automation: Axe.Windows (the engine behind Accessibility Insights) can run automated a11y scans in tests — [axe-windows](https://github.com/microsoft/axe-windows)

### Inferences (recommended test pyramid for Kumquat)
1. **Unit tests (fast, every PR)**: graph planner, format sniffer, ffmpeg progress parser, argument builders, settings migration, Result mapping, and the localization-keys parity test. Target ≥80% line coverage on `Kumquat.Core`. Don't chase a coverage number on UI.
2. **Property-based tests (CsCheck)**:
   - For any generated format graph, the planned path is valid (each edge exists), within the hop limit, and no costlier than any direct edge.
   - Argument builders never emit an argument that starts with `-` from a user filename, so a file named `-y.mp4` can't inject flags. Prefix with `./` or use `file:` for ffmpeg inputs.
   - Round-trip tests (e.g. PNG→WebP-lossless→PNG) keep pixels identical.
3. **Golden tests (Verify)**: for deterministic text outputs (Pandoc md→html, CSV→JSON), snapshot the output. For images and PDFs, use Verify.ImageMagick with a threshold. For lossy media, don't snapshot bytes. Instead assert:
   - ffprobe JSON (`ffprobe -v error -print_format json -show_streams -show_format`): codec, width/height, pixel format, duration within ±50 ms, audio sample rate/channels.
   - Quality metrics via ffmpeg filters: `-lavfi ssim` / `psnr` (or `libvmaf` if built in) against the source. Thresholds, e.g. SSIM ≥ 0.95 for "high quality" presets.
   - Determinism: pin engine versions in tests, because encoder updates change bytes.
4. **Integration tests** run the real engines from a pinned, hashed `tools/` bundle, using the CLI project with tests tagged `[Trait("Category","Integration")]`. They run nightly plus on PRs that touch `Engines/*`. Each one-hop edge in the registry gets at least one integration test, generated as data-driven theories from the capability list. This is the main defence for "hundreds of conversions".
5. **Test corpus**: keep small (<100 KB) synthetic fixtures in the repo, generated by scripts (`ffmpeg -f lavfi -i testsrc2=duration=2`, `sine`, ImageMagick `rose:`/`logo:`, Pandoc from markdown). Keep a larger corpus in Git LFS or a release asset with a SHA-256 manifest. Use only files with clear licenses. Candidates (not verified this session): the Pillow/libvips/FFmpeg FATE test suites, Mozilla pdf.js test PDFs, and public-domain Wikimedia Commons media. Record per-file license in `corpus/LICENSES.md`.
6. **Fuzzing**: SharpFuzz/libFuzzer harnesses for the format sniffer, any managed metadata parsers (EXIF, ID3, archive listing), and the Zip-Slip-safe extractor. Run them nightly or weekly in CI for N minutes and commit crashing inputs as regression tests. Native engines (FFmpeg, libvips, PDFium) are already fuzzed upstream via OSS-Fuzz, so Kumquat's job is to *isolate* them (section 6), not to fuzz them.
7. **UI tests (few, smoke only)**: FlaUI UIA3 runs on a Windows runner with an interactive session. Tests: tray icon menu opens, settings window keyboard navigation, overlay appears on hotkey, a converted file appears. Test drag-drop and global hooks mostly **below** the UI:
   - Abstract hooks behind `IGlobalInputSource` and inject synthetic events in unit tests.
   - For an end-to-end check, use `SendInput` from the test process, or FlaUI's `Mouse.Drag`, on a dedicated runner.
   - GitHub-hosted runners do have a desktop session, but hook/drag tests are flaky there, so quarantine them in a separate job with retries.
8. **Mutation testing**: run Stryker.NET weekly on `Kumquat.Core` only and track the mutation score, with a ≥60% floor at first. Running it on PRs is too slow.
9. **Coverage**: use the MTP code-coverage extension or coverlet, producing Cobertura output. ReportGenerator writes a summary into the GitHub job summary, and Codecov handles PR comments if the repo is public.

### Gaps
- The final release status of xUnit v3 4.0 (Aug 2026) was not confirmed from xunit.net.
- No authoritative, current list of legally redistributable media test corpora was verified this session.
- PowerToys' migration from WinAppDriver to `UITest.Next`/winappcli is supported only by a secondary source.
- No published guidance was found on reliably testing low-level keyboard/mouse hooks in GitHub-hosted runners.

---

## 3. Static analysis, style and dependency hygiene

### Takeaway
Copy the PowerToys/Files pattern:
- A root `Directory.Build.props` with `Nullable=enable`, `TreatWarningsAsErrors=true`, `EnableNETAnalyzers` and `AnalysisMode` set to Recommended (or All with selective downgrades).
- StyleCop.Analyzers plus an `.editorconfig`.
- Central Package Management with `NuGetAuditMode`.
- CodeQL and `dotnet format --verify-no-changes` in CI.
- Dependabot for NuGet and GitHub Actions.

### Cited Findings
- PowerToys: `TreatWarningsAsErrors=true`, `EnableNETAnalyzers=true`, `AnalysisMode=Recommended`, StyleCop.Analyzers plus the NetAnalyzers package, a shared GlobalSuppressions/StyleCop.json, and `NuGetAuditMode=direct`. — [PowerToys props (fetched)](https://raw.githubusercontent.com/microsoft/PowerToys/main/Directory.Build.props)
- Files: `WarningsAsErrors=nullable`, `Features=strict`, `LangVersion=preview`, and CsWinRT AOT warnings at level 3. — [Files props (fetched)](https://raw.githubusercontent.com/files-community/Files/main/Directory.Build.props)
- Code analysis configuration (`AnalysisLevel`, `AnalysisMode`, `<AnalysisLevel>latest-recommended</AnalysisLevel>`) and severity via `.editorconfig`/`.globalconfig`. — [Code analysis overview (canonical, not fetched)](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview)
- Central Package Management: `Directory.Packages.props` with `ManagePackageVersionsCentrally=true`. — [CPM docs (canonical, not fetched)](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management)
- NuGet Audit (vulnerability warnings NU1901–NU1904 at restore). — [NuGet Audit (canonical, not fetched)](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages)
- Banned API analyzer (`Microsoft.CodeAnalysis.BannedApiAnalyzers` + `BannedSymbols.txt`). — [BannedApiAnalyzers (canonical, not fetched)](https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.BannedApiAnalyzers/BannedApiAnalyzers.Help.md)
- Extra analyzers commonly used in quality OSS: Meziantou.Analyzer — [repo](https://github.com/meziantou/Meziantou.Analyzer); Roslynator — [repo](https://github.com/dotnet/roslynator); SonarAnalyzer.CSharp / SonarQube Cloud (free for public projects) — [SonarQube Cloud pricing](https://www.sonarsource.com/plans-and-pricing/); CodeQL for C# via `github/codeql-action` (free for public repos) — [CodeQL docs](https://docs.github.com/en/code-security/code-scanning/introduction-to-code-scanning/about-code-scanning-with-codeql) (all canonical, not fetched).

### Inferences: concrete config for Kumquat

`Directory.Build.props` (root):
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0-windows10.0.26100.0</TargetFramework>
    <SupportedOSPlatformVersion>10.0.19041.0</SupportedOSPlatformVersion>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);CS1591</NoWarn>
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild Condition="'$(CI)'=='true'">true</ContinuousIntegrationBuild>
    <NuGetAudit>true</NuGetAudit>
    <NuGetAuditMode>all</NuGetAuditMode>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
    <Platforms>x64;ARM64</Platforms>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="StyleCop.Analyzers" PrivateAssets="all" />
    <PackageReference Include="Meziantou.Analyzer" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" PrivateAssets="all" />
    <AdditionalFiles Include="$(MSBuildThisFileDirectory)BannedSymbols.txt" />
  </ItemGroup>
</Project>
```
- `BannedSymbols.txt` entries for Kumquat:
  - `P:System.Diagnostics.ProcessStartInfo.Arguments;Use ArgumentList`
  - `M:System.IO.Path.GetTempFileName;Use TempWorkspace`
  - `M:System.Diagnostics.Process.Start(System.String,System.String);Use ProcessRunner`
  - `T:System.Net.WebClient`
  - `M:System.Threading.Thread.Sleep(System.Int32)` in UI code
  - `M:System.IO.Compression.ZipFileExtensions.ExtractToDirectory...;Use SafeExtractor`
- `.editorconfig`: start from the dotnet/runtime one, then set `dotnet_diagnostic.CA2007.severity = none` (UI app, ConfigureAwait not needed in app code), `CA1303 = none` (resx handles localization), and keep CA2000/CA2213 (dispose) as warnings.
- CI gates:
  - `dotnet format --verify-no-changes`
  - build with warnings as errors
  - CodeQL (`build-mode: manual` or `autobuild` for C#)
  - Dependabot `nuget` + `github-actions` ecosystems, grouped weekly
  - Renovate is the alternative if you need to bump the native engine bundles via regex managers. Renovate can track FFmpeg/libvips/Pandoc release tags in a `tools.json` manifest, which Dependabot cannot.
- Skip Roslynator and Sonar at first, because they overlap heavily with StyleCop + Meziantou + NetAnalyzers. Add SonarQube Cloud only if the repo is public, where it's free.

### Gaps
- Current SonarQube Cloud free-tier terms in 2026 and the latest analyzer package versions weren't verified this session.

---

## 4. CI/CD on GitHub Actions, signing, SBOM, release automation

### Takeaway
Use one `windows-latest` workflow with an x64/ARM64 matrix: restore (locked mode), build, unit tests with coverage, `dotnet format`, CodeQL, then pack (Velopack), sign, generate an SBOM and attest provenance on tags. **Code signing is the hard constraint.** Microsoft's Artifact Signing (formerly Trusted Signing) only accepts **individual developers located in the US or Canada**, and organizations only from a short list of countries that doesn't include Latin America. A Chile-based solo developer therefore needs SignPath Foundation (if the project is OSS), a conventional OV certificate on a cloud HSM, or Microsoft Store distribution.

### Cited Findings
- Artifact Signing (renamed from Trusted Signing) has two tiers: **Basic, 5,000 signatures/month, 1 certificate profile of each type**, and **Premium, 100,000/month, 10 profiles**. Billing starts as soon as the account is created, isn't prorated, and needs a **paid** Azure subscription (free, trial and sponsored subscriptions aren't allowed). USD prices showed as placeholders on the fetched page. Historically the tiers were $9.99/month and $99.99/month, but that wasn't confirmed this session. — [Artifact Signing pricing (fetched)](https://azure.microsoft.com/en-us/pricing/details/artifact-signing/); [Artifact Signing FAQ (fetched)](https://learn.microsoft.com/en-us/azure/artifact-signing/faq)
- Eligibility: "Public Trust certificates are available to organizations in the United States, Canada, the European Union, the United Kingdom, Australia, New Zealand, Japan, South Korea, Singapore, Switzerland, Norway, and Israel. **Individual developers must be located in the United States or Canada.**" Individual validation requires an Azure billing account of type Individual whose name and address match a government ID, verified through AU10TIX and Microsoft Entra Verified ID. Organization validation takes 1–20 business days. — [Artifact Signing quickstart (fetched)](https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart)
- Artifact Signing issues **no EV certificates** and has no plans to. SmartScreen reputation "builds up automatically" per file hash with download history. The service signs anything SignTool supports and integrates with GitHub Actions, SignTool, PowerShell and an SDK. — [Artifact Signing FAQ (fetched)](https://learn.microsoft.com/en-us/azure/artifact-signing/faq); [SmartScreen reputation (canonical, not fetched)](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation)
- SignPath Foundation offers free code signing for OSS projects. The foundation's certificate is used (the foundation vouches that the binary was built from your repo), keys live in SignPath's HSM, and no personal ID is needed. — [SignPath Foundation (fetched)](https://signpath.org/)
- GitHub artifact attestations (`actions/attest-build-provenance`) and SBOM attestation are available for public repos. — [GitHub artifact attestations (canonical, not fetched)](https://docs.github.com/en/actions/security-for-github-actions/using-artifact-attestations/using-artifact-attestations-to-establish-provenance-for-builds)
- SBOM: `microsoft/sbom-tool` generates SPDX 2.2/3.0 for .NET outputs. CycloneDX has a .NET tool (`CycloneDX`). — [sbom-tool (canonical, not fetched)](https://github.com/microsoft/sbom-tool); [cyclonedx-dotnet (canonical, not fetched)](https://github.com/CycloneDX/cyclonedx-dotnet)
- release-please automates changelog, version bumps and GitHub Releases from Conventional Commits. — [release-please (canonical, not fetched)](https://github.com/googleapis/release-please); [Conventional Commits](https://www.conventionalcommits.org/)
- Reproducibility: `Deterministic`, `ContinuousIntegrationBuild`, Source Link (built in since .NET 8 SDK), and lock files with `--locked-mode`. — [Reproducible builds / ContinuousIntegrationBuild (canonical, not fetched)](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props#continuousintegrationbuild)

### Inferences: workflow sketch
```yaml
# .github/workflows/ci.yml
on: { push: { branches: [main] }, pull_request: {} }
permissions: { contents: read }
jobs:
  build:
    runs-on: windows-latest          # ARM64: windows-11-arm runner for tests; cross-compile otherwise
    strategy: { matrix: { rid: [win-x64, win-arm64] } }
    steps:
      - uses: actions/checkout@v4     # pin actions by SHA in practice
      - uses: actions/setup-dotnet@v4
        with: { global-json-file: global.json }
      - run: dotnet restore --locked-mode
      - run: dotnet format --verify-no-changes --no-restore
      - run: dotnet build -c Release --no-restore -p:RuntimeIdentifier=${{ matrix.rid }}
      - run: dotnet test -c Release --no-build -- --coverage --coverage-output-format cobertura --report-trx
      - uses: actions/upload-artifact@v4
  integration:   # nightly + engines/** paths; downloads hashed engine bundle
  codeql:        # github/codeql-action/init + analyze, language csharp
  release:       # on tag from release-please: vpk pack -> sign (SignPath/Artifact Signing/OV) -> sbom-tool -> attest -> gh release
```
- Pin `global.json` to the .NET 10 SDK feature band. Pin third-party actions by commit SHA and let Dependabot bump them.
- Verify engine binaries (ffmpeg.exe etc.) by SHA-256 against a committed manifest before packaging. Sign Kumquat's own binaries. Third-party engine exes keep their upstream signature or stay unsigned; still list them in the SBOM.
- **Signing decision for a solo developer outside the US and Canada**:
  1. If the repo is OSS, apply to SignPath Foundation (free).
  2. Otherwise, buy an OV certificate from a CA that offers cloud HSM signing (e.g., SSL.com eSigner, Certum), since CA/B Forum rules have required HSM-held keys since 2023.
  3. Alternatively, or in addition, publish through the Microsoft Store, which also accepts unpackaged Win32 EXE/MSI submissions. The Store signs MSIX itself, but EXE/MSI submissions must already be signed.
  - Re-check Artifact Signing's eligibility list, since it has expanded over time.

### Gaps
- Exact current USD prices for Artifact Signing weren't readable from the fetched page.
- SignPath Foundation's eligibility terms (OSI license, maintained project, build on CI) weren't fetched in detail.
- Microsoft Store fees for individual developers in 2026 weren't verified.
- GitHub-hosted Windows ARM64 runner availability for private repos in 2026 wasn't verified.

---

## 5. Installers, updates, and startup performance (MSIX vs Velopack vs Inno vs WiX; AOT/R2R/trimming)

### Takeaway
For a tray app with **global hooks** and **Explorer context-menu integration**, ship an unpackaged per-user install with **Velopack** (installer + delta auto-update from GitHub Releases). Use a **sparse MSIX package (package identity)** only if you want the modern Windows 11 top-level context menu (`IExplorerCommand`). Full MSIX blocks in-process shell extensions and other things a tray utility often needs. Squirrel.Windows is superseded by Velopack.

### Cited Findings
- MSIX restrictions relevant to Kumquat:
  - Modules loaded in-process into processes outside the package are not permitted, so **classic in-process shell extensions aren't supported**.
  - No writes to HKLM. HKCU and AppData are virtualized per app.
  - No writing to the install directory.
  - Apps must not always require elevation (the Store rejects them).
  - No per-user services.
  - The app should avoid launching utilities like cmd/PowerShell.
  - Plug-ins installed as separate packages are isolated from the host.
  — [Prepare to package a desktop app (MSIX) (fetched)](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-prepare)
- Velopack: `vpk pack --packId --packVersion --packDir --mainExe` produces `-full.nupkg`, a `-delta.nupkg` when a previous release exists, and `Setup.exe`. It supports hosting on GitHub Releases or any static file host, offers migration guides from Squirrel and ClickOnce, has SDKs for C#, Rust, JS, C++ and Python, and its main exe handles install/update hooks. The docs recommend code signing before distribution. — [Velopack docs (fetched)](https://docs.velopack.io/); [Velopack packaging overview (fetched)](https://docs.velopack.io/packaging/overview)
- Windows 11's modern context menu requires `IExplorerCommand` plus package identity. Unpackaged apps can get identity through a **sparse package / external location** registration. PowerToys (File Locksmith, PowerRename, Image Resizer) uses this approach. — [Grant package identity by packaging with external location (canonical, not fetched)](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/grant-identity-to-nonpackaged-apps)
- Native AOT is not supported for WPF. WinUI 3 supports AOT with CsWinRT trimming warnings (Files enables `CsWinRTAotWarningLevel=3`). ReadyToRun improves startup without AOT's restrictions. — [Native AOT deployment (canonical, not fetched)](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/); [ReadyToRun (canonical, not fetched)](https://learn.microsoft.com/en-us/dotnet/core/deploying/ready-to-run); [Files props (fetched)](https://raw.githubusercontent.com/files-community/Files/main/Directory.Build.props)

### Inferences: comparison for Kumquat
| Option | Fits tray + hooks? | Context menu | Auto-update | Notes |
|---|---|---|---|---|
| **Velopack** (recommended) | Yes, per-user, no admin | Classic registry verbs (HKCU) work; Win11 top-level menu needs a sparse package | Built in, delta, GitHub Releases | MIT-licensed OSS, active successor to Squirrel |
| Full MSIX | Hooks OK (`SetWindowsHookEx` LL hooks run in your own process), but in-process shell ext blocked; AppData virtualized | `IExplorerCommand` via manifest (good) | App Installer / Store | Clean uninstall; Store channel; constraints above |
| Inno Setup | Yes | Any, via registry | None built in (pair with your own updater) | Simple, mature, Pascal scripting |
| WiX 5/6 (MSI) | Yes | Any | None built in | Best for enterprise/GPO deployment; steep learning curve |
| Squirrel.Windows | n/a | n/a | n/a | Unmaintained; migrate to Velopack |

- Low-level hooks (`WH_KEYBOARD_LL`, `WH_MOUSE_LL`) are not DLL injection: the callback runs in the installing thread, so they don't conflict with MSIX's in-process rule. Hooks still can't observe elevated windows from a non-elevated process (UIPI). Document this limitation. (Inference from Win32 hook semantics; see [LowLevelKeyboardProc (canonical, not fetched)](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc).)
- Startup budget for the tray process: aim for under 300 ms to tray icon and under 60 MB working set idle. Enable ReadyToRun plus `TieredPGO` (default), and defer engine discovery and graph build to a background task after the tray icon shows. Keep the radial overlay window pre-created but hidden, so the first hotkey press is instant. Use Native AOT only if the shell is WinUI 3 and every dependency is trim-safe. Measure with `dotnet-counters`/`dotnet-trace` and a CI startup benchmark (launch → tray-ready log line).
- Recommended combination: Velopack for install and update, plus an optional sparse identity package registered at first run for the Win11 context menu. Classic HKCU `shell\Kumquat` verbs (visible under "Show more options") cover the fallback.

### Gaps
- Velopack's per-user install path, MSI output and signing integration details weren't confirmed from the fetched pages (the overview links to separate pages).
- No first-hand source confirms that Velopack and sparse packages coexist cleanly. This should be prototyped.

---

## 6. Security: external processes, malicious files, archives, resource limits, sandboxing, patching

### Takeaway
Treat every input file as hostile and every engine as exploitable:
- Launch engines with `ProcessStartInfo.ArgumentList`, `UseShellExecute=false` and no shell.
- Put each engine in a **Job Object** with memory/CPU/time caps and kill-on-close.
- Optionally run engines at **low integrity or in an AppContainer**, with a dedicated temp directory.
- Extract archives with Zip-Slip checks.
- Keep the native engine bundle on a tracked, hashed, auto-bumped manifest.

### Cited Findings
- `ArgumentList` escapes each argument and builds the command line internally. It is independent of `Arguments` and only one of the two may be used. Microsoft: "if you aren't sure how to properly escape your arguments, you should choose ArgumentList". The page also warns that using `ProcessStartInfo` with untrusted data is a security risk and points to OWASP input validation. — [ProcessStartInfo.ArgumentList (fetched)](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.argumentlist)
- MSIX guidance also says to avoid launching cmd.exe/PowerShell from apps. — [MSIX prepare (fetched)](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-prepare)
- Job Objects support `JOB_OBJECT_LIMIT_PROCESS_MEMORY`, `JOB_OBJECT_LIMIT_JOB_TIME`, CPU rate control and `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`, so child process trees die when Kumquat exits or cancels. — [Job Objects (canonical, not fetched)](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects); [JOBOBJECT_EXTENDED_LIMIT_INFORMATION (canonical, not fetched)](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-jobobject_extended_limit_information)
- Microsoft.Windows.CsWin32 generates P/Invoke for `CreateJobObject`, `AssignProcessToJobObject`, and similar APIs. — [CsWin32 (canonical, not fetched)](https://github.com/microsoft/CsWin32)
- AppContainer isolation for Win32 processes (`PROC_THREAD_ATTRIBUTE_SECURITY_CAPABILITIES`) and low-integrity processes. — [AppContainer isolation (canonical, not fetched)](https://learn.microsoft.com/en-us/windows/win32/secauthz/appcontainer-isolation); [Mandatory Integrity Control (canonical, not fetched)](https://learn.microsoft.com/en-us/windows/win32/secauthz/mandatory-integrity-control)
- Zip Slip: validate that `Path.GetFullPath(Path.Combine(dest, entry.FullName))` starts with the full destination path plus a separator. — [Snyk Zip Slip research (canonical, not fetched)](https://security.snyk.io/research/zip-slip-vulnerability); [OWASP File Upload Cheat Sheet (canonical, not fetched)](https://cheatsheetseries.owasp.org/cheatsheets/File_Upload_Cheat_Sheet.html)

### Inferences: Kumquat security controls
- **ProcessRunner** (the only allowed way to spawn engines; enforced by the banned-API analyzer):
  - Configuration: `UseShellExecute=false`, `CreateNoWindow=true`, an absolute engine path from the verified bundle (never PATH lookup), `ArgumentList` only, a working directory set to the job temp folder, and a minimal environment.
  - Read stdout/stderr asynchronously with size caps to avoid deadlocks and memory blowups.
  - Assign the process to a Job Object immediately (create suspended via CsWin32 `CreateProcess` if you need zero race), with a memory cap (e.g. 2–4 GB, configurable), a wall-clock timeout per job, and `KILL_ON_JOB_CLOSE`.
  - On cancel, terminate the job; never kill only the parent.
- **Engine-specific hardening**:
  - FFmpeg: `-nostdin -hide_banner -protocol_whitelist file,pipe`, so a malicious playlist can't fetch URLs. Pass inputs as `file:C:\...` so names starting with `-` or containing `:` aren't parsed as options or protocols.
  - LibreOffice headless: use a separate `-env:UserInstallation=file:///…` per job and disable macros.
  - Pandoc: `--sandbox`, which stops reading or writing files beyond the inputs.
  - ImageMagick (if used): a restrictive `policy.xml` (disable MVG/MSL/URL coders, resource limits).
  - Ghostscript: `-dSAFER`.
  - Verify these flags against each engine's docs. They come from general knowledge and weren't fetched this session.
- **Sandbox tiers**:
  - v1: Job Object plus a dedicated temp directory.
  - v2: run engines in a low-integrity process. It can only write to low-integrity-labelled directories, so give each job temp folder a low label.
  - v3: AppContainer with access only to the job folder. This is the strongest option but needs careful ACL brokering, so the main process copies input in and output out.
- **Archives**: a `SafeExtractor` with a Zip Slip check, entry-count and total-uncompressed-size limits (zip bomb), symlink rejection, and a max-nesting depth.
- **Patching**: a `tools/engines.json` manifest (name, version, URL, SHA-256, license), with Renovate custom regex managers bumping versions. CI downloads, verifies the hash and runs the integration suite. Subscribe to FFmpeg, libvips, PDFium (Chromium), LibreOffice and Pandoc security advisories, and ship engine updates via Velopack delta.
- **Output path safety**: never let engine output names come from file metadata. Normalize, strip reserved names (CON, NUL...), and handle long paths (`\\?\`) and collisions ("name (1).ext").

### Gaps
- No .NET OSS project was found (this session) that ships a reusable AppContainer launcher for third-party CLIs. The sandbox tiers are a design proposal.
- The engine-specific hardening flags weren't verified against upstream docs this session.

---

## 7. Observability: crash reporting, minidumps, telemetry, GDPR

### Takeaway
Use Sentry for .NET with **explicit opt-in**, PII scrubbing (`SendDefaultPii=false`, strip file paths and usernames in `BeforeSend`) and no file contents ever. Keep local Serilog logs plus a "Copy diagnostics" button. The free Developer plan covers 5k errors and 1 user, enough for early stages.

### Cited Findings
- Sentry Developer (free) plan: 1 user, 5k errors, 5M spans, 5 GB logs, 50 replays, 30-day retention, 1 GB attachments. The pricing page doesn't mention an OSS sponsorship program. — [Sentry pricing (fetched)](https://sentry.io/pricing/)
- Sentry .NET SDK options (`Dsn`, `SendDefaultPii`, `SetBeforeSend`, attachments) and WPF/WinUI integration. — [Sentry .NET docs (canonical, not fetched)](https://docs.sentry.io/platforms/dotnet/)
- Windows Error Reporting LocalDumps (registry `HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps`) or `DOTNET_DbgEnableMiniDump` for .NET crash dumps. — [Collecting user-mode dumps (canonical, not fetched)](https://learn.microsoft.com/en-us/windows/win32/wer/collecting-user-mode-dumps); [.NET dumps (canonical, not fetched)](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/collect-dumps-crash)
- GDPR: consent must be freely given, specific, informed and unambiguous (Art. 4(11), Art. 7), and pseudonymous identifiers are personal data. — [GDPR text (canonical, not fetched)](https://gdpr-info.eu/art-7-gdpr/)

### Inferences
- First-run dialog: "Send anonymous crash reports? (off by default)". Add a toggle in Settings and a privacy notice in the repo.
- In `BeforeSend`, replace user-profile paths with `%USERPROFILE%` and remove input/output file names, keeping only extensions and size buckets. Set `Release` to the semantic version and upload PDBs (or use Source Link) for symbolication. Hold attachments to logs only, scrubbed.
- Telemetry (feature usage): skip it in v1, or use an opt-in counter with no identifiers. Crash reports alone give most of the quality value.
- Engine failures (non-zero exit) are *expected* errors. Log them locally with the last N lines of stderr and offer "Report this conversion problem", which attaches ffprobe metadata but never the file.

### Gaps
- Sentry's OSS sponsorship terms for 2026 weren't verified. Alternatives such as self-hosted GlitchTip or Bugsnag weren't researched.

---

## 8. Performance: startup, memory, hardware acceleration, parallelism

### Takeaway
Keep the tray process tiny, with heavy work in child engine processes. Detect GPU encoders at runtime by probing `ffmpeg -encoders` and test-encoding a frame, and fall back to software. Use separate concurrency limits for CPU-bound jobs, GPU encodes and memory-hungry engines such as LibreOffice.

### Cited Findings
- FFmpeg hardware encoders exist for NVIDIA (NVENC), Intel (QSV) and AMD (AMF). Availability depends on the build and the driver. — [FFmpeg HWAccelIntro wiki (canonical, not fetched)](https://trac.ffmpeg.org/wiki/HWAccelIntro)
- ReadyToRun and Native AOT trade-offs for startup (see section 5 sources). — [ReadyToRun](https://learn.microsoft.com/en-us/dotnet/core/deploying/ready-to-run); [Native AOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
- Server/workstation GC and `GCHeapAffinitizeMask`/`GCConserveMemory` settings tune memory for desktop apps. — [GC config (canonical, not fetched)](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/garbage-collector)

### Inferences
- Concurrency defaults:
  - CPU pool: `max(1, cores/2)` because FFmpeg and libvips multithread internally.
  - GPU encode pool: 2, since consumer NVENC has historically capped concurrent sessions (verify per driver).
  - LibreOffice: 1, since a single soffice profile can't run in parallel; use per-job `UserInstallation` to go above 1.
- Benchmarks: BenchmarkDotNet for managed hot paths (graph planning, sniffing). A CI "startup smoke" job records time-to-tray and idle working set and fails on more than 20% regression.
- Idle memory: `<ConcurrentGarbageCollection>`, defaults are fine. Consider `GCConserveMemory` and trimming caches after each job. Unload the overlay's heavy visuals when hidden.

### Gaps
- No measured startup numbers for WPF vs WinUI 3 tray apps on .NET 10 were found. Current NVENC session limits weren't verified.

---

## 9. Process: branching, PR reviews, definition of done, accessibility

### Takeaway
Use trunk-based development with short-lived branches, squash-merge, and Conventional Commits feeding release-please. Protect `main` with required checks (build, unit, format, CodeQL). Use a PR template with a Definition-of-Done checklist that includes accessibility (keyboard, Narrator, Accessibility Insights FastPass) and es/en strings.

### Cited Findings
- Conventional Commits spec, and release-please, which derives semver bumps (`feat` = minor, `fix` = patch, `!` = major) and the changelog. — [Conventional Commits](https://www.conventionalcommits.org/); [release-please (canonical, not fetched)](https://github.com/googleapis/release-please)
- Accessibility Insights for Windows (FastPass and automated checks on the Axe.Windows engine) and Axe.Windows for automated a11y scans in tests. — [Accessibility Insights for Windows (canonical, not fetched)](https://accessibilityinsights.io/docs/windows/overview/); [axe-windows](https://github.com/microsoft/axe-windows)
- WPF/WinUI expose controls via UI Automation peers. Custom controls such as the radial menu need an `AutomationPeer` with proper names, roles and keyboard support. — [UI Automation for custom controls (canonical, not fetched)](https://learn.microsoft.com/en-us/windows/apps/design/accessibility/custom-automation-peers)
- PowerToys uses a dedicated UI-test framework and keeps module tests per utility, a model for a ~26-tool app. — [PowerToys UI tests docs](https://microsoft.github.io/PowerToys/development/ui-tests/)

### Inferences: Definition of Done (per PR)
- [ ] Builds with zero warnings (TreatWarningsAsErrors); `dotnet format` clean.
- [ ] Unit tests for new logic. New converter edge → at least one integration test plus one golden or ffprobe assertion.
- [ ] Cancellation honored (test with a pre-cancelled token and mid-run cancel). Temp files cleaned up. Output written atomically.
- [ ] Errors surfaced as `Result` with localized messages (en + es keys present; the parity test passes).
- [ ] No new process spawn outside `ProcessRunner`; no `Arguments` string concatenation.
- [ ] UI changes: keyboard-only path works, Narrator reads name and role, high-contrast theme checked, Accessibility Insights FastPass clean.
- [ ] Logs contain no file contents or full user paths at Information level.
- [ ] Conventional Commit title; CHANGELOG handled by release-please.
- Radial overlay accessibility: every slice must be reachable by arrow keys or Tab and expose `AutomationProperties.Name`. Offer a non-radial list fallback in settings ("Use list menu") for screen-reader users. Respect `SystemParameters.ClientAreaAnimation` and the reduce-motion setting.
- Branching: `main` is always releasable. Use `feat/*` and `fix/*` branches with squash merges. Releases are tags created by the release-please PR, with no long-lived release branches until a support line is needed. Solo developers should still open PRs so CI and CodeQL gate merges. CODEOWNERS is unnecessary until there are contributors.

### Gaps
- No source quantifying PR-review practices of PowerToys or Files (e.g., required reviewers) was fetched. Statements about their process beyond the build props are not verified.

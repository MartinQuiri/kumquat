# Kumquat: distribution, packaging and monetization (research as of 2026-10-18)

Scope: how to distribute and monetize "Kumquat", a C#/.NET Windows 10/11 tray utility. It uses global input hooks, a transparent overlay, Explorer context-menu integration, and bundled FFmpeg/native engines (100-500 MB). The developer is likely based in Chile.

Method note: the research budget was about 22 tool calls. Primary sources fetched in full: Microsoft Store Policies v7.20 (dated 2026-09-14), the Artifact Signing quickstart (updated 2026-10-08), and the Sep 2025 Windows blog post on free registration. Other claims rest on search-result summaries of secondary sources and are labelled. Items that could not be verified are listed under Gaps.

## 1. Microsoft Store for Win32/desktop apps in 2026 (accounts, fees, packaging, policies that shape design)

### Takeaway
Individuals can now publish to the Store for free worldwide, including Chile. Companies pay a one-time $99. Non-game apps may use their own commerce and keep 100% of that revenue; Store commerce costs 15% for apps. Kumquat has two Store routes. (a) MSIX with runFullTrust: Store licensing/IAP, StartupTask and a sparse/packaged IExplorerCommand context menu are all available. (b) An unpackaged signed EXE/MSI behind a versioned HTTPS URL: easier technically, but you need your own signing cert, a silent install, and your own payments. Current Store policy has no clause that bans global keyboard/mouse hooks. Clause 10.2.8 (no unsupported APIs or accessibility-API abuse to change the user's Windows experience without consent) and 10.2.3 (malware/PUA criteria) are the relevant risks.

### Cited Findings
**Accounts and fees**
- Individual developers can register on the Microsoft Store for free. The program is "globally available in nearly 200 markets", needs no credit card, and sign-up is at storedeveloper.microsoft.com. Announced Sep 10, 2025 — [Windows Developer Blog](https://blogs.windows.com/windowsdeveloper/2025/09/10/free-developer-registration-for-individual-developers-on-microsoft-store/); [AlternativeTo](https://alternativeto.net/news/2025/9/microsoft-finally-drops-microsoft-store-publishing-fee-for-individual-developers-globally)
- The individual fee was previously a one-time $19. Company accounts still pay a $99 one-time fee — [AlternativeTo summary](https://alternativeto.net/news/2025/9/microsoft-finally-drops-microsoft-store-publishing-fee-for-individual-developers-globally); [Yahoo Tech](https://tech.yahoo.com/apps/articles/microsoft-store-eliminates-registration-fees-153041024.html)
- The waiver was first announced at Build 2025 (May). Thurrott reported a June 2025 start, but global availability was confirmed only in September 2025 — [Thurrott](https://www.thurrott.com/?p=321175)
- Policy 10.14: "Company accounts must be used for organizations, businesses, and any person acting in relation to their trade or profession." A company account is also required "if a reasonable consumer would interpret your application or publisher name to be that of a business entity." For example, a publisher name like "Kumquat Software SpA" would push toward a company account — [Store Policies v7.20](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies)

**Revenue share**
- Since July 28, 2021, non-game apps that use their own or a third-party commerce system keep 100% of in-app revenue. Games are excluded. The Store commission is 15% for apps and 12% for games (from Aug 1, 2021) — [Game World Observer](https://gameworldobserver.com/2021/06/25/microsoft-to-allow-everyone-to-get-100-of-revenue-from-third-party-payment-systems-everyone-except-game-developers); [Windows Central](https://windowscentral.com/game-developers-wont-get-keep-100-revenue-some-app-makers); [PC Gamer](https://www.pcgamer.com/au/windows-store-devs-can-keep-100-of-their-revenue-unless-theyre-making-games)
- A 2025 report confirms the policy still applies: non-game developers using their own commerce keep 100% — [TechRadar](https://www.techradar.com/pro/microsoft-just-dropped-its-store-fees-for-windows-developers)
- Current policy text, 10.8.1: "Non-game products made available on PC devices may either use a secure third-party purchase API or the Microsoft Store in-product purchase API for in-app purchases of digital items." Games and Xbox titles must use Store IAP — [Store Policies v7.20](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies)
- 10.8.2: if a third-party processor collects card data, it must be PCI DSS compliant. If registration or payment is required at install, it must happen in the app's own experience. "After installation… users may be directed to a browser to complete registration or transactions" — [Store Policies v7.20](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies)
- 10.8.7: pricing, including sales and discounts, must meet certain fairness rules. The exact sub-bullets were not extracted — [Store Policies v7.20](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies)

**MSIX vs unpackaged EXE/MSI**
- 10.2.9: non-game products may submit an HTTPS download URL. The installer must be .msi or .exe, and the binary and all its PE files must be signed with a cert chaining to the Microsoft Trusted Root Program. The URL must be versioned and its binary must not change. Each update needs a new URL. The install must be silent (a UAC prompt is allowed). It must be a standalone installer, not a web/downloader stub, and PC-only — [Store Policies v7.20](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies)
- 10.4: 10.2.9 products "must not take an unreasonable amount of time to download and must not have an unreasonably low installation success rate". This matters for a 100-500 MB installer — [Store Policies v7.20](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies)
- 10.2.5: the "install and update only through the Store" rule applies to games and Xbox products only, not to non-game apps — [Store Policies v7.20](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies)
- Microsoft's FAQ calls MSIX the recommended format but accepts traditional EXE/MSI — [MS Learn FAQ](https://learn.microsoft.com/windows/apps/publish/faq/submit-your-app)
- Community Q&A (not official docs): EXE/MSI listings have no Store payment or pricing mechanism, so a third-party payment service is needed. "Microsoft billing mechanism is available only for MSIX apps" (2024 comment) — [MS Q&A 986498](https://learn.microsoft.com/en-us/answers/questions/986498/how-to-recieve-payments-for-a-msi-or-exe-microsoft); [MS Q&A 1031423](https://learn.microsoft.com/en-gb/answers/questions/1031423/how-or-when-do-i-set-price-of-my-application-on-th)
- At Build 2025, Microsoft improved Win32 support in the Store. Self-updating Win32 apps can now surface updates via the Store, and listings show "last updated" — [Neowin](https://www.neowin.net/news/microsoft-store-finally-gets-last-updated-for-apps-direct-win32-updates-and-more/); [Thurrott](https://www.thurrott.com/?p=321175)
- Package size: 25 GB maximum per .msix/.msixbundle for Win10/11. This comes from a mirror of the MS UWP docs and was not confirmed on learn.microsoft.com. Either way, 500 MB is far below the limit — [mirror of MS docs](https://dotnet.territoriali.olinfo.it/en-us/windows/uwp/publish/supported-languages)

**Policies that affect Kumquat's technical design**
- 10.2.8: "You are required to use supported methods and must obtain user consent to change any user's Windows settings… Unsupported methods include… use of accessibility APIs or undocumented or unsupported APIs in unsupported ways." — [Store Policies v7.20](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies)
- 10.2.3: no malware or PUA, per Microsoft's "Unwanted and Malicious Software" criteria. 10.2.2: no dynamic code that changes described functionality, such as downloading and running remote scripts. This matters if engines are downloaded on demand instead of bundled — [Store Policies v7.20](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies)
- 10.2.4: dependency on non-Microsoft drivers or NT services is generally not allowed. A user-mode tray app is fine — [Store Policies v7.20](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies)
- 10.2.7: the product must allow a clean uninstall — [Store Policies v7.20](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies)
- 10.6: declared capabilities must "legitimately relate to the functions of your product" — [Store Policies v7.20](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies)
- 10.1: metadata must describe functionality accurately, "including required or supported input devices" — [Store Policies v7.20](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies)
- runFullTrust: added automatically when an EXE is converted to MSIX. Partner Center asks for a justification, which is reviewed during certification. One April 2026 forum report describes a submission stuck at "Incomplete" after the justification was entered (anecdotal) — [MS Q&A](https://learn.microsoft.com/en-us/answers/questions/769263/cannot-submit-to-microsoft-store); [MS Q&A 2026](https://learn.microsoft.com/en-us/answers/a/12725901)
- StartupTask: declared as `desktop:Extension Category="windows.startupTask"` in the MSIX manifest, with an Executable path such as `VFS\App.exe` — [Advanced Installer](https://www.advancedinstaller.com/startup-programs-msix.html)
- Explorer context menu: packaged apps, and unpackaged Win32 apps that use a sparse package (package with external location), register an IExplorerCommand COM server plus a `windows.fileExplorerContextMenus` extension in the manifest. Microsoft advises keeping GetTitle/GetIcon/GetState fast — [MS Learn: integrate packaged app with File Explorer](https://learn.microsoft.com/en-my/WINDOWS/APPS/desktop/modernize/integrate-packaged-app-with-file-explorer)
- The sparse package must be signed or it will not appear in the menu. Build steps: write AppxManifest → MakeAppX → signtool → register via PackageManager. 7-Zip's community Explorer extension uses this pattern — [zabkat](https://www.zabkat.com/blog/win11-explorer-menu-package.htm); [Mp3tag forum](https://community.mp3tag.de/t/54629); [GitHub 7zipExplorerExtension](https://github.com/2xRon/7zipExplorerExtension)
- IExplorerCommand is "entirely incompatible" with legacy IContextMenu, per a commercial installer vendor — [SD Times / InstallAware](https://sdtimes.com/installaware/new-installaware-x15-with-context-menu-bridge-for-windows-11/)
- Windows.Services.Store is the recommended namespace for in-app products and supports the latest add-on types — [MS Learn](https://learn.microsoft.com/tr-tr/windows/uwp/monetize/enable-in-app-product-purchases)

### Inferences
- The recommended Store path is a full MSIX (Desktop Bridge, runFullTrust). Package identity then gives in one package: Windows 11 modern context-menu entries, StartupTask, file-type associations, Windows.Services.Store trial/IAP/licensing, and Store-managed updates. Unpackaged EXE/MSI listings get none of the Store commerce (per community answers) and need the developer's own signing cert.
- The Store listing doubles as a free signing and SmartScreen path. MSIX apps are re-signed by Microsoft, so no paid cert is needed for Store users. This matters because Chilean individuals cannot use Artifact Signing (see section 3).
- Global hooks: WH_KEYBOARD_LL/WH_MOUSE_LL are documented Win32 APIs. Run them from the full-trust process only for the "shake/drag gesture to show overlay" feature. Describe this in the listing and the certification notes, make it opt-in, and do not log keystrokes, so the app is not read as a keylogger (10.2.3/PUA). This is inference; no policy text names hooks.
- A Store-only feature set can coexist with a direct-download build. 10.2.5's Store-only rule does not apply to non-game apps.

### Gaps
- Current official wording of the App Developer Agreement revenue share (15%/12%, 0% with own commerce) on learn.microsoft.com was not fetched. The figures above come from press coverage (2021, reconfirmed 2025).
- Whether Windows.Services.Store works for unpackaged apps without identity was not confirmed. The common understanding is that it needs package identity, which means MSIX or a sparse package.
- Package size limits on official MS Learn, and any MSIX size or bandwidth concerns for a 500 MB package, were not confirmed.
- No Microsoft documentation was found on certification treatment of low-level keyboard/mouse hooks.
- File type associations (`windows.fileTypeAssociation`) and Windows 11 default-app rules were not researched in detail.

## 2. Direct distribution: installer, auto-update, payments/licensing, MoR/VAT, comparable pricing

### Takeaway
For direct sales: Velopack (installer, delta updates, one command from `dotnet publish`) plus a merchant of record (Paddle or Lemon Squeezy, both about 5% + $0.50). The MoR handles global VAT/GST, which matters a lot for a Chile-based solo developer. Comparable utilities sell for about €15-17 one-time (Tangerine €17 after 20 free actions; Permute $14.99), with free OSS competitors (File Converter, ShareX, PowerToys).

### Cited Findings
- Velopack is an "installation and auto-update framework for cross-platform .NET applications". It turns `dotnet publish` output into an installer, updates and delta packages with one command (vpk CLI plus NuGet package), and migrates apps from Squirrel.Windows/Clowd.Squirrel — [Velopack GitHub mirror](https://github.com/alexachso/velopack); [NuGet vpk](https://packages.nuget.org/packages/vpk)
- Paddle and Lemon Squeezy both list headline fees of 5% + $0.50 per transaction with no monthly fee, including global tax handling (comparison updated June 2026) — [apicalculators](https://apicalculators.com/compare/paddle-vs-lemonsqueezy); [Rework](https://resources.rework.com/tools/billing-revenue/paddle-vs-lemon-squeezy)
- Lemon Squeezy reportedly adds surcharges: +1.5% for international cards, +1.5% for PayPal, +0.5% for some subscriptions. A later analysis could not re-verify these — [Rework](https://resources.rework.com/tools/billing-revenue/paddle-vs-lemon-squeezy); [Dodo Payments blog](https://dodopayments.com/blogs/lemon-squeezy-alternatives)
- Stripe acquired Lemon Squeezy; sources disagree on whether that was late 2023 or 2024. Stripe's "Managed Payments" MoR product is built on it and charges 3.5% plus standard Stripe processing — [Rework](https://resources.rework.com/tools/billing-revenue/paddle-vs-lemon-squeezy); [fungies.io](https://fungies.io/paddle-review-2026.md)
- Lemon Squeezy has native license-key delivery and suits indie software. Paddle is aimed more at B2B SaaS — [Rework](https://resources.rework.com/tools/billing-revenue/paddle-vs-lemon-squeezy)
- Tangerine (Mac): free download with 20 free actions, then a one-time €17 for unlimited use. Requires macOS 13 and Apple Silicon (MacGeneration review, Oct 2, 2026) — [MacG](https://www.macg.co/logiciels/2026/10/tangerine-un-convertisseur-de-fichiers-toujours-portee-de-main-sur-mac-311381)
- Tangerine saves output as a new copy beside the original — [chatgate.ai listing](https://chatgate.ai/post/tangerine)
- Permute 4: $14.99 permanent license. Also available via Setapp ($9.99/month) and in a Downie + Permute bundle ($26.99). Free trial available — [Charlie Monroe site](https://site2021.charliemonroe.net/permute); [MacStories](https://www.macstories.net/sponsored/permute-quick-image-audio-and-video-conversion-sponsor/)
- HandBrake is completely free and open source — [thetestedhub](https://thetestedhub.com/guides/best-converter-for-mac/)

### Inferences
- A Chile-based individual selling to EU/US/UK consumers would otherwise have to register for and remit VAT in many jurisdictions. A MoR becomes the legal seller and removes that burden, which is the main reason to choose Paddle or Lemon Squeezy over raw Stripe + Keygen.sh. Stripe also does not offer direct merchant accounts to Chilean individuals in every configuration (unverified).
- Suggested price anchor: $12-19 USD one-time, with a free tier of N conversions per day/month, mirroring Tangerine's 20 actions. Free OSS competitors (File Converter, ShareX, PowerToys) cap what Windows users will pay, so polish (overlay UX, presets, speed) must justify the price.
- Velopack is a good fit for the direct channel. Ship the Store build as MSIX separately; Velopack does not produce MSIX, as far as I know (unverified).

### Gaps
- No primary-source 2026 pricing pages were fetched for Paddle, Lemon Squeezy, Gumroad, Keygen.sh or LicenseSpring. Paddle's acceptance of Chilean individual sellers was not verified.
- Dropover price, File Converter (Tichau, GPL/free) details, and Inno Setup comparison were not retrieved.
- Tangerine's official pricing page (tangerineformac.com) was not fetched; €17 / 20 actions come from one review.

## 3. SmartScreen reputation and code signing in 2026

### Takeaway
Azure Artifact Signing (formerly Trusted Signing) Public Trust is not available to a Chilean individual. Individuals must be in the US or Canada. Organizations qualify only in the US, Canada, EU, UK, Australia, NZ, Japan, South Korea, Singapore, Switzerland, Norway and Israel, so Chile is excluded either way. Since 2024, EV certificates no longer grant instant SmartScreen reputation, so an OV cert (or the Store's own signing) is the pragmatic choice. SignPath Foundation is free, but only for fully OSI-licensed projects with no proprietary components.

### Cited Findings
- Artifact Signing quickstart (page updated 2026-10-08): "Public Trust certificates are available to organizations in the United States, Canada, the European Union, the United Kingdom, Australia, New Zealand, Japan, South Korea, Singapore, Switzerland, Norway, and Israel. Individual developers must be located in the United States or Canada." The product name changed from "Trusted Signing" to "Artifact Signing" (URL /azure/artifact-signing/). Brazil South is among the regions hosting the service, but customer eligibility is set by the identity location rule — [MS Learn Artifact Signing quickstart](https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart)
- Individual validation requires an Azure billing account of type "Individual" whose name and address match a government ID. Validation runs through AU10TIX/Verified ID. There are Basic and Premium SKUs; prices were not in the doc. Identity validation takes 1-20 business days — [MS Learn quickstart](https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart)
- In April 2025 a Microsoft moderator said onboarding was limited to US/Canada organizations with 3+ years of history and that individual onboarding was paused. The current doc lists a broader country set — [MS Q&A](https://learn.microsoft.com/en-us/answers/questions/2243504/trusted-signing-for-other-countries)
- EV and OV are treated the same for SmartScreen. SSL.com says this has been true since March 2024; a jtel wiki ties it to Trusted Root Program changes around August 2024. The month is unconfirmed — [SSL.com](https://www.ssl.com/de/products/software-integrity/code-signing/ev/); [jtel wiki](https://wiki.jtel.de/x/A4DSC)
- ToDesktop: customers see "Windows protected your PC" despite valid EV certs, and OV is now the better buy — [ToDesktop](https://www.todesktop.com/blog/posts/windows-apps-psa-ev-certs-do-not-grant-immediate-reputation-anymore)
- Microsoft's SmartScreen page says every new build starts with zero reputation — [MS SmartScreen reputation page (mirror)](https://star-hk2.eastasia.cloudapp.azure.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation)
- EV is still required for kernel-mode drivers, which Kumquat does not need — [SSL.com](https://www.ssl.com/de/products/software-integrity/code-signing/ev/)
- SignPath Foundation: free code-signing certs for OSS. Requires an OSI-approved license, no proprietary components, active maintenance, and fully automated builds with the key in an HSM. The cert is issued to "SignPath Foundation" as publisher — [signpath.org](https://signpath.org/); [SignPath OSS](https://signpath.io/product/open-source); [Super Productivity example](https://super-productivity.com/code-signing/)
- 10.2.9 requires Store-submitted EXE/MSI to be signed with a Trusted Root Program CA cert — [Store Policies v7.20](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies)

### Inferences
- For a Chilean individual, the options in order of cost: (1) MSIX via Store, where Microsoft signs and there is no SmartScreen issue for Store installs. (2) A commercial OV cert from a CA that validates Chilean individuals (Certum, SSL.com, Sectigo resellers). It must sit on a hardware token or cloud HSM under the 2023 CA/B Forum key-storage rules. (3) Form a company in an eligible country (e.g., a US LLC) to use Artifact Signing, which costs about $10/month on the Basic tier (not verified in this pass).
- If Kumquat keeps a proprietary paid core, SignPath Foundation is out. It works only for a fully OSS edition.

### Gaps
- Artifact Signing Basic/Premium prices were not captured; commonly cited figures are $9.99 and $99.99/month, unverified here.
- OV certificate prices for 2026 (Certum, SSL.com, Sectigo) and CA support for Chilean individual validation were not researched.
- No Microsoft primary announcement of the EV/SmartScreen change was found.

## 4. winget, Chocolatey, Scoop

### Takeaway
winget lists apps by submitting a YAML manifest PR to microsoft/winget-pkgs. Automated validation runs, with possible manual review. This suits a direct-download installer, and Store apps can also be installed via winget's msstore source. Chocolatey and Scoop were not researched.

### Cited Findings
- Submit a manifest to https://github.com/microsoft/winget-pkgs. Automated checks run and manual review may follow. Run `winget validate` and test in Windows Sandbox before opening the PR — [MS Learn: submit manifest](https://learn.microsoft.com/vi-vn/windows/package-manager/package/repository)
- Forks of the repo describe a one-manifest-per-PR convention (secondary source) — [winget-pkgs fork](https://github.com/baochenw/winget-pkgs)
- ImageMagick auto-publishes each release to winget via Advanced Installer, with its installer signed through Trusted Signing — [Advanced Installer case study](https://www.advancedinstaller.com/imagemagick-latest-release-automatically-published-to-winget.html)

### Inferences
- An unsigned installer on winget would still hit SmartScreen and Defender warnings; signing is effectively needed. Automate manifest updates in CI (e.g., wingetcreate or komac) on each Velopack release.

### Gaps
- Official winget signing requirements, and the Chocolatey moderation and Scoop bucket processes, were not researched in this pass.

## 5. Business model options and how OSS apps handle Store + GitHub

### Takeaway
The Store explicitly allows the same developer to sell a paid Store version of an app that is free elsewhere. This policy was briefly restricted, then clarified in July 2022. Files (MIT, free on GitHub, about $8.99 on the Store) is the reference example. For a solo developer, the best fit is freemium with a conversion allowance (Tangerine's model) plus a one-time unlock, sold both via Store IAP (MSIX) and via a MoR on the website.

### Cited Findings
- Microsoft first barred selling otherwise-free open-source apps in the Store (2022) to curb scam copies, then reversed it. Giorgio Sardo: "we removed the previous mention to open source pricing." A paid Store version is allowed when the same developer offers a free version elsewhere. Third-party copies can be reported — [Winaero (updated Jul 19, 2022)](https://winaero.com/microsoft-store-again-allows-selling-open-source-apps/?amp)
- Files (Explorer replacement, MIT license) is free from its site and GitHub. A paid Store version costs $8.99 in the US / €14.99 in France — [ElevenForum](https://www.elevenforum.com/t/files-app-not-free.11517); [AlternativeTo](https://www.alternativeto.net/software/files-uwp/about/); [JustGeek](https://www.justgeek.fr/?p=145694)
- Paint.NET is cited as an app sold on the Store to fund development — [Winaero](https://winaero.com/microsoft-store-again-allows-selling-open-source-apps/?amp)
- Tangerine uses freemium with 20 free actions, then a one-time unlock — [MacG](https://www.macg.co/logiciels/2026/10/tangerine-un-convertisseur-de-fichiers-toujours-portee-de-main-sur-mac-311381)
- Policy 11.2: all content and metadata must be original or licensed. IP owners can report infringement — [Store Policies v7.20](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies)

### Inferences
- Solo-developer recommendation: a closed-source core with freemium (e.g., 20-30 free conversions per month, or unlimited basic image conversion with advanced video/batch/presets paid) and a one-time "Pro" unlock of about $15. Optionally offer a paid major-version upgrade every 2-3 years. Subscriptions are a poor fit for an offline converter: there are no server costs, and users resent subscriptions for local utilities.
- Dual channel: Store MSIX with a durable add-on via Windows.Services.Store (15% fee, Microsoft handles VAT and refunds), or your own MoR inside the Store app (0% Store fee, MoR ~5%+$0.50). Plus a website build via Velopack + Paddle/Lemon Squeezy license keys.
- Open-core (GPL'd core on GitHub, paid Store build) builds trust and SignPath eligibility, but invites free forks given ShareX- and File Converter-style competition. That is viable only if the moat is UX and brand, not code.

### Gaps
- No primary sources were found for how ShareX, EarTrumpet, Notepads or ModernFlyouts structure their Store listings or revenue. Indie post-mortems with Store vs direct revenue splits were not found within budget.

## 6. Legal and privacy: privacy policy, GDPR, trademark, FOSS (FFmpeg) compliance

### Takeaway
A privacy policy URL is mandatory in Partner Center if the app collects any personal information; crash reporting or telemetry counts. FFmpeg must be an LGPL-only build (no --enable-gpl/nonfree, so no libx264/libx265 inside), dynamically linked, with source offer and notices. Store agreement terms put FOSS compliance on the developer. Trademark clearance of "Kumquat" and avoiding Tangerine's trade dress were not researched against registries.

### Cited Findings
- 10.5.1: "If your product accesses, collects or transmits Personal Information, or if otherwise required by law, you must maintain a privacy policy" and enter its URL in Partner Center. It must describe the data collected, its use, storage and security, the parties it is disclosed to, user controls and access, and be kept up to date — [Store Policies v7.20](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies)
- 10.5: highly sensitive data only if related to functionality, and with express consent — [Store Policies v7.20](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies)
- FFmpeg is LGPL 2.1+, but optional GPL parts make the whole build GPL. FFmpeg's checklist: compile without --enable-gpl/--enable-nonfree, use dynamic linking, distribute or offer source, and credit FFmpeg in the about box and license text. Not legal advice — [FFmpeg legal](https://www.ffmpeg.org/legal.html)
- The older Microsoft Application Provider Agreement (quoted 2016) says that if the app includes FOSS, "you are responsible for compliance with all applicable FOSS license terms, including any source code availability requirements" — [GNOME vala-list quoting MS agreement](https://lists.gnome.org/archives/vala-list/2016-July/msg00013.html)
- The App Store vs LGPL debate (iOS) is about whether users can relink/replace libraries. A commenter on the FFmpeg tracker considers LGPL compliance in app stores simple if relinking stays possible — [FFmpeg trac #1229](https://trac.ffmpeg.org/ticket/1229); [ffmpeg-trac archive](https://ffmpeg.org/pipermail/ffmpeg-trac/2012-April/006331.html)
- Closed-source commercial apps can sell with LGPL FFmpeg when dynamically linked — [WIP discussion](https://wip.co/posts/ffmpeg-license-can-we-legally-sell-closed-source-apps-that-use-it-ap32zk)

### Inferences
- Windows desktop apps can ship FFmpeg as separate DLLs or ffmpeg.exe invoked as a separate process, which makes LGPL compliance much easier than on iOS. In an MSIX the package folder is read-only, but users can still obtain the source and run their own build. Shipping ffmpeg.exe as a separate process (not linked) is the cleanest approach.
- Avoiding GPL encoders means no libx264/libx265 inside the bundle. Options: Windows Media Foundation hardware H.264/HEVC encoders, OpenH264 (Cisco binary license), or libaom/SVT-AV1/libvpx, which use BSD-style licenses. Patent licensing for H.264/HEVC/AAC is a separate concern.
- Include an in-app "Open-source licenses" screen listing FFmpeg (LGPL text, version, configure flags, source link) and any other engines (ImageMagick, libvips, pandoc (GPL, so run it as a separate process), Ghostscript (AGPL; avoid it or isolate it carefully)).
- GDPR: crash reporting such as Sentry collects IP addresses and device identifiers, which are personal data. Make it opt-in, disclose it in the privacy policy, and sign a DPA with the processor. Chile's Law 21.719 (2024, in force Dec 2026) also matters. These points are inference and not sourced in this pass.
- Trademark: use your own name, icon, colors and UI. Do not copy Tangerine's orange citrus visual style, wording or layout; 11.2 lets IP owners file complaints. A citrus-fruit name may invite comparison claims. Search "Kumquat" in USPTO, EUIPO TMview, WIPO Global Brand Database and INAPI Chile for Class 9 conflicts before launch.

### Gaps
- No trademark registry searches for "Kumquat" (Class 9/42) were performed. No evidence was found on whether Tangerine's developer has registered marks.
- The current (2026) Microsoft App Developer Agreement FOSS clause was not fetched; the quote is from the 2016 version.
- GDPR crash-reporting guidance and Chilean data-protection law (Ley 21.719) were not researched with sources.

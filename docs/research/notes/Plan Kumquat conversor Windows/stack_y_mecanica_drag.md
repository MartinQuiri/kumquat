# Kumquat: Windows stack and core drag + radial-wheel mechanics

Research date: 2026-10-18. Research budget: about 23 tool calls. Many claims were confirmed in primary docs (Microsoft Learn). Some were not confirmed by a source; those are listed under Inferences or Gaps.

Reference behavior (Tangerine for Mac): you drag a file and tap a modifier key while dragging. Shift opens the Convert wheel, Command the Tools wheel and Control the Actions wheel. You can release the key and keep dragging, and the wheel stays open until you drop or cancel. The keyboard path is: select a file, press Enter, switch wheels with Cmd/Ctrl, choose with the arrow keys, press Enter to apply or Esc to cancel. The modifier keys can be changed in Settings. On touch screens, you hold briefly before dragging. Features: 275 conversions, 26 tools and 6 actions, all processed locally. Requires an Apple silicon Mac on macOS 13 or later — [tangerineformac.com](https://tangerineformac.com)

## Q1. Stack comparison (WPF vs WinUI 3 vs Avalonia vs Tauri 2 vs Electron vs native C++)

### Takeaway
For this app, the hard parts are a global hook, an OLE drop target on a topmost transparent window, and a window that appears mid-drag. Each of these is a thin Win32 layer, and every stack has to P/Invoke or FFI down to it. The deciding factors are therefore how much the UI framework gets in the way of transparency and drop-target registration, and memory/startup. My recommendation: a C#/.NET (8 LTS or 10 LTS) app whose wheel overlay is a raw Win32 layered HWND. The overlay is drawn with Direct2D/Composition, or with WPF if Native AOT is not needed. WinUI 3 is a weaker fit for the overlay because of its transparency limits. Tauri/Electron work, but they have documented WebView2 drop-target pitfalls.

### Cited Findings
- **WPF cannot be trimmed or Native-AOT compiled.** Microsoft says "Almost no WPF apps are runnable after trimming, so trimming support for WPF is currently disabled in the .NET SDK." Windows Forms trimming is disabled too, because WinForms depends on built-in COM marshalling. A ComWrappers-based community workaround exists for WinForms (WinFormsComInterop) — [MS Learn: trimming incompatibilities](https://learn.microsoft.com/lt-lt/dotnet/core/deploying/trimming/incompatibilities); [WinFormsComInterop README](https://github.com/kant2002/WinFormsComInterop/blob/main/README.md)
- **Avalonia XPF (the commercial WPF-compatible stack) supports Native AOT** because it does not use COM marshalling — [Avalonia XPF docs](https://docs.avaloniaui.net/xpf/deployment/native-aot)
- **.NET 9 shipped Native AOT support for WinUI 3** ("for smaller and more performant apps") — [Thurrott on .NET 9](https://www.thurrott.com/?p=312941); [Visual Studio Magazine](https://visualstudiomagazine.com/articles/2024/09/11/microsofts-stalled-uwp-project-supports-net-9-as-winui-3-migration-urged.aspx)
- **WinUI 3 transparency is limited.**
  - AcrylicBrush is in-app only and does not show the desktop behind the window. See-through effects must go through Window.SystemBackdrop or SystemBackdropElement — [MS Learn: Materials](https://learn.microsoft.com/en-my/windows/apps/develop/ui/materials)
  - An April 2026 DevBlogs post (WinUI 3 Gallery 2.9) describes making the window background fully transparent and drawing Acrylic/Mica only on the parts of the UI that need it, using SystemBackdropElement — [DevBlogs, Apr 30 2026](https://devblogs.microsoft.com/ifdef-windows/announcing-winui-3-gallery-2-9/)
  - Microsoft's Reactor docs say "The WinUI XAML compositor does not compose cleanly with classic WS_EX_LAYERED rendering". They leave true layered overlays to raw HWND interop and warn that WS_EX_LAYERED can bypass the WinUI composition path — [microsoft-ui-reactor windowing-advanced](https://microsoft.github.io/microsoft-ui-reactor/latest/windowing-advanced/)
  - SwapChainPanel, WebView2, MediaPlayerElement and Mica/Acrylic count as "external content" that does not support transparency — [MS Learn: Visual layer](https://learn.microsoft.com/sv-se/windows/apps/windows-app-sdk/composition)
  - Community reports say XAML transparency alone can still render black in a top-level WinUI 3 window, so a custom SystemBackdrop and DWM tweaks are needed (unofficial) — [linux.do guide](https://linux.do/t/1790806); [MS Q&A: transparent WinUI 3 window](https://learn.microsoft.com/en-sg/answers/questions/1463839/how-to-make-winui-3-window-transparent)
- **WPF AllowsTransparency=true creates a layered window, which has historically cost performance.** WPF renders with DirectX, reads the surface back with GetDC and presents it with GDI, which causes video-to-system-to-video memory blits. An old benchmark (GeForce 6800) measured about 8 fps when resizing on the hardware path versus about 60 fps in software. The same source says results depend on the GPU and memory model. These sources date from the Vista/Win7 era — [Seema Ramchandani (MS) blog](https://learn.microsoft.com/fr-fr/archive/blogs/seema/layered-windows-sw-is-sometimes-faster-than-hw); [Dwayne Need: Transparent Windows in WPF](https://learn.microsoft.com/fr-fr/archive/blogs/dwayneneed/transparent-windows-in-wpf)
- **Avalonia transparency on Windows.**
  - It requires Background=Transparent. Windows can suppress it, for example in battery saver. ActualTransparencyLevel reports what was actually applied, and TransparencyLevelHint takes a fallback list — [Avalonia Windows guide](https://docs.avaloniaui.net/docs/platform-specific-guides/windows); [TransparencyLevelHint API](https://api-docs.avaloniaui.net/docs/P_Avalonia_Controls_TopLevel_TransparencyLevelHint)
  - In Avalonia 11, transparent areas still receive clicks and WPF-style click-through did not carry over — [Avalonia discussion #11911](https://github.com/AvaloniaUI/Avalonia/discussions/11911)
- **Tauri 2 / WebView2 pitfalls for exactly this pattern** (a transparent topmost drop shelf on Windows):
  - WebView2 registers its OLE drop target (RegisterDragDrop/IDropTarget) only when the window is first shown and composited by DWM. If the first show() happens mid-drag, drops fail silently. The fix: declare the window `visible: true` in tauri.conf.json, parked at x=-10000.
  - Tauri issue #13761: `dragDropEnabled` is ignored when set in the Rust WebviewWindowBuilder, so the window must be declared in config.
  - Calling `set_focus()` mid-drag "interrupts Explorer's DoDragDrop loop" and breaks the drop.
  - hide()/show() strips Acrylic, and re-applying it flickers, so the window is "hidden" by moving it to x=-32000.
  - A click-through window (`set_ignore_cursor_events`) does not receive OS drag-enter.
  - Release binary went from 17 MB to 5.6 MB. The idle memory target was under 30 MB (a goal, not a measurement).
  - Source for all of the above: [dev.to: transparent drag-and-drop shelf with Tauri](https://dev.to/devrayat000/building-a-transparent-drag-and-drop-shelf-for-windows-with-tauri-the-engineering-war-stories-3c8c)
- **More Tauri findings.**
  - Native drag handling gives absolute paths. `dragDropEnabled: false` gives HTML5 DOM drag events but loses the file paths — [react-dropzone Tauri guide](https://react-dropzone.js.org/guide/tauri/)
  - wry 0.52.1 fixed "DragDropEvent::Drop::paths returns random data on Windows" (PR #1573) — [wry 0.52.1 release notes](https://v2.tauri.app/release/wry/v0.52.1)
  - Paths arrive on enter and again on drop, so cache them on enter — [tauri issue #9823](https://redirect.github.com/tauri-apps/tauri/issues/9823)
- **Electron prior art (Edge-Drop shelf).** Electron 34+ with React 18 and koffi for Win32 calls, and PowerShell for parsing HDROP. It uses a frameless, transparent, always-on-top BrowserWindow that is fully click-through when collapsed (setIgnoreMouseEvents) and polls the cursor position. During a drag it lowers its always-on-top level to 'normal' so other apps can receive the drop — [Edge-Drop wiki](https://github.com/Deepender25/Edge-Drop/wiki)

### Inferences
- Every candidate stack ends up calling the same Win32 APIs: SetWindowsHookEx, RegisterDragDrop, layered windows, Shell_NotifyIcon. C# (CsWin32 source generator / P/Invoke) and Rust (`windows` crate) both handle these well. C++ is the most direct, but slows UI and iteration. Electron adds the most memory (a Chromium process tree) and needs native addons (koffi/ffi) for hooks.
- **Recommended architecture: two layers.**
  - (a) A small native-style "interaction host". It owns the LL mouse/keyboard hooks on a dedicated thread with a message loop. It also owns a pre-created, topmost, WS_EX_NOACTIVATE + WS_EX_TOOLWINDOW layered HWND for the wheel, registered with RegisterDragDrop at startup and parked off-screen.
  - (b) A UI for settings and history, which can be WPF, WinUI 3 or anything else.
  - This avoids WinUI 3's layered-window conflict and WebView2's late registration of its drop target.
- If Native AOT (fast startup, small footprint for a tray-resident app) is a priority, use C# with WinUI 3, or with raw Win32 + Direct2D/Composition for the overlay. WPF rules out AOT and trimming.
- ARM64: .NET 8+, Windows App SDK, Tauri/Rust and Electron all ship ARM64 Windows builds (general knowledge; no 2026 source was checked in this pass).

### Gaps
- No measured 2026 benchmarks of startup time or idle memory for WPF vs WinUI 3 AOT vs Avalonia vs Tauri vs Electron for a tray app.
- Per-monitor DPI v2 behavior of layered windows in each stack was not checked.
- I did not find a specific microsoft-ui-xaml GitHub issue on drag-and-drop into WinUI 3 transparent windows.

## Q2. Detecting a system-wide OLE drag that starts in Explorer or another app

### Takeaway
Windows has no official API that announces "a drag started somewhere". DoDragDrop runs a modal loop inside the source process. Practical approaches:
- A WH_MOUSE_LL hook (or polling) watching left-button-down plus movement past the drag threshold.
- Watching for the shell's `SysDragImage` window through UI Automation or WinEvents (undocumented).
- Probing with a pre-registered drop target: once the wheel window is under the cursor and gets DragEnter, the drag is confirmed to be a real OLE drag, and the IDataObject shows what is being dragged.

### Cited Findings
- **The loop runs inside the source.** A source calls DoDragDrop, which "enters a loop" that determines the window under the cursor, checks whether it is a valid drop target, and calls IDropTarget methods — [MS Learn: DoDragDrop](https://learn.microsoft.com/en-us/windows/desktop/api/ole2/nf-ole2-dodragdrop)
- **Microsoft Q&A thread (2025), "How to Detect System-Wide Drag & Drop Start".**
  - RLWA32 says there is no built-in Windows support for globally intercepting drag-and-drop. He suggested WinEvents and UIA drag events, but Castorix31 tested them with AccEvent and saw `EVENT_SYSTEM_CAPTURESTART` and no drag/drop events.
  - MVP gekka posted C# code that subscribes to `Automation.AddStructureChangedEventHandler(RootElement, TreeScope.Children, ...)` and looks for a child window of class `SysDragImage` (the shell's drag-image window) to detect the start of a drag. This is undocumented and could break.
  - The suggested UI is a borderless, transparent window registered with RegisterDragDrop.
  - Source: [MS Q&A 2020151](https://learn.microsoft.com/en-us/answers/a/2020151)
- **Polling with a screen-edge trigger (Tauri shelf).** One Rust thread polls every 50 ms with `GetCursorPos` + `GetAsyncKeyState(VK_LBUTTON)`. It opens the shelf when the left button is held and the cursor is within 30 px of the right screen edge. It installs no system-wide hooks — [dev.to Tauri shelf](https://dev.to/devrayat000/building-a-transparent-drag-and-drop-shelf-for-windows-with-tauri-the-engineering-war-stories-3c8c)
- **Hover polling with hysteresis (Electron Edge-Drop).** It polls the cursor, with longer intervals on battery. Each monitor has a hot zone, with a 140 ms dwell threshold at monitor seams. Edge hover is suppressed in fullscreen apps via `SHQueryUserNotificationState` + `GetForegroundWindow` — [Edge-Drop wiki](https://github.com/Deepender25/Edge-Drop/wiki)
- **A Delphi developer** whose standard detection missed Outlook attachment drags (these are OLE drags) was looking at a global mouse hook instead — [Delphi-PRAXiS thread 6251](https://en.delphipraxis.net/topic/6251-detecting-start-of-drag-operations)
- **WH_MOUSE_LL rules.**
  - The callback runs on the installing thread, by message, so that thread needs a message loop.
  - It must return within `LowLevelHooksTimeout` (HKCU\Control Panel\Desktop). On Windows 7 and later, a hook that times out is silently removed and the app cannot detect it. On Windows 10 1709 and later, the maximum is 1000 ms.
  - Microsoft recommends a dedicated hook thread that hands work off immediately, and suggests Raw Input as the better choice for most monitoring.
  - Source: [MS Learn: LowLevelMouseProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelmouseproc)
- **The taskbar already acts as a drop-activation target during drags** (RLWA32's point about prior art) — [MS Q&A 2020151](https://learn.microsoft.com/en-us/answers/a/2020151)

### Inferences
- **Recommended detection pipeline for Kumquat:**
  1. A WH_MOUSE_LL hook (or Raw Input) sees WM_LBUTTONDOWN, then movement beyond SM_CXDRAG/SM_CYDRAG while the button is held. Mark this as a candidate drag.
  2. Optionally confirm with the `SysDragImage` window watcher, or with GetCursorInfo cursor changes.
  3. On the trigger key, or dwell, move the pre-registered wheel window under the cursor.
  4. The wheel's IDropTarget::DragEnter is the definitive confirmation. If no DragEnter arrives within roughly 100–200 ms, the drag was a window drag or text selection, so hide the wheel.
- Raw Input (WM_INPUT with RIDEV_INPUTSINK) avoids the risk of the hook being silently removed, but it cannot block keys. Use Raw Input for mouse tracking, and a LL keyboard hook only if keys must be swallowed.

### Gaps
- I could not fetch the DropPoint (GuddyTech) source or Dropshelf internals to see how they detect drags. AlternativeTo lists DropPoint 1.2.1 (Sept 2022) as discontinued, GPL-3.0 and cross-platform — [AlternativeTo DropPoint](https://alternativeto.net/software/droppoint)
- I did not verify whether PowerToys modules use WH_MOUSE_LL for drag detection. The FancyZones docs describe Shift-during-drag behavior only. Related bug reports (Shift alone triggering zones and blocking other keypresses; zones not activating until the mouse moves after Shift) fit a hook-driven implementation, but this is unconfirmed — [FancyZones docs](https://learn.microsoft.com/windows/powertoys/fancyzones); [PowerToys #3244](https://github.com/microsoft/PowerToys/issues/3244); [PowerToys #29560](https://github.com/microsoft/PowerToys/issues/29560)

## Q3. A window that appears mid-drag under the cursor and receives the drop

### Takeaway
DoDragDrop re-hit-tests the window under the cursor on every loop iteration. A window that becomes visible under the cursor mid-drag can therefore become the target on the next mouse move, provided that:
- its drop target is already registered;
- it is hit-testable (not WS_EX_TRANSPARENT, not click-through, not alpha 0 at the hit pixel);
- showing it does not steal activation.

### Cited Findings
- **Re-evaluation each loop.** DoDragDrop "determines the window under the current cursor location. It then checks to see if this window is a valid drop target", repeated inside its loop — [MS Learn: DoDragDrop](https://msdn2.microsoft.com/en-us/library/ms678486.aspx)
- **Late registration and focus pitfalls (WebView2).** Its drop target is registered only after the window is first shown and composited. A window first shown mid-drag fails silently. Focusing mid-drag (`set_focus`) breaks Explorer's DoDragDrop loop. A click-through window never gets OS drag-enter — [dev.to Tauri shelf](https://dev.to/devrayat000/building-a-transparent-drag-and-drop-shelf-for-windows-with-tauri-the-engineering-war-stories-3c8c)
- **WS_EX_TRANSPARENT is not simple.** Raymond Chen ("Like the cake, WS_EX_TRANSPARENT is a lie") says hit-testing functions differ in how they treat WS_EX_TRANSPARENT, and suggests layered windows instead — [Old New Thing 2012-12-17](https://devblogs.microsoft.com/oldnewthing/20121217-00/?p=5823)
- **Layered-window hit-testing.** It follows per-pixel alpha, so zero-alpha pixels pass mouse input through. WS_EX_LAYERED|WS_EX_TRANSPARENT makes the whole window pass-through. One anecdotal tip: keep alpha at 1 or above so WindowFromPoint still hits the window — [cnblogs](https://www.cnblogs.com/xnzzj/p/4524085.html); [CodeProject thread](https://www.codeproject.com/Messages/3466346/System-Time-Zone)
- **Z-order trick (Edge-Drop).** It lowers its always-on-top level during drags so other apps can receive the drop — [Edge-Drop wiki](https://github.com/Deepender25/Edge-Drop/wiki)

### Inferences
- **Recommended implementation:**
  1. Create the wheel HWND at startup with WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW. Do not set WS_EX_TRANSPARENT.
  2. Call OleInitialize and RegisterDragDrop once at creation.
  3. Keep the window visible and composited but parked off-screen.
  4. To show it, call SetWindowPos(HWND_TOPMOST, x, y, ..., SWP_NOACTIVATE | SWP_SHOWWINDOW) centered on the cursor.
  5. Paint per-pixel alpha with UpdateLayeredWindow, using a minimum alpha of 1 across the whole wheel disc. The wheel then hit-tests over its entire area, including "transparent" gaps.
- **Sector hit-testing:** in IDropTarget::DragOver(grfKeyState, POINTL pt, pdwEffect), `pt` is in screen coordinates. Convert it to polar coordinates relative to the wheel center, using the angle to pick a sector and the radius to pick the center or cancel zone. Set *pdwEffect = DROPEFFECT_COPY for valid sectors and DROPEFFECT_NONE for the dead center. Use IDropTargetHelper (CLSID_DragDropHelper) DragEnter/DragOver/Drop so the shell drag image keeps rendering over the wheel.
- Because Kumquat is the drop target, it decides *pdwEffect regardless of grfKeyState. Shift/Ctrl/Alt held over the wheel therefore do not cause a move or link. Always return COPY or LINK semantics, and never MOVE, so Explorer does not delete the source after an optimized move.
- Pause in DragOver is limited to the source's modal loop. Do the heavy conversion asynchronously after Drop returns (see Q6, IDataObjectAsyncCapability).

### Gaps
- I found no explicit Microsoft statement guaranteeing that a newly shown window becomes the target without a mouse move. Test whether a synthetic 1 px SetCursorPos nudge is needed.
- I did not test whether WS_EX_NOACTIVATE has any side effect on OLE drop targeting. I found no source saying it interferes.

## Q4. Modifier-key conflicts and safe trigger alternatives

### Takeaway
On Windows, Shift, Ctrl and Alt are part of the drag protocol: they change the drop effect, and the source reads them in QueryContinueDrag/DragOver via grfKeyState. Copying Tangerine's "tap Shift/Cmd/Ctrl" is risky in two ways. The tap changes the drag feedback over other targets. And if Kumquat swallows the key in a LL hook, the source never sees it. The safest choices:
- a non-modifier key (CapsLock is reasonable, but the toggle state needs restoring; or a configurable key);
- a right-mouse-button click during the left drag;
- dwell or a "shake" gesture as an alternative trigger.

### Cited Findings
- **Key-state bitmask during drag:** 1 = left button, 2 = right, 4 = Shift, 8 = Ctrl, 16 = middle, 32 = Alt. QueryContinueDrag is raised periodically and can Continue, Cancel or Drop. By default it drops when the mouse button is released, and Esc cancels — [MS Learn: QueryContinueDragEventArgs.KeyState](https://learn.microsoft.com/dotnet/api/system.windows.forms.querycontinuedrageventargs.keystate); [MS Learn: DragEventArgs.KeyState](https://learn.microsoft.com/dotnet/api/system.windows.forms.drageventargs.keystate)
- **LL hooks can block input.** If the hook procedure processed the message, it "may return a nonzero value to prevent the system from passing the message to the rest of the hook chain or the target window procedure" (stated for the mouse hook; the keyboard hook works the same way) — [MS Learn: LowLevelMouseProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelmouseproc)
- **Tangerine's own design:** Shift = Convert, Cmd = Tools, Ctrl = Actions. Keys are remappable in Settings, and you can release the key after the wheel appears — [tangerineformac.com](https://tangerineformac.com)
- **macOS precedent for drag + dwell.** Spring-loaded folders open on hover during a drag. The delay is user-adjustable, and pressing Space opens immediately — [Engadget Mac 101](https://www.engadget.com/2007-06-26-mac-101-spring-loaded-folders.html). An Apple patent (US 6,307,545) describes a "split pie" target during drag, where each half of the target triggers a different action — [USPTO 6307545](https://image-ppubs.uspto.gov/dirsearch-public/print/downloadPdf/6307545)
- **FancyZones' Shift-during-drag trigger has had bugs:** Shift alone showing zones and blocking other keys, and zones not activating until the mouse moves after Shift — [PowerToys #29560](https://github.com/microsoft/PowerToys/issues/29560); [PowerToys #23897](https://github.com/microsoft/PowerToys/issues/23897)

### Inferences
- Tapping Shift, Ctrl or Alt **without swallowing it** is mostly harmless if the wheel appears instantly under the cursor, because Kumquat (the target) then decides the effect. The only side effect is a brief cursor-badge change over the original target. Alt is the worst choice: an Alt tap can activate menu mnemonics in the source/foreground app after the drag ends. Ctrl+Shift together means "create shortcut".
- Swallowing a modifier in WH_KEYBOARD_LL means the key never reaches the system key state. The source's GetKeyState/grfKeyState never shows it, so the drag continues unaffected. The key-up must be swallowed too, or the system sees an unpaired event. (Inference from hook semantics; not verified by testing.)
- **Recommended default:** a remappable trigger, defaulting to Shift (Tangerine parity) with pass-through, plus alternatives:
  - right-click during the left drag (the hook sees WM_RBUTTONDOWN while VK_LBUTTON is down; swallow it so the source does not get a right-drag);
  - a dwell of about 400–600 ms with low velocity;
  - shake to open.
- Press 1/2/3, or Tab/Shift+Tab, while the wheel is shown to switch between the Convert/Tools/Actions wheels.

### Gaps
- I found no peer-reviewed study comparing keypress-during-drag with dwell for drag-time menus. Relevant prior work (Kurtenbach & Buxton marking menus, CHI/UIST hover-delay studies) was not retrieved.
- I did not test whether Explorer's DoDragDrop loop reacts badly to a swallowed modifier.

## Q5. UIPI and elevation

### Takeaway
UIPI blocks drag-and-drop from a lower-integrity process (Explorer, medium) to a higher one (an elevated app). Kumquat should run non-elevated, at the same level as Explorer, so it can receive drops from Explorer. Drags that start in elevated apps will not reach Kumquat, and that case should be documented, not worked around. ChangeWindowsMessageFilterEx is not a safe fix for drag-and-drop.

### Cited Findings
- **UIPI** blocks drag-and-drop from medium-integrity Explorer to high-integrity (elevated) processes by filtering the window messages drag-and-drop needs. The recommendation is to drag only between equal integrity levels, and Microsoft engineers advise against ChangeWindowsMessageFilterEx for drag-and-drop — [MS blog (Patrick): Why doesn't drag-and-drop work when my application is running elevated](https://learn.microsoft.com/pl-pl/archive/blogs/patricka/q-why-doesnt-drag-and-drop-work-when-my-application-is-running-elevated-a-mandatory-integrity-control-and-uipi); [MS Q&A 2007362](https://learn.microsoft.com/en-au/answers/questions/2007362/drag-and-drop-widget-is-not-not-working-for-win32)
- **XAML-islands case:** a Win32 app hosting XAML islands crashed on in-app drag when run as administrator (no confirmed resolution) — [MS Q&A 2007362](https://learn.microsoft.com/en-au/answers/questions/2007362/drag-and-drop-widget-is-not-not-working-for-win32)
- **Workaround:** a non-elevated main process plus a separate elevated helper for admin-only work — [elevenforum thread](https://www.elevenforum.com/t/drag-and-drop-while-running-as-admin.16916/post-326106)

### Inferences
- A non-elevated LL hook still receives input events when an elevated window is in the foreground (UIPI restricts message injection, not global LL hook notifications, in general understanding). But Kumquat cannot drop onto, or receive drops from, elevated windows. If the source is elevated, detect it (OpenProcess + token elevation check on the window under the cursor at drag start) and suppress the wheel.

### Gaps
- I found no authoritative 2026 source on exactly how WH_KEYBOARD_LL/WH_MOUSE_LL behave when the foreground window is elevated. Test this.

## Q6. Getting file paths out of the IDataObject

### Takeaway
Handle CF_HDROP for real files. Use CFSTR_FILEDESCRIPTORW + CFSTR_FILECONTENTS (IStream) for virtual files from Outlook, zip folders, FTP/namespace extensions and some browsers: materialize them to a temp folder, then convert. SHCreateShellItemArrayFromDataObject is the high-level route.

### Cited Findings
- **CF_HDROP route:** IDataObject::GetData(CF_HDROP, TYMED_HGLOBAL), then DragQueryFile(0xFFFFFFFF) for the count and DragQueryFile(i) for each path, then ReleaseStgMedium — [MS Learn: Handling Shell Data Transfer Scenarios](https://learn.microsoft.com/en-us/windows/win32/shell/datascenarios)
- **Virtual folders and namespace extensions** usually do not provide CF_HDROP. They use CFSTR_FILEDESCRIPTOR (FILEGROUPDESCRIPTOR + FILEDESCRIPTOR[]) and CFSTR_FILECONTENTS with lindex = file index, via TYMED_HGLOBAL | ISTREAM | ISTORAGE. Use nFileSizeLow/High for the exact size. Microsoft's examples include FTP, web, CAB and Windows CE folders — [same doc](https://learn.microsoft.com/en-us/windows/win32/shell/datascenarios)
- **Shell-item route:** targets can call SHCreateShellItemArrayFromDataObject to work with the shell item model. Sources should offer CFSTR_SHELLIDLIST — [same doc](https://learn.microsoft.com/en-us/windows/win32/shell/datascenarios)
- **Optimized move:** if the source offers DROPEFFECT_MOVE and the target returns MOVE, the source may delete the original. Targets report the outcome with CFSTR_PERFORMEDDROPEFFECT — [same doc](https://learn.microsoft.com/en-us/windows/win32/shell/datascenarios)
- **Async extraction:** IDataObjectAsyncCapability (formerly IAsyncOperation) lets the target return from Drop immediately and extract on a background thread (GetAsyncMode, StartOperation, then EndOperation). This avoids blocking Explorer — [same doc](https://learn.microsoft.com/en-us/windows/win32/shell/datascenarios)
- **Format order:** enumerate formats with EnumFormatEtc and pick the first usable one. Do not assume a particular format is present — [same doc](https://learn.microsoft.com/en-us/windows/win32/shell/datascenarios)

### Inferences
- In DragEnter, decide which sectors to show by the dragged file types (extensions from CF_HDROP or from the FILEDESCRIPTOR names). The wheel can then show only the relevant target formats, as Tangerine does.
- Browsers commonly offer CF_HDROP for files the user downloaded, and FILEDESCRIPTOR or a URL for images dragged from a page. Support CFSTR_INETURLW as a fallback that downloads locally. That is still "local processing", but it is a network fetch, so flag it.

### Gaps
- I did not verify exactly which formats Outlook (classic vs new), Chrome/Edge and Windows 11 zip folders offer in 2026. Inspect them with a clipboard/drag format viewer during prototyping.

## Q7. Keyboard-accessible alternative paths

### Takeaway
Provide three paths:
1. A global hotkey that acts on the current Explorer selection.
2. A Windows 11 modern context-menu entry via IExplorerCommand in a sparse MSIX package, plus a classic shell extension or Send To entry for Windows 10 and "Show more options".
3. Arrow-key and Enter navigation inside the wheel (Tangerine parity).

### Cited Findings
- **PowerToys pattern:** Windows 11 context-menu handlers are packaged as sparse MSIX packages exposing IExplorerCommand. They are registered for all file types and filtered in code, and the packages must be signed. ImageResizer uses dual registration (classic and Win11), which produces duplicate entries in the expanded menu. Registration happens when the module is enabled, and Explorer may need a restart — [PowerToys dev docs: Context Menu Handlers](https://microsoft.github.io/PowerToys/common/context-menus/)
- **Microsoft guidance:** a native DLL implements the COM classes, and the manifest declares a comServer plus a `fileExplorerContextMenus` extension. This works for packaged apps and for unpackaged Win32 apps that use a sparse package. GetTitle, GetIcon, GetState and the other menu-construction methods must be fast — [MS Learn: Integrate packaged app with File Explorer](https://learn.microsoft.com/en-my/WINDOWS/APPS/desktop/modernize/integrate-packaged-app-with-file-explorer)
- **Sparse packages** (package identity for unpackaged desktop apps) have been available since Windows 10 version 2004 — [MS Q&A 832880](https://learn.microsoft.com/en-us/answers/questions/832880/adding-an-item-to-windows-11-context-menu)
- **Third-party walkthrough** of the Win11 menu packaging route — [zabkat blog](https://www.zabkat.com/blog/win11-explorer-menu-package.htm)
- **Tangerine's keyboard path:** Enter on a selected file opens the wheel. Cmd/Ctrl switch wheels, arrow keys choose, Enter applies and Esc cancels — [tangerineformac.com](https://tangerineformac.com)

### Inferences
- **Hotkey + Explorer selection:** register the hotkey with RegisterHotKey. When it fires, find the foreground Explorer window and get its selection through IShellWindows, then IWebBrowserApp, then IServiceProvider → IShellBrowser → IShellView → IFolderView::Items(SVGIO_SELECTION). The desktop is handled via SWC_DESKTOP. Then show the wheel at the cursor or the screen center, with keyboard navigation.
- The IExplorerCommand handler can host a submenu ("Convert to ▸ PNG/JPG/WebP…"), filtered by extension in GetState.
- Send To (a shortcut in shell:sendto) is the zero-packaging fallback for Windows 10. It is not available in the top-level Windows 11 menu.

### Gaps
- I did not inspect the File Locksmith or PowerRename source specifically, or the Files (files-community) app's integration.
- QuickLook and Flow Launcher hotkey or selection code was not retrieved in this pass.

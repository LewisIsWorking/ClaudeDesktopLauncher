# Claude Desktop Launcher - Roadmap

Current and upcoming work. Historical release notes:
- [`docs/release-history/v1.0-v1.8.md`](docs/release-history/v1.0-v1.8.md) - foundation + UI maturity era
- [`docs/release-history/v1.9.md`](docs/release-history/v1.9.md) - thumbnail-and-card series
- [`docs/release-history/v1.10.md`](docs/release-history/v1.10.md) - Velopack migration through v1.10.3 icon-cache polish
- [`docs/RELEASE-HISTORY.md`](docs/RELEASE-HISTORY.md) - index pointing at the above

## v1.10.18 - Released
**Critical fix #2.** v1.10.17's global exception handler did NOT catch the WebView2 focus crash - because that exception is thrown synchronously inside a Win32 WndProc callback, bypassing the Avalonia dispatcher entirely. Fixed by setting `Focusable="False"` on the embedded NativeWebView so the crash path is never entered. Mouse interaction still works.

### Numbers
- 332 tests passing. 0 warnings, 0 errors. All files <=200 lines.
## v1.10.17 - Released
**Critical fix.** Catches unhandled UI-thread exceptions so the launcher survives them. Eliminates the recurring crash where the embedded WebView2 usage panel throws `ArgumentException` from `CoreWebView2Controller.MoveFocus` during window activation, bubbling unhandled to the Win32 message pump and killing the process.

### Global exception handler
- `App.HookGlobalExceptionHandlers()` - called at start of `OnFrameworkInitializationCompleted`. Subscribes to `Dispatcher.UIThread.UnhandledException` (logs + `e.Handled = true`) and `AppDomain.CurrentDomain.UnhandledException` (logs only - AppDomain exceptions are still fatal in .NET 5+).
- Documented in `docs/dev/LEARNINGS.md`.

### Numbers
- 332 tests passing. 0 warnings, 0 errors. All files <=200 lines.
## v1.10.16 - Released
Fixes the disk usage display to show "Scanning..." while the background file scan is in progress, preventing stale data from being shown as if it were the current result. Also corrects the scan total: on a typical install (7 ClaudeSlot* + 4 large ClaudeInstance* dirs) the true total is ~133 GB, not the ~48 GB the previous version showed.

### Fix: Scanning... indicator during disk scan
- MainWindowResourceViewModel.IsDiskScanning — new observable bool, set to true before GetTotalGbAsync() fires and false when it completes.
- RefreshDiskUsageAsync now sets IsDiskScanning = true on the UI thread before the scan starts, and alse + updates TotalDiskGb when done.
- ResourceTotalsRow.axaml — "Scanning..." TextBlock visible when IsDiskScanning is true (grey, with tooltip explaining the delay). GB TextBlock hidden while scanning. Both mutually exclusive.

## v1.10.15 - Released
Adds a manual "Check for updates" button visible in the launcher when no update is in progress. Previously the only way to trigger an update check was to restart the launcher or wait for the 6-hour auto-check timer.

### Check for updates button
- MainWindowUpdateViewModel.IsIdle — new computed bool, true when State == Idle. Raised on every state change.
- CheckForUpdatesCommand — new relay command that calls RunCheckAsync(autoCheckEnabled: true), bypassing the AutoCheck setting so the manual button always works even if auto-update is toggled off.
- LaunchControlsPanel.axaml — small "Check for updates" button bound to Update.CheckForUpdatesCommand, visible only when Update.IsIdle. Disappears when a check, download, or banner is active.

## v1.10.14 - Released
Fixes the disk usage display to include legacy ClaudeInstance* directories alongside ClaudeSlot*. These are real Chromium profiles created by older versions of the launcher that the current codebase no longer creates but which still occupy disk space.

### Fix: include ClaudeInstance* in disk scan
- ClaudeDiskUsageService now scans both ClaudeSlot* and ClaudeInstance* patterns via ScanPatterns array.
- 1 new test: GetTotalGbAsync_IncludesLegacyClaudeInstanceDirs.

## v1.10.13 - Released
Adds a **Disk** column to the resource totals row showing the combined on-disk size of all ClaudeSlot* directories. Refreshes at startup and on the manual refresh (?) button — not on every poll tick, since a full recursive scan of 80+ GB takes several seconds.

### Claude disk usage display
- IClaudeDiskUsageService — new interface in Core with Task<double> GetTotalGbAsync().
- ClaudeDiskUsageService — enumerates %LOCALAPPDATA%\ClaudeSlot* directories recursively on a thread-pool thread. Has an internal testing seam constructor. Never throws — returns 0.0 on any failure.
- MainWindowResourceViewModel.TotalDiskGb — new observable property. Refreshed asynchronously at construction and on every ManualRefresh() call. Updates via Dispatcher.UIThread.InvokeAsync so the background scan never touches the UI thread.
- ResourceTotalsRow.axaml — new "Disk" column (GB, 1dp) with tooltip explaining the refresh cadence.
- InternalsVisibleTo added to ClaudeDesktopLauncher.Core.csproj so the testing seam constructor is reachable from the Tests project.

## v1.10.12 - Released
Raises the slot count spinner maximum from 20 to 100. No technical upper limit exists on slot count; the only practical constraint is available RAM.

## v1.10.11 - Released
Raises the slot count spinner maximum from 10 to 20. There is no technical upper limit on slot count (the scanner, classifier, and data directories all work for any slot number); the only practical constraint is available RAM (~300-500 MB per Claude instance).

## v1.10.10 - Released
Adds a per-slot activity signal to each slot card — "Active now", "Active Xm ago", or "Idle" — derived from when the slot's CPU last crossed 3%. Also closes the per-slot activity preview backlog item: thumbnails were already shipped in v1.9.x; this adds the missing activity timestamp signal. Also ticks off the "Submit to awesome-avalonia" backlog item.

### Per-slot activity signal
- ClaudeInstanceViewModel.LastActiveDisplay — computed string property updated on every UpdateFrom call. Stamps _lastActiveAt = DateTime.UtcNow when CpuPercent >= 3.0. Format: "Idle" (never spiked), "Active now" (<30s), "Active Xm ago" (<1h), "Active Xh Xm ago" (≥1h).
- SlotCard.axaml — small grey TextBlock below the stats row bound to LastActiveDisplay, with tooltip explaining the 3% threshold.
- 6 new tests in ClaudeInstanceViewModelActivityTests: never-active→Idle, high-CPU→Active now, low-CPU→Idle, exact-threshold→Active, spike-then-idle retains timestamp, just-below-threshold→Idle.

### Backlog closures
- **Per-slot activity preview** — closed as shipped. Thumbnails landed in v1.9.x; last-active timestamp lands here.
- **Submit to awesome-avalonia** — PR opened to AvaloniaCommunity/awesome-avalonia adding Claude Desktop Launcher to the Open Source Applications section.

## v1.10.9 - Released
Per-slot RAM and CPU totals now match Windows Task Manager by aggregating the full Electron process tree (renderer, GPU, crashpad, network service, node-service) into each slot card. Also ships a large test health pass: 57 new tests across 7 new files, covering tree analysis, child-snapshot aggregation, resource monitor CPU delta, thumbnail refresher, slot callback binder, update orchestrator, and banner text formatting.

### Per-slot full-tree RAM/CPU aggregation
- `ClaudeProcessInfo.ChildProcessIds` field added — the WMI scanner now populates it with the full list of descendant PIDs for each main Claude process.
- `ClaudeProcessTreeAnalyser` (new, Core layer) — pure static BFS over a parent→children map. Extracted from the scanner so tree-walking logic is unit-testable independently of WMI. Handles arbitrary depth; returns empty list for leaf nodes.
- `WmiClaudeProcessScanner.Scan()` calls `ClaudeProcessTreeAnalyser.CollectDescendantPids` after building the `childPidsByParent` map, attaching descendants to each `ClaudeProcessInfo`.
- `SlotInstanceListViewModel.AggregateChildSnapshots` (new, `internal static`) — sums `RamBytes` and `CpuPercent` from each slot's child snapshots into the main-process snapshot before collection reconciliation. Missing child snapshots (already exited) are silently skipped. Early-exits when no `ChildProcessIds` are set so the no-Claude-running path is zero-cost.
- Per-slot card figures now match Task Manager's per-app totals. The v1.10.7 totals-row fix already corrected `TotalRamMb`/`TotalCpuPercent`; this fix closes the per-slot card gap.

### Test health
- `VelopackLogApplyFailureDetectorTests` — corrected log format in test fixtures from `[HH:MM:SS] ERROR:` to real Velopack format `[HH:MM:SS] [ERROR] Apply error:`. All 5 detector tests now pass.
- `ExternalInstanceViewModelGapTests` — removed the valid-PNG `UpdateThumbnailFromBytes` test; Avalonia's `Bitmap` constructor requires a running `IPlatformRenderInterface` unavailable in headless unit tests. Null/empty boundary tests retained.
- `MainWindowUpdateViewModelBannerTests` — replaced four tautological same-variable comparisons (CS1718) with three meaningful enum-distinctness assertions (`Downloading ≠ Idle ≠ ReadyToInstall ≠ Failed ≠ ApplyFailed`). Zero warnings.
- 7 new test files: `ClaudeProcessTreeAnalyserTests` (6), `SlotInstanceListViewModelAggregationTests` (6), `ResourceMonitorCpuTests` (3), `ExternalInstanceViewModelGapTests` (4), `MainWindowUpdateViewModelBannerTests` (14), `SlotCallbackBinderTests` (7), `ThumbnailRefresherTests` (8).


## v1.10.8 - Released
Fixes the version footer display (was stuck at v1.10.5 since project creation) and adds repo health infrastructure: a version-consistency test that catches csproj/assembly drift in CI, and a pre-push git hook that runs the full test suite before every push.

### Footer version fix
- Removed explicit `<AssemblyVersion>` element from csproj — MSBuild now derives `AssemblyVersion` from `<Version>` automatically. `VersionProvider` reads `Assembly.GetName().Version`, so footer now shows the correct version on every Velopack-installed build.
- Re-versioned from 1.10.7.1 to 1.10.8 (Velopack requires 3-part SemVer; four-part tags are rejected).

### Repo health
- `VersionConsistencyTests` — asserts that compiled `AssemblyVersion` matches the csproj `<Version>` at the `Major.Minor.Build` level. Catches any future divergence before it reaches users.
- `docs/dev/hooks/pre-push` — git hook that runs `dotnet test --verbosity quiet` before every `git push`. Aborts the push on any test failure. Install once: `cp docs/dev/hooks/pre-push .git/hooks/pre-push`. Documented in `BUILD-AND-TOOLING.md`.


## v1.10.7 - Released
Embeds the Claude usage dashboard directly into the main launcher window as a side-by-side panel, adds a responsive breakpoint that stacks vertically on narrow windows, and fixes the resource total underestimation.
### Embedded usage dashboard (NativeWebView)
- Avalonia.Controls.WebView 12.0.0 added as a dependency. Uses platform-native rendering (WebView2 on Windows, WKWebView on macOS, WebKitGTK on Linux) - no Chromium bundled, negligible install size delta.
- NativeWebView embedded directly in MainWindow.axaml as the right column of a two-panel Grid. Navigates to claude.ai/settings/usage on launch.
- NavigationCompleted redirect: after login claude.ai redirects to the home page; detected and re-navigated to /settings/usage automatically.
- Auth persistence: EnvironmentRequested handler sets UserDataFolder via reflection (avoids needing the exact WindowsWebView2EnvironmentRequestedEventArgs type name at compile time, keeping the code cross-platform). User logs in once; cookies survive restarts.
### Responsive layout + scroll preservation
- MainWindow.axaml.cs ApplyLayout() runs on OnSizeChanged and OnOpened. Width >= 900px: horizontal Grid (440,4,*) with vertical GridSplitter. Width < 900px: vertical Grid (*,4,320) with horizontal GridSplitter.
- Layout re-applies when UsagePanelOnLeft changes, keeping the orientation consistent.
- ScrollViewer offset saved before ApplyLayout and restored after so the left panel doesn't jump to the top when the window is resized.
### Usage panel position toggle
- **Checkbox (A)**: "Usage on left" checkbox added to the settings row in LaunchControlsPanel.axaml, binding to MainWindowViewModel.UsagePanelOnLeft. Persisted in AppSettings.
- **Right-click (B)**: GridSplitter context menu wired in MainWindow.axaml.cs. Opens on right-click; header reads "Move usage panel to left/right" depending on current state. Click triggers ToggleUsagePanelPositionCommand.
### RAM/CPU total fix
- IProcessService.GetAllProcessSnapshots added. Unlike GetWindowedProcessSnapshots, it captures all claude.exe processes including child/helper processes (renderer, GPU, crashpad, network service, node-service, etc.) that Electron spawns per instance.
- ResourceMonitor switched from GetWindowedProcessSnapshots to GetAllProcessSnapshots so TotalRamMb and TotalCpuPercent match what Windows Task Manager shows for the full process tree. Per-slot card numbers still show the browser-main process only (full-tree aggregation is a backlog item).
## v1.10.6 - Released
Completes the show/hide-button pair introduced in v1.10.5. Adds a Show button to every TrayCard row so hidden Claude slots can be restored to the foreground directly from the launcher without digging through Claude system-tray menu. Also fixes a latent bug where tray-resident slots were invisible to the resource monitor and were dropped from both collections on the next poll.
### New IWindowShower service
- IWindowShower.TryShow(int processId) enumerates all top-level windows via EnumWindows + GetWindowThreadProcessId, selects the best candidate for the target PID, then calls ShowWindow(hwnd, SW_SHOW) and SetForegroundWindow(hwnd). Returns true on success, false on any failure. Never throws.
- Window selection heuristic: skip WS_EX_TOOLWINDOW windows (Electron renderer helpers), prefer windows with a non-empty title (the main Electron browser window always has a page title), fall back to first remaining match.
- Managed EnumWindowsProc delegate held in a local variable during enumeration to prevent GC collection mid-callback.
- SetForegroundWindow can fail silently due to Windows foreground-lock mechanism. Logged but not fought.
- Separate interface from IWindowHider (single-responsibility: different Win32 footprint, different failure modes).
### Tray-resident slot visibility fix
- SlotInstanceListViewModel.Refresh synthesises stub InstanceResourceSnapshot entries for tray-resident PIDs that have no resource snapshot (the resource monitor only snapshots windowed processes). Stubs carry real uptime from ClaudeProcessInfo.StartTime; CPU/RAM show zero.
- MainWindowResourceViewModel derives RunningInstanceCount from collection sizes after reconciliation (Items.Count + TrayItems.Count + ExternalInstances.Count) instead of IClaudeInstanceLauncher.GetRunningInstanceCount() which only counted windowed processes.
- MainWindowResourceViewModel no longer depends on IClaudeInstanceLauncher.
### UI: Show button on TrayCard
- Teal (hex 80CBC4) Show button in column 3 of the TrayCard stats row. Quit moves to column 4.
- ClaudeInstanceViewModel.ShowCommand routes through a new OnShow callback, wired by SlotCallbackBinder to IWindowShower.TryShow.
## v1.10.5 - Released
Adds a Hide button to every slot card so users can close a Claude slot to the system tray without terminating the process.

### New `IWindowHider` service
- `IWindowHider.TryHide(int processId)` - resolves the process's `MainWindowHandle` and calls `ShowWindow(hwnd, SW_HIDE)`. Returns `true` on success, `false` on any failure (process gone, zero hwnd, Win32 call rejected). Never throws.
- `Win32WindowHider` is the production implementation using `user32.dll` P/Invoke. `SW_HIDE` (not `SW_MINIMIZE`) - minimise leaves the window in the taskbar, hide removes it entirely, matching the user's mental model of "close to tray".
- Logged on every invocation so diagnostic tail-reads can see whether Hide attempts landed.

### UI: Hide button on `SlotCard`
- Button sits in the stats row between the uptime display and the existing Kill (X) button. Neutral grey (`#A0A0A0`) foreground so it visually de-emphasises vs Kill's red (`#EF9A9A`).
- `ClaudeInstanceViewModel.HideCommand` routes through a new `OnHide` callback, wired up by `SlotCallbackBinder` to call `IWindowHider.TryHide`.
- No refresh prod needed after hide - the next scanner poll naturally moves the slot into the TrayCard list.


## v2.0 - ComeOnOver Integration

- [ ] Native ComeOnOver desktop app detection and launch (when available)
- [ ] Link to ComeOnOver download page if not installed
- [ ] ComeOnOver version display

## v3.0 - Cross-Platform

- [ ] macOS support (Claude Desktop path resolver)
- [ ] Linux support (Claude Desktop AppImage/deb path resolver)
- [ ] Platform-specific path resolver implementations behind `IClaudePathResolver`
- [ ] CI/CD pipeline for multi-platform builds

## Monetisation

- [ ] In-app advertising (tasteful, non-intrusive - planned for a future version)
- [ ] GitHub Sponsors / Ko-fi as an alternative for users who prefer ad-free
- [ ] Ads will never appear in v1.x - planned for a later major version once the user base is established

## Backlog / Under Consideration

- [x] **Shared extension store across slots** - CLOSED: junction/symlink approach is not feasible. Windows blocks directory enumeration (FindFirstFileW) on reparse points whose source is inside `%LOCALAPPDATA%` (non-Temp), regardless of ACLs, reparse tag type, or `\\?\` prefix. Since `ClaudeSlot{N}\Claude Extensions\` is always inside `%LOCALAPPDATA%`, Claude's Node.js process cannot enumerate through any junction placed there. Investigated empirically 2026-04-22 - confirmed on Lewis's machine. See `docs/dev/LEARNINGS.md` for full findings. The 1.22 GB duplication remains; a future copy-on-install propagation approach (install in one slot → offer to copy to others) is a different feature with different tradeoffs and could be added as a new backlog item.

- [x] **Per-slot activity preview** - Shipped in v1.10.10. Thumbnails: v1.9.x. Last-active timestamp: v1.10.10.

- [ ] **Per-instance token/usage display** - deferred until Anthropic exposes a stable per-session API. The embedded WebView panel (v1.10.7) already shows global usage at claude.ai/settings/usage. A local-only cooldown timer is not meaningful without knowing when the user's session window actually started.

- [x] Submit to awesome-avalonia list - Merged into AvaloniaCommunity/awesome-avalonia (PR #265, 2026-04-24). Listed under the Artificial Intelligence section.
- [ ] Reddit / HN launch post

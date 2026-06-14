# Claude Desktop Launcher - Roadmap

Current and upcoming work. Historical release notes:
- [`docs/release-history/v1.0-v1.8.md`](docs/release-history/v1.0-v1.8.md) - foundation + UI maturity era
- [`docs/release-history/v1.9.md`](docs/release-history/v1.9.md) - thumbnail-and-card series
- [`docs/release-history/v1.10.md`](docs/release-history/v1.10.md) - Velopack migration through v1.10.3 icon-cache polish
- [`docs/RELEASE-HISTORY.md`](docs/RELEASE-HISTORY.md) - index pointing at the above

## v1.10.21 - Released
Linux MVP M4: GitHub Actions now produces a Linux x64 artifact alongside the existing Windows artifacts on every release. A new `build-linux` job runs after the Windows job (so it can append to the existing GitHub Release), runs the test suite on Ubuntu (catching cross-platform regressions in CI rather than only on Lewis's laptop), publishes a self-contained `linux-x64` build, tars it (excluding pdbs), and uploads `ClaudeDesktopLauncher-linux-x64.tar.gz` via `gh release upload`. Tarball is ~44 MB compressed, 224 files, valid ELF binary with executable bit preserved. Linux end-users now download a single file, `tar -xzf`, and `./ClaudeDesktopLauncher`.

### Why sequential not parallel
The Windows job creates the GitHub Release via `vpk upload github --publish`. The Linux job uses `gh release upload --clobber` to append to it. If the jobs ran in parallel, the Linux upload could race the Windows release creation. Sequential gates this correctly and only costs ~2 extra minutes of wall time per release.

### What's NOT in v1.10.21
- AppImage / .deb packaging (deferred to M5; users who want a desktop entry must create their own .desktop file for now).
- Linux auto-update (deferred; users check GitHub Releases manually).
- ARM64 Linux (deferred; only linux-x64 is built).

### Next
M5 - AppImage or .deb packaging so the launcher gains a Start Menu entry and proper system integration on Linux distros.

## v1.10.20 - Released
Linux MVP M3: real `ProcfsClaudeProcessScanner` parsing `/proc/*/cmdline`. Replaces the v1.10.19 empty stub so running Claude instances now appear in the launcher's slot/external lists on Linux just like they do on Windows.

### What works
Walks `/proc` per refresh tick; filters to `comm == "electron"`; identifies main Claude electrons by `app.asar` in cmdline + absence of `--type=` Electron child flag. Reuses the existing `ClaudeProcessTreeAnalyser` (pure function, no platform changes needed) for descendant PID aggregation so slot RAM/CPU totals work the same as on Windows. Start time computed from `/proc/PID/stat` field 22 + `/proc/stat` btime + ticks-per-second (cached). The slot classifier regex updated from `\ClaudeSlotN` to `[\/]ClaudeSlotN` so the single pattern matches Windows `%LOCALAPPDATA%\ClaudeSlotN` AND Linux `~/.config/ClaudeSlotN`.

### Verified live
On Lewis's Linux laptop: 13 electron processes total, 1 main identified, 12 helpers filtered out. The main has no `--user-data-dir=ClaudeSlot*` so it classifies as External (correct - it's the launcher-unaware install).

### Numbers
- 335 tests pass on Linux (22 new this milestone: 17 ProcfsParser + 2 Linux classifier + 3 from earlier).
- 0 warnings, 0 errors. All files <= 200 lines.

### Next
- M4: GitHub Actions matrix to also build for `ubuntu-latest`.
- M5: AppImage or .deb packaging.
- Deferred: Wayland-portal window enumeration for IsWindowed; xdg-desktop-portal screenshot for Linux thumbnails.

## v1.10.19 - Released
First Linux build. Same codebase produces a Windows .exe (full feature set) and a Linux binary (cross-platform subset with no-op stubs where Win32/WebView2/Velopack don't apply). No Windows regression - all Windows-only code paths are gated by ``Condition="'$(OS)' == 'Windows_NT'"`` in the csproj files plus ``#if WINDOWS`` blocks for the few mixed source files.

### What works on Linux
- Build compiles cleanly with ``dotnet build`` (0 warnings, 0 errors).
- 313 tests pass on Linux (out of 313 cross-platform tests; the 6 Windows-only test files are excluded via ``<Compile Remove>``).
- Process starts, framework initialises, ``MainWindowViewModel`` constructs, slot monitor and seed cache updater run on their timers.
- ``LinuxClaudePathResolver`` finds ``/usr/bin/claude-desktop`` (the community Debian build).

### Stubbed on Linux for v1.10.19 (deferred to later milestones)
WMI scanner (procfs deferred to M3), Win32 hide/show, PrintWindow thumbnails (Wayland blocks cross-app capture), HKCU registry + run-at-startup (use ``.desktop`` autostart files), Velopack auto-update (manual download from GitHub Releases), embedded usage WebView (the ``UsagePanelHost`` ContentControl stays empty).

### Architecture changes that enabled this
- csproj conditionals key off ``'$(OS)' == 'Windows_NT'`` for packages and ``<Compile Remove>``; mixed source files use ``#if WINDOWS``. Full details in ``docs/dev/LEARNINGS.md``.
- ``MainWindow.axaml`` uses a ``ContentControl x:Name="UsagePanelHost"`` placeholder; WebView setup moved to a Windows-only partial class ``MainWindow.WebView.cs`` via a partial method ``InitializeUsagePanel()``.
- DI split into cross-platform + ``RegisterWindowsServices`` / ``RegisterLinuxServices`` branches in ``App.axaml.cs``, the Windows branch wrapped in ``#if WINDOWS``.
- New Linux service folders contain 11 stub implementations (mostly no-op).

### Next milestones
- M3: real ``ProcfsClaudeProcessScanner`` parsing ``/proc/*/cmdline`` for running Claude electron processes
- M4: GitHub Actions matrix build for ``windows-latest`` + ``ubuntu-latest`` with separate artefacts per platform
- M5: AppImage or .deb packaging for Linux

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

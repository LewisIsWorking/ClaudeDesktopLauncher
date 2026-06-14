# CoODL — Claude's learnings

Repeated mistakes and their fixes. Written so future-me (any Claude session) does not re-discover these by touching the stove.

Read this FIRST at the start of any CoODL session. Add to it whenever a lesson is learned twice. If a topic accumulates and pushes this file near the 200-line limit, extract it into a sibling `docs/dev/<TOPIC>.md` and cross-reference from here.

## Companion docs

- **`docs/dev/BUILD-AND-TOOLING.md`** — line-count audit, PowerShell / heredoc quirks, commit-message workflow, CI runner gotchas, screenshot workflow, UI-Automation snippets, the release checklist.
- **`docs/dev/VELOPACK.md`** (v1.10.0+) — Velopack packaging + auto-update CI pipeline: local `vpk pack` commands, the `PublishSingleFile=false` and `fetch-depth: 0` invariants, the `--token` gotcha, delta-package generation via `vpk download github`, post-ship validation strategy, code-signing future plans.
- **`docs/dev/REFACTOR-AND-XAML.md`** — NSubstitute / fixture gotchas, Avalonia compiled-binding pitfalls (`string` -> `Color`, `Tapped` vs `PointerPressed`, `IsHitTestVisible` on child images/overlays), WMI scanner Electron quirk, close-to-tray process state, clipboard bitmap capture.
- **`docs/MIGRATION.md`** — user-facing v1.9.x -> v1.10.0 migration guide (portable .exe -> Setup.exe + auto-update).

## Hard rules (repeated here for emphasis)

These come from the project owner and are non-negotiable:

1. **Every file ≤ 200 lines** (.cs, .axaml, .md, .ts, etc.). Only exception is `package.json`. When a file grows past 200, extract via OOP — split, don't trim. Never remove comments, code or whitespace to hit the limit.
2. **100% test pass rate** before any push. Build must be 0 warnings, 0 errors.
3. **Never suppress warnings** with `NoWarn`, `#pragma`, or `[SuppressMessage]`. Fix the root cause.
4. **Never strip logging.** Diagnostic/info/warning/error logging stays permanently once added. Logging is never "temporary".
5. **Always `dotnet run` + visually verify** before committing a visual change. Fresh screenshot → `docs/screenshots/` → ROADMAP updated referencing it.
6. **Plan before implementing.** Present options, wait for the user to pick, then build. No unilateral "I'll just build it" moves.
7. **SOLID / MVVM / event-driven** throughout. ViewModels never touch system APIs directly — everything goes through an `I<Service>` abstraction.
8. **Read `docs/` first.** At the start of any CoODL session, read this file and the two companion docs before touching code. Saves re-discovering things.

## CoODL architecture invariants

- `MainWindowViewModel` depends only on `I<Service>` abstractions. No direct WMI / filesystem / registry calls.
- Core layer (`ClaudeDesktopLauncher.Core`) has zero Avalonia references. Keep UI out of Core. The only exception is that Core services can return raw bytes (e.g. `byte[]?` for thumbnails) and the UI layer materialises them into Avalonia types.
- Every service has an `I<Service>` interface in `Services/Interfaces/` and a concrete implementation in `Services/`.
- List VMs own reconciliation — they mutate their `ObservableCollection` in place, never reassign. Preserves Avalonia binding identity so row state (edit-in-progress text) survives refreshes.
- Reconciliation is identity-preserving: match by stable key (slot number for slots, PID for externals), update in place, add missing, remove gone. Never clear-and-repopulate.
- Pure functions / testable logic separated from IO-orchestration services. Example: `SeedCacheValidators` (pure validation) extracted from `FileSlotSeedCache` (IO orchestration).
- Scanner→Classifier→ViewModel pipeline. Scanner returns raw `ClaudeProcessInfo`. Classifier returns typed `SlotProcessInfo` / `ExternalProcessInfo` / null. VMs filter + reconcile.
- `IThumbnailableViewModel` is the shared surface across both `ClaudeInstanceViewModel` and `ExternalInstanceViewModel`, letting `ThumbnailRefresher` treat both uniformly without a common base class (v1.9.1+).
- Per-row callbacks live as `Action<T>? OnSomething` properties on the list VM, wired via `SlotCallbackBinder.Bind` (slots) + `SlotCallbackBinder.BindExternal` (externals). Keeps row VMs free of service dependencies.
- **Velopack auto-update** (v1.10.0+): `VelopackApp.Build().Run()` MUST run as the first line of `Program.Main`, before Avalonia boots. Velopack's `UpdateManager` is a concrete type wrapped by our `IAutoUpdateService` adapter for testability. Update orchestration lives in `MainWindowUpdateViewModel` (sub-VM pattern) + `UpdateOrchestrator` (state machine) so `MainWindowViewModel` stays under the 200-line cap. Full design rationale in `docs/dev/VELOPACK.md`.
- **Self-heal at startup** (v1.10.2+): where an external system (Velopack, Claude Desktop, the Windows Shell) has a known bug that leaves our app in a broken state, we prefer shipping a local heal-on-startup check over waiting for an upstream fix. The pattern: interface + two injectable probes (production resolver + a mock for testing), dev-build guard so `dotnet run` never attempts the heal, defensive `try/catch` at the top of the heal method so a broken probe can't block startup, and an enum return type so callers (and tests) can assert which branch ran. `IShortcutHealer` is the reference implementation.

## NTFS junctions and symlinks inside AppData\Local do not support directory enumeration

Investigated 2026-04-22 during the shared extension store design. **Do not re-investigate — this is a confirmed Windows OS restriction, not a bug we can fix.**

- `GetFileSystemEntries` (`FindFirstFileW`) and `GetChildItem` on a junction or directory symlink located inside `%LOCALAPPDATA%` (but outside `%LOCALAPPDATA%\Temp`) always fail with "Could not find a part of the path", even though `Test-Path` and `GetFileAttributesW` succeed on the same path.
- Confirmed on the developer machine (Windows 11, Developer Mode ON). Not caused by: AppContainer SIDs, spaces in path names, `\\?\` extended prefix, or junction vs symlink reparse tag. A junction FROM `Documents` or `AppData\Roaming` TO `AppData\Local` works fine — the restriction is specifically on junctions whose SOURCE is inside `AppData\Local` (non-Temp).
- **Consequence for extension store**: `ClaudeSlot{N}\Claude Extensions\` is always inside `AppData\Local`, so any junction/symlink placed there to redirect to a shared store will be unreadable by Claude's Node.js process (which uses the same Win32 `FindFirstFileW` API). The entire junction-based extension deduplication approach is blocked.
- The backlog item "Shared extension store" has been closed as not feasible via junctions/symlinks. See ROADMAP for the updated note.

## Diagnostic honesty — lessons from the 2026-04-20 update-failure investigation

- **"Transient" is a conclusion, not an opening hypothesis.** Cheap diagnostics FIRST: read the relevant log, run `handle64.exe`, check process trees. A retry without evidence wastes the user's time. Two consecutive failures with identical symptoms = reproducible bug, not flake.
- **Dev builds can impersonate the installed version.** A `dotnet run` produces an .exe showing the same version footer as the Velopack build. ONLY check via `Get-Process ClaudeDesktopLauncher | Select-Object Path` and `GetLastWriteTime` on the installed exe. Kill stray dev builds before diagnostic work.
- **Velopack silently relaunches the old version on apply failure.** When apply fails, Velopack logs `[ERROR] Apply error:` to `velopack.log` and relaunches the original exe. No C# exception is thrown. The only signal is the log. Detection pattern lives in `IUpdateApplyFailureDetector` (v1.10.4+).
- **"Continue" isn't always the right response to a stuck investigation.** Stop after the second identical failure and write a plan. Signs to stop: repeated same-symptom failures; speculating instead of measuring; running low on tokens while still flailing.

## Avalonia.Controls.WebView - type name resolution
Added in v1.10.7. Package: `Avalonia.Controls.WebView 12.0.0`. `NativeWebView` is in the `Avalonia.Controls` namespace. `WebViewNavigationCompletedEventArgs` also in `Avalonia.Controls`. Set `UserDataFolder` via reflection inside `EnvironmentRequested` handler (avoids needing `WindowsWebView2EnvironmentRequestedEventArgs` type name). No `AppBuilder` extension needed — just add the package and use `NativeWebView` in XAML.

## Memory / mental model

- The user runs the CoODL launcher on Windows (Dell laptop is Linux but not the target).
- ClaudeSlot{N} data directories live at `%LOCALAPPDATA%\ClaudeSlot{N}\`.
- Seed cache is at `%APPDATA%\ClaudeDesktopLauncher\seed\`.
- Logs are at `%APPDATA%\ClaudeDesktopLauncher\logs\launcher-YYYY-MM-DD.log`.
- User's Windows GitHub Desktop is the shiftkey fork v3.4.12-linux1.
- AutoHotkey v2 script at `%USERPROFILE%\Documents\AutoHotkey\Continue.ahk`.
- The user has a multi-monitor setup. Primary monitor is 2560x1440. Prefer `Windows-MCP:Click` tool for automated clicks (handles DPI correctly).

## Avalonia.Controls.WebView focus crash - global exception handler

Added in v1.10.17. The Avalonia.Controls.WebView 12.0.0 package has a known crash where `CoreWebView2Controller.MoveFocus` throws `System.ArgumentException` (HRESULT 0x80070057) when the underlying WebView2 is in a transient state during window activation. The exception bubbles from `NativeWebView.OnGotFocus` -> `WindowBase.HandleActivated` -> Win32 `WndProc` and kills the process because no Avalonia layer catches it.

**Fix**: `App.OnFrameworkInitializationCompleted` calls `HookGlobalExceptionHandlers()` which subscribes to `Dispatcher.UIThread.UnhandledException`, logs the exception, and sets `e.Handled = true`. The launcher survives the crash and the user keeps working. Every swallowed exception is logged so real bugs remain discoverable in `%APPDATA%\ClaudeDesktopLauncher\logs\`.

**Diagnostic pattern**: when the launcher dies abruptly with no stack in the launcher log, check the Windows Application event log for `ClaudeDesktopLauncher.exe` faulting application entries - the .NET Runtime usually logs the unhandled exception there before the process exits.

## WndProc-synchronous exceptions bypass Dispatcher.UnhandledException

Discovered in v1.10.18 after v1.10.17 shipped with what turned out to be an ineffective fix. `Dispatcher.UIThread.UnhandledException` only fires for exceptions thrown during `Invoke`/`Post` operations on the dispatcher. Exceptions thrown synchronously from inside a Win32 WndProc callback (focus events, paint events, etc.) bypass the dispatcher entirely - they propagate up through native code to the Win32 message pump and kill the process before any managed handler can intercept them.

**Concrete case**: the WebView2 `CoreWebView2Controller.MoveFocus` ArgumentException crash originates from `NativeWebView.OnGotFocus`, which is called synchronously by `WindowBase.HandleActivated` from `WindowImpl.AppWndProc`. The Dispatcher.UnhandledException hook from v1.10.17 was registered but never fired for this crash, and v1.10.17 continued to crash identically.

**Correct fix**: prevent the exception at the source. v1.10.18 sets `Focusable="False"` on the NativeWebView so the OnGotFocus -> MoveFocus path is never entered. Mouse interaction still works because pointer events don't trigger focus.

**Diagnostic signature**: when a crash logged in the Windows Application event log shows "The process was terminated due to an unhandled exception" with a stack trace that ends in `WindowImpl.AppWndProc` or any `HandleXxx` method on `WindowBase`, this is a WndProc-synchronous exception. Don't try to catch it - find what triggers it and prevent the trigger.
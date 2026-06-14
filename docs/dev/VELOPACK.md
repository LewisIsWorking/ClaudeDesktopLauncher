# CoODL - Velopack packaging & auto-update

Extracted from docs/dev/BUILD-AND-TOOLING.md during v1.10.1 to keep that file under the 200-line limit. These are accumulated gotchas from landing the Velopack migration in v1.10.0 and shipping the first validation hotfix in v1.10.1.

The core architecture lessons (VelopackApp in Program.Main, IAutoUpdateService wrapper, UpdateOrchestrator state machine) live in docs/dev/LEARNINGS.md under the Architecture section - this file is purely about packaging + CI + update-pipeline mechanics.

## CI workflow

CoODL releases are packaged with [Velopack](https://velopack.io). The CI workflow installs `vpk` via `dotnet tool install -g vpk`, then runs `vpk pack` to produce the `Setup.exe` installer + delta packages, then `vpk upload github` to publish the release.

### Local testing

For iterating on the install flow locally without pushing a tag:

```powershell
# From repo root
Remove-Item -Recurse -Force publish, Releases -ErrorAction SilentlyContinue

dotnet publish ClaudeDesktopLauncher\ClaudeDesktopLauncher.csproj `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output publish `
    -p:PublishSingleFile=false

vpk pack `
    --packId ClaudeDesktopLauncher `
    --packVersion 1.10.0-test `
    --packDir publish `
    --mainExe ClaudeDesktopLauncher.exe `
    --packTitle "Claude Desktop Launcher" `
    --shortcuts "Desktop,StartMenu"
```

Output appears in `Releases/`. Key files:
- `ClaudeDesktopLauncher-win-Setup.exe` - bootstrap installer for end users
- `ClaudeDesktopLauncher-{version}-full.nupkg` - full Velopack update package
- `ClaudeDesktopLauncher-{version}-delta.nupkg` - delta from previous version (only from v1.10.1 onwards)
- `ClaudeDesktopLauncher-win-Portable.zip` - bonus portable variant
- `RELEASES`, `releases.win.json`, `assets.win.json` - Velopack manifests

Both `publish/` and `Releases/` are `.gitignore`d.

### Publish single-file MUST be false

Velopack needs a multi-file output to produce delta packages - it rebuilds individual assembly diffs between versions. The CI workflow's `dotnet publish` step explicitly sets `-p:PublishSingleFile=false`. Don't re-enable PublishSingleFile or you'll break delta generation and every update will be a full 100MB download.

### VelopackApp.Build() must run in Program.Main, not App.axaml.cs

`VelopackApp.Build().OnFirstRun(...).Run()` lives as the very first line of `Program.Main()`, before Avalonia's `BuildAvaloniaApp().StartWithClassicDesktopLifetime()`. If it runs later (e.g. inside `App.OnFrameworkInitializationCompleted`), `vpk pack` emits a warning and Velopack's install/update/uninstall hooks can briefly flash a window or fail silently. Don't move it.

### Code signing (future)

v1.10.0 ships unsigned. Every `vpk pack` emits `WRN: No signing parameters provided` for ~231 binaries - these warnings are expected and benign in the v1.10.0 era. Users see Windows SmartScreen on first install; subsequent auto-updates are silent (Velopack's updater inherits the install's trust).

When we do sign, **Azure Trusted Signing is the recommended option** (~$10/month, integrates cleanly with GitHub Actions, no hardware token). Traditional OV/EV certs require USB hardware tokens that don't work in CI. The retrofit is ~2 hours of workflow changes plus Azure tenant setup - a deliberately-deferred decision, not a forgotten one.

### Validating updates after shipping

Because the auto-update flow requires TWO published releases (the old one must actually be able to update to the new one), the canonical validation strategy is:
1. Ship v1.10.0 as the first Velopack release.
2. Install it locally from the published `Setup.exe`.
3. Ship v1.10.1 with a trivial change (e.g. a log message tweak) purely to validate the update pipeline.
4. Wait up to 6 hours (the poll interval) or restart the launcher to trigger the check.
5. Confirm the "Restart to install v1.10.1" banner appears, click it, verify the new version launches.

If any step fails, logs at `%APPDATA%\ClaudeDesktopLauncher\logs\launcher-YYYY-MM-DD.log` will show every `[Show]`, `[OpenWindow]`, `CheckForUpdatesAsync`, and Velopack-emitted entry.

### Why fetch-depth: 0 in the workflow

`vpk upload github` needs full git history to locate the previous release tag for delta package generation. Without `fetch-depth: 0` on `actions/checkout`, only the current tag is fetched and delta generation silently produces a full package instead. Delta packages are the whole point of the Velopack migration, so don't remove this.

### `vpk upload github` needs `--token` explicitly, not `GITHUB_TOKEN` env var

v1.10.0's initial CI run failed with `Value cannot be null. (Parameter 'token')` because we'd set `GITHUB_TOKEN` in the step's `env:` block expecting vpk to read it automatically (as most GitHub-hosted CLI tools do). It doesn't. vpk's Octokit client only reads the `--token` command-line argument. Pass `--token ${{ secrets.GITHUB_TOKEN }}` directly on the `vpk upload github` line.

Symptom in the CI log:

```
[INF] Preparing to upload 3 asset(s) to GitHub
[FTL] Value cannot be null. (Parameter 'token')
System.ArgumentNullException
  at Octokit.Ensure.ArgumentNotNull
  at Velopack.Deployment.GitHubRepository.UploadMissingAssetsAsync
```

Good news: the token check happens BEFORE vpk creates the GitHub release, so a failure at this step leaves no orphaned release or draft behind. Delete the tag (local `git tag -d v1.10.0` + remote `git push --delete origin v1.10.0`), fix the workflow, re-tag cleanly.

### Delta packages need `vpk download github` before `vpk pack`

v1.10.1's CI shipped a `-full.nupkg` but NOT a `-delta.nupkg`, despite `fetch-depth: 0` being set correctly. Velopack doesn't generate deltas from git history - it generates them by **diffing against the previous release's actual `.nupkg` content**, which has to be on disk at pack time. Without a previous nupkg in the `Releases/` directory, `vpk pack` has nothing to diff against and silently produces full packages only.

Fix: add a `vpk download github` step BEFORE `vpk pack`:

```yaml
- name: Download previous release for delta generation
  run: |
    vpk download github `
      --repoUrl https://github.com/LewisIsWorking/ClaudeDesktopLauncher `
      --token ${{ secrets.GITHUB_TOKEN }}
  continue-on-error: true  # first-ever release has nothing to download
```

The `continue-on-error: true` is required because the v1.10.0 release was the first Velopack release and had no predecessor - without that flag, a first release would fail at the download step.

Auto-update still works without deltas; clients just download the full `.nupkg` (~49 MB) each time instead of a small diff. Disk/bandwidth efficiency loss, not a functional bug. Delta generation should kick in from whichever release first lands with the `vpk download github` step active.

### Upstream bug: Start Menu shortcut may vanish during update apply

Observed on 2026-04-20 during v1.10.0 → v1.10.1 auto-update on the first real-world install. Velopack's install step correctly created the Start Menu `.lnk`. During the update apply step, Velopack's log said:

```
[INFO] Will update all current shortcuts: [..., (START_MENU, ...\Claude Desktop Launcher.lnk)]
[INFO] Updating existing shortcut '...\Start Menu\...\Claude Desktop Launcher.lnk' (START_MENU)
[INFO] Package applied successfully.
```

But after apply, the `.lnk` was gone. The parent folder (`...\Start Menu\Programs\ClaudeDesktopLauncher\`) remained, just empty. Desktop shortcut survived fine. This means: Windows Search cannot find the app, the user cannot launch via Start Menu.

**User-facing workaround** (one-liner, no admin needed):

```powershell
$exe = "$env:LOCALAPPDATA\ClaudeDesktopLauncher\current\ClaudeDesktopLauncher.exe"
$lnk = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\ClaudeDesktopLauncher\Claude Desktop Launcher.lnk"
$sh = New-Object -ComObject WScript.Shell
$s = $sh.CreateShortcut($lnk)
$s.TargetPath = $exe
$s.WorkingDirectory = Split-Path $exe
$s.Save()
```

**Upstream reporting**: worth filing a Velopack issue citing the log sequence. Until fixed, consider either (a) documenting the workaround in `docs/MIGRATION.md` for any user who hits it, or (b) having the launcher itself self-heal on startup - check for the shortcut, recreate if missing. Option (b) is more ambitious but would cover all users transparently.

**v1.10.2 update: option (b) shipped.** `IShortcutHealer` runs on every startup; if the Velopack install's Start Menu .lnk is missing it is recreated via the same `WScript.Shell` COM API Velopack itself uses. Dev-build guard skips the heal on `dotnet run`. Six unit tests cover every branch. Upstream issue still worth filing but users no longer hit the bug regardless of Velopack fix timeline.

**v1.10.3 update: icon cache refresh added.** Observed on the developer machine after v1.10.2 shipped: even after a successful heal, Windows' icon cache kept showing the generic document icon for the new shortcut until `explorer.exe` was manually restarted or the cache expired naturally. v1.10.3 adds an `IIconCacheRefresher` service invoked only on the `HealedMissing` branch. Calls `ie4uinit.exe -show`, Microsoft's documented icon-cache refresh utility (lighter than killing explorer.exe). Never fires on `AlreadyPresent` or `SkippedDevBuild` - those paths don't write anything new to disk.

### Watch out: MSIX-sandboxed PowerShell resolves `$env:LOCALAPPDATA` to a virtualized path

Learned while investigating the Start Menu shortcut bug above. When running PowerShell from inside an MSIX-sandboxed process (e.g. Claude Desktop's terminal), `$env:LOCALAPPDATA` resolves to the sandbox's redirected path:

```
$env:LOCALAPPDATA = %LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Local
```

not the real `%LOCALAPPDATA%`. If you construct paths from `$env:LOCALAPPDATA` (e.g. when manually rebuilding a shortcut), the resulting paths will be **virtualized paths that don't exist outside the sandbox**. Windows Shell won't be able to resolve them, the target won't be found, icons won't load.

Fix: construct paths from `$env:USERPROFILE` instead (`$env:USERPROFILE\AppData\Local\...`). `USERPROFILE` is not virtualized by the MSIX sandbox.

This doesn't affect the C# app at runtime - `Environment.GetFolderPath(SpecialFolder.LocalApplicationData)` resolves correctly in the installed launcher because the installed launcher itself isn't MSIX-sandboxed. Only matters when you're running *manual* PowerShell commands from a sandboxed shell to diagnose or fix launcher state.

### Startup update-check is noisy about old non-Velopack releases

Same v1.10.0 → v1.10.1 session surfaced: on startup the update check walks backwards through every v1.x GitHub release looking for `releases.win.json`, generating a trace exception for every release older than v1.10.0:

```
[Trace] System.ArgumentException: Could not find asset called 'releases.win.json' in GitHub Release 'v1.9.2'.
[Trace] System.ArgumentException: Could not find asset called 'releases.win.json' in GitHub Release 'v1.9.1'.
...
```

These are all pre-Velopack releases so the absence is correct. Velopack retries on each one until it hits a release that does have the manifest. Performance impact: ~1 second of extra startup time per launch and a lot of Trace-level noise in the Velopack log. Functional impact: none. Upstream should consider short-circuiting after the first missing-manifest result, or at minimum downgrading these to `Debug` level.

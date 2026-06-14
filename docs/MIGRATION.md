# Migrating from v1.9.x to v1.10.0

Starting with v1.10.0, Claude Desktop Launcher ships as an installer (`Setup.exe`) with Velopack-powered background auto-update instead of the single portable `.exe` used in v1.0 through v1.9.2. This is a one-time migration for existing users. Your settings, slot nicknames, and Claude login sessions all survive unchanged.

## What's changing

| Before (v1.9.x) | After (v1.10.0+) |
|-----------------|------------------|
| Single `ClaudeDesktopLauncher-vX.X.X-win-x64.exe` downloaded manually | `Setup.exe` installer downloaded once; future versions auto-update |
| You placed the .exe wherever you liked | Installed to `%LOCALAPPDATA%\ClaudeDesktopLauncher\current\` |
| Update banner told you to manually download the new .exe | Update banner tells you when a background-downloaded update is ready; one click restarts into it |
| No shortcuts | Desktop + Start Menu shortcuts created automatically |

## What stays the same

- **Your settings** live at `%APPDATA%\ClaudeDesktopLauncher\` (not inside the install dir), so slot count, nicknames, refresh interval, "Launch on startup" checkbox, and "Show thumbnails" preference all persist across the switch.
- **Your seed cache** at `%APPDATA%\ClaudeDesktopLauncher\seed\` persists too, so slots stay logged in without re-seeding.
- **ClaudeSlot{N}** data directories at `%LOCALAPPDATA%\ClaudeSlot1\`, `ClaudeSlot2\`, etc. are untouched — each slot's Claude login, conversation history, and installed extensions are preserved.

## Step-by-step migration

### 1. Download Setup.exe

Grab `ClaudeDesktopLauncher-win-Setup.exe` from the [v1.10.0 release page](https://github.com/LewisIsWorking/ClaudeDesktopLauncher/releases/latest).

### 2. Close the old launcher

Right-click the system tray icon → Quit. If you had it pinned to the taskbar or had a shortcut pointing at the old portable .exe, remove those now.

### 3. Run Setup.exe

Double-click it. Windows will show the blue "Windows protected your PC" SmartScreen dialog because the installer isn't code-signed. Click **More info** → **Run anyway**. The installer takes about 5 seconds, no UAC prompt needed (it installs per-user, not system-wide). The launcher auto-launches after install completes.

You'll see your existing slot count, nicknames, and all your settings exactly as you left them. If you previously had "Launch on startup" enabled, that toggle persists too — though you may need to toggle it off and on once to update the target path (startup entries pointed at your old portable .exe location and now need to point at `%LOCALAPPDATA%\ClaudeDesktopLauncher\current\ClaudeDesktopLauncher.exe`).

### 4. Delete the old portable .exe

Wherever you had it — `Downloads/`, `Desktop/`, `C:\Tools\`, `Dropbox/synced folder/`, wherever — just delete the file. The new install is entirely separate.

### 5. Verify auto-update is working

Open the launcher's settings row and confirm "Auto-update" is checked (it defaults on). From v1.10.1 onwards, when a new release ships, you'll see a blue "Downloading..." banner appear briefly, then a green "v1.10.X ready - restart to install" banner. Click "Restart to install" whenever convenient — preferably between Claude conversations rather than mid-chat.

If you prefer manual update control, un-tick "Auto-update" in the settings row. The launcher won't check GitHub; you can manually download new `Setup.exe` versions from Releases whenever you choose.

## Uninstalling

v1.10.0+ appears in Windows **Settings → Apps → Installed apps** as "Claude Desktop Launcher". Click Uninstall to remove it cleanly. Your settings at `%APPDATA%\ClaudeDesktopLauncher\` and your ClaudeSlot data are **not** deleted by the uninstaller — delete them manually if you want a fully clean removal.

## FAQ

**Q: Do I lose my slot logins?**
A: No. Slot logins live in `%LOCALAPPDATA%\ClaudeSlot{N}\`, entirely separate from the launcher install. The Velopack installer only touches `%LOCALAPPDATA%\ClaudeDesktopLauncher\`.

**Q: Can I still use the portable version?**
A: Yes — every release also ships `ClaudeDesktopLauncher-win-Portable.zip`. Extract anywhere, run the inner `.exe`. No auto-update though.

**Q: Why does Windows warn me about the installer?**
A: SmartScreen flags unsigned installers for new apps with limited reputation. Code signing costs money and introduces CI complexity; skipped for v1.10.0 to keep CoODL free. Tracked as a roadmap item — Azure Trusted Signing is the likely future choice.

**Q: Will the auto-update ever fail silently?**
A: If the check fails (no network, GitHub rate limit), you'll see an amber "Update check failed" banner with a "Retry now" button. If the download fails, same banner with retry. If the install step fails (disk full, permissions), the old version keeps running — next launch retries. Logs at `%APPDATA%\ClaudeDesktopLauncher\logs\launcher-YYYY-MM-DD.log` show every update-related event.

**Q: Can I roll back to an older version?**
A: Velopack doesn't provide a built-in rollback UI. To roll back: uninstall via Add/Remove Programs, then grab the older `Setup.exe` from the Releases page. Settings persist across the round-trip.

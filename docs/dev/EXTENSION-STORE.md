# CoODL - Shared extension store (design draft)

Status: **planning**, no code yet. Next backlog item after v1.10.2 ship (2026-04-20). Created during the planning conversation before implementation begins.

## The problem

Each CoODL slot keeps its own full copy of every Claude extension the user has installed there:

```
%LOCALAPPDATA%\ClaudeSlot1\Claude Extensions\<ext-id>\
%LOCALAPPDATA%\ClaudeSlot2\Claude Extensions\<ext-id>\
%LOCALAPPDATA%\ClaudeSlot3\Claude Extensions\<ext-id>\
...
```

For extensions like Desktop Commander (~139 MB per install after the full Puppeteer/Chromium bundle downloads), this scales linearly with slot count. Measured on Lewis's machine 2026-04-20:

| Slot | Extensions total |
|------|------------------|
| ClaudeSlot1 | 342 MB |
| ClaudeSlot2 | 210 MB |
| ClaudeSlot3 | 202 MB |
| ClaudeSlot4 | 151 MB |
| ClaudeSlot5 | 342 MB |
| **Total** | **1,248 MB** |

Of which only ~300 MB is unique extension payload; the rest is duplication. Installing one new big extension across 5 slots also means 5× download time and 5× disk I/O.

## Proposed solution

A single shared store at:

```
%LOCALAPPDATA%\ClaudeDesktopLauncher\ExtensionStore\<ext-id>\
```

With per-slot junction points (Windows reparse points, NTFS-native, no admin needed) replacing each slot's extension directory:

```
%LOCALAPPDATA%\ClaudeSlot1\Claude Extensions\<ext-id>\  --junction-->  ...\ExtensionStore\<ext-id>\
%LOCALAPPDATA%\ClaudeSlot2\Claude Extensions\<ext-id>\  --junction-->  ...\ExtensionStore\<ext-id>\
```

Claude sees a normal-looking directory; writes from one slot are visible in all. Verified 2026-04-20 that junction creation via `mklink /J` works without admin on Lewis's machine.

## Open design questions

These need decisions before implementation starts.

### 1. When does the launcher "adopt" a slot's extensions into the shared store?

Options:
- **a) On launcher startup** - walk every slot's `Claude Extensions` folder, any extension-dir that isn't already a junction gets moved into the shared store and replaced with a junction. Invisible to the user.
- **b) On Launch Claude click** - adopt just that slot's extensions before launching. Less work per startup, but the first click on a new slot has a latency hit.
- **c) Opt-in via a "Deduplicate extensions" button** - never automatic. Surfaces the feature but requires user engagement.
- **d) Opt-in per-extension via a list UI** - user chooses which extensions to share. More control, more UI.

My lean: **a + a settings toggle to disable**. Automatic dedup is the magic that makes this worthwhile; power users who want per-slot divergence can opt out.

### 2. How do we handle extensions that already differ between slots?

The 2026-04-20 scan found 4 slots with `ant.dir.gh.wonderwhy-er.desktopcommandermcp` installed: 3 at 139 MB and 1 at 7 MB (a partial/broken install). They can't all be the "canonical" shared version.

Options:
- **Newest file mtime wins** - simple, usually right, occasionally wrong.
- **Largest size wins** - biased toward "most complete" install. Good heuristic for extensions like DC where partial = broken.
- **Check for a known sentinel file** - read `package.json` version, compare. Most correct but tied to implementation details of the extension format.
- **Prompt the user** - "Slot 2's install of DC looks different - use Slot 1's version? [Yes / No / Keep separate]". Highest correctness, worst UX.

Realistic approach: use **largest-size** as default, emit a WARN log line naming every slot whose version was discarded so users can check the logs if something breaks.

### 3. How do we handle per-slot extension configuration?

Most Claude extensions store their runtime config in `claude_desktop_config.json` at the slot's root, NOT inside the extension dir. So sharing the dir shouldn't affect config.

BUT: some extensions cache data inside their own install directory (e.g. Desktop Commander may cache browser session state; Windows-MCP has `images/` for temporary screenshots). Sharing the install dir means slots fight over the cache.

Options:
- **a) Full dir sharing** - accept occasional cache conflicts. Desktop Commander is the main worry; verify behaviour.
- **b) Share static files, slot-local mutable folders** - redirect known-mutable subdirs (`cache/`, `images/`, `temp/`) to slot-local storage via additional junctions. Complex.
- **c) Share only the install dir's immediate files, subdirectories stay per-slot** - wrong for most extensions; most of the bulk IS in subdirs (node_modules/, Chromium binaries).

Realistic approach: start with **(a) full dir sharing** and add per-extension overrides if specific extensions prove to be affected. Document which subdirs to test.

### 4. What happens when Claude auto-updates an extension?

Claude's extension store flow:
1. User clicks "Update" (or extension is auto-updated silently)
2. Claude writes new files into the slot's extension directory
3. Extension reloads

If the extension dir is a junction, Claude's writes go into the shared store — which means **all slots get the update atomically**. This is usually what the user wants. Could surprise someone who deliberately held back an update in slot 2.

Need to verify: does Claude's updater **replace** the whole dir (which would break the junction) or **update files in place** (which would survive)? This requires experimentation before we rely on it.

### 5. What about uninstall?

If a user uninstalls an extension from slot 1, Claude deletes slot 1's extension dir — which is a junction. Deleting a junction removes the reparse point, not the target, so the shared store survives, but slot 1 loses the extension.

Good behaviour: uninstall in one slot removes it from that slot only.
Bad behaviour: the shared-store copy now has no slot pointing at it (orphan). Garbage-collect on next launcher startup (walk store, any dir with zero junction referrers → delete).

## Implementation sketch

Following the same patterns as `IShortcutHealer`:

### Services
- `IExtensionStoreManager` - the high-level orchestrator
  - `MigrateSlotIntoStore(int slotNumber)` - replaces extension dirs with junctions
  - `GarbageCollect()` - removes orphaned shared store entries
  - `IsMigrated(int slotNumber)` - idempotency check
- `IJunctionPointService` - thin wrapper over `mklink /J` via process launch or `CreateSymbolicLink` P/Invoke with `SYMBOLIC_LINK_FLAG_DIRECTORY | SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE`
- `IExtensionStoreResolver` - given an extension ID and the set of copies found across slots, pick the canonical one. Encapsulates the "largest size wins" heuristic so swapping strategies later is an interface change.

### Wiring
- Runs after `IShortcutHealer` in `App.OnFrameworkInitializationCompleted`
- Dev-build guard (skip when `Environment.ProcessPath` is in a `bin\Debug\` path)
- Settings toggle `AppSettings.ShareExtensionsAcrossSlots` (default ON in v1.11+)

### Tests
- `ExtensionStoreManagerTests` - drives the migration decision tree with mocked IO
- `JunctionPointServiceTests` - test the P/Invoke wrapper against real junctions in a temp dir
- `ExtensionStoreResolverTests` - drive the "which copy wins" rule with multiple sizes/timestamps

### Ship strategy
v1.11.0 — big feature, definitely not a hotfix release. Should include:
- Settings UI entry (a new checkbox next to the existing ones)
- A `--migrate-extensions` CLI flag for manual re-runs
- A log summary at startup: `[ExtensionStoreManager] 3 extensions shared across 5 slots, saved 940 MB`
- Full MIGRATION.md update explaining the behaviour

## Risks

- **Claude breaks the junction by replacing the dir during auto-update** - biggest unknown. Test before implementation.
- **Claude caches state inside the extension dir** - pollution across slots. Test specifically with Desktop Commander, Windows-MCP, Filesystem.
- **Junction point deletion** - `Remove-Item -Recurse` in PowerShell can follow junctions and delete through to the target. Defensive code must check `ReparsePoint` attribute before recursing.
- **Antivirus** - some AV flags junction creation as suspicious; spurious alerts on first migration.
- **Cross-slot sync during active use** - if slot 1 and slot 3 are both running and a shared extension writes temp files, what happens? Test.

## Next steps for the next session

1. **Empirical testing**: manually junction one extension (e.g. Filesystem, smallest at 12 MB) from slot 2 -> slot 1's copy. Confirm Claude still loads it, still runs tools through it, still lets the user uninstall it from slot 2 without breaking slot 1.
2. Answer the 5 design questions above.
3. Write the services, tests, ship as v1.11.0.

# Upstream Claude Desktop issues observed from CoODL

This document records issues CoODL has observed in Claude Desktop's
runtime that originate inside Claude Desktop itself (specifically,
inside `app.asar\.vite\build\index.pre.js` or Electron's own init
code). CoODL cannot patch them; they need fixes upstream. The document
exists so: (1) we don't waste time rediscovering them every release, (2)
when we do report them to Anthropic we have precise call sites and
reproduction steps ready, and (3) the rest of the LEARNINGS.md workflow
around "fix the root cause, never suppress" stays honest - these are
root causes that live outside our repo.

All four issues were captured in v1.8.3 development using `claude.exe
--trace-deprecation --user-data-dir=...` with stderr redirected into a
file. Line/column offsets refer to Claude Desktop v1.3109.0 and will
drift with future Claude builds.

## 1. DEP0040 - `punycode` module deprecation

**Message:**
```
(node:NNNN) [DEP0040] DeprecationWarning: The `punycode` module is
deprecated. Please use a userland alternative instead.
```

**Trace:**
```
at node:punycode:7:10
at BuiltinModule.compileForInternalLoader (node:internal/bootstrap/realm:398:7)
...
at Module.h (app.asar\.vite\build\index.pre.js:5:3370)
at _require.e.require (app.asar\.vite\build\index.pre.js:5:2341)
at Object.<anonymous> (app.asar\.vite\build\index.js:1:3095)
```

**Diagnosis:** a dependency bundled by Vite into `index.pre.js` calls
`require('punycode')` during app initialisation. Likely candidates
include `whatwg-url`, `tr46`, `url-parse`, or `psl` - all common URL/IDN
libraries that still reach for the Node built-in.

**Why it matters:** `punycode` is slated for removal in a future Node
major. When Claude Desktop's bundled Electron ships with that Node
version, startup will fail with `MODULE_NOT_FOUND` rather than a
deprecation warning. This is a ticking clock, not cosmetic.

**Suggested upstream fix:** audit deps that call `require('punycode')`,
bump to versions that use the userland `punycode` package from npm (or
switch to WHATWG URL / URLPattern APIs where applicable).

## 2. DEP0169 - `url.parse()` security deprecation

**Message:**
```
(node:NNNN) [DEP0169] DeprecationWarning: `url.parse()` behavior is not
standardized and prone to errors that have security implications. Use
the WHATWG URL API instead. CVEs are not issued for `url.parse()`
vulnerabilities.
```

**Trace:**
```
at Module.urlParse (node:url:136:13)
at Sle (app.asar\.vite\build\index.pre.js:28:8667)
at Object.request (app.asar\.vite\build\index.pre.js:28:9696)
```

**Diagnosis:** a minified function `Sle.request(...)` calls
`url.parse()` before making an HTTP request. The class looks like an
internal HTTP client wrapper; could be `axios`, `got`, `request`, or a
custom implementation.

**Why it matters:** Node explicitly states CVEs are NOT issued for
`url.parse` vulnerabilities. Any URL-parsing-related security issue in
this code path will never be fixed by Node - it must be fixed by moving
off `url.parse`. Keeping this call silently works today but is a
security footgun.

**Suggested upstream fix:** replace `url.parse(s)` with `new URL(s)` (or
`URL.canParse(s)` for validity checks).

## 3. BuddyBleTransport.reportState - no handler registered

**Message:**
```
Error occurred in handler for
'$eipc_message$_8e6f15c2-1794-4f6a-a9e4-7586203a8d91_$_claude.buddy_$_BuddyBleTransport_$_reportState':
Error: No handler registered for
'$eipc_message$_8e6f15c2-1794-4f6a-a9e4-7586203a8d91_$_claude.buddy_$_BuddyBleTransport_$_reportState'
```

**Trace:**
```
at Session.<anonymous> (node:electron/js2c/browser_init:2:116575)
at Session.emit (node:events:508:28)
```

**Diagnosis:** the renderer process is emitting an IPC message on
channel `claude.buddy_$_BuddyBleTransport_$_reportState` (Bluetooth Low
Energy transport for the Claude buddy / companion feature), but the
main process has no handler registered for it. The error surfaces at
Electron's `browser_init.js` IPC routing layer; the missing
registration lives in Claude's own code and we cannot see exactly
where from our trace.

**Hypothesis:** timing-related. Either the renderer is sending before
main finishes initialising the BLE module, OR the BLE feature is
disabled / not applicable on this hardware so the handler never gets
registered but the renderer still emits.

**Why it matters:** surfaces as an error in stderr on every single
launch. Either the feature is silently broken (and users don't get BLE
buddy connectivity) or the error is expected-but-noisy (in which case
Claude should silently return `null` / a "not supported" result rather
than logging an error).

**Suggested upstream fix:** register a default no-op / "not-supported"
handler for `BuddyBleTransport.reportState` so the renderer's call
succeeds cleanly even when BLE isn't available.

## 4. MaxListenersExceededWarning on `[RAr]`

**Message:**
```
(node:NNNN) MaxListenersExceededWarning: Possible EventEmitter memory
leak detected. 11 change listeners added to [RAr]. MaxListeners is 10.
Use emitter.setMaxListeners() to increase limit.
```

**Diagnosis:** minified class `RAr` is an EventEmitter subclass (Node's
default listener limit is 10). Either code is adding listeners without
removing them on teardown, or a legitimate use case needs >10 listeners
and should explicitly raise the limit.

**Why it matters:** could indicate a real memory leak - listeners hold
references to their closures. If this fires early in startup we see
maybe one warning per slot launch, but over time, listener count will
grow if the underlying cause is a leak.

**Suggested upstream fix:** audit listeners on `RAr`. If legitimate,
call `emitter.setMaxListeners(N)` with a higher explicit limit. If a
leak, pair each `.on(...)` with a matching `.off(...)` in teardown.

## How CoODL handles these today (v1.8.3)

CoODL's `IProcessService.StartWithStderrPipe` redirects Claude's stderr
and pipes each line into `launcher-YYYY-MM-DD.log` tagged `[claude slot
N stderr]`. This preserves every warning as a reviewable diagnostic
entry rather than losing it (production: no console attached) or
polluting the dev terminal (dev: clutter). The warnings are not
suppressed - they are *routed* to a location we control, leaving the
original stderr stream clear for future tooling and production runs
silent as users expect.

See `ClaudeDesktopLauncher.Core/Services/SystemProcessService.cs`
and `ClaudeInstanceLauncher.LaunchSlot` for the implementation.

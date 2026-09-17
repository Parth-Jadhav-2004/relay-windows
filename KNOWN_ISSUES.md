# Known Issues and Bugs

> Read-only audit snapshot (2026-09-17). No code was modified.
> Note: uncommitted changes by another agent (`Theme.cs`, `SettingsWindow.*`) were not reviewed.

---

## Top-priority bugs

| # | Issue | Location |
|---|-------|----------|
| 1 | Failed export destroys the previous backup — existing archive is deleted before the new one is written; a mid-write failure leaves nothing. Write to temp + replace. | `src/Relay.Core/Features/Backup/BackupArchive.cs:26-28` |
| 2 | FTS index corruption — backing row is deleted/updated *before* deleting from the external-content FTS table (wrong order for FTS); `SetOcr` indexes a value that differs from the backing column. SQLite errors are swallowed, hiding the problem. | `src/Relay.Core/Features/Clipboard/ClipboardStore.cs:199-233` |
| 3 | Settings writes are not atomic — `File.WriteAllText` directly; a crash mid-write corrupts settings. `Load` silently resets the entire file on any `JsonException` or JSON `null`, with no backup of the old file. Only `JsonException` is caught; `IOException`/`UnauthorizedAccessException` crash the caller. | `src/Relay/Platform/SettingsStore.cs:24-36` |
| 4 | WindowProcedureHook is GC/crash-prone — if the instance is GC'd while still subclassed (caller missed `Dispose`), the collected delegate is invoked → crash. `Dispose` restores `_previous` unconditionally without verifying the hook chain, so nested/overlapping hooks corrupt each other. No check that `SetWindowLongPtr` succeeded. | `src/Relay/Platform/WindowProcedureHook.cs:30-37` |
| 5 | Command injection via AI binary path — `cmd.exe /c "..."` built from a user-configured path/args without escaping (`&`, `|`, `%VAR%` execute). Related: child server process leaks on health-check failure; stderr is redirected but never drained (pipe-full deadlock in `ProbeCliVersionAsync`); `AcquireAsync`'s finally calls `_gate.Release()` with no disposed-gate guard → `ObjectDisposedException`. | `src/Relay/Platform/OpenCodeServerManager.cs:160-181, 215, 353` · `src/Relay/Platform/OpenCodeServerOwner.cs:92` |
| 6 | Null JSON breaks startup/searches — `LauncherRanking` dereferences deserialized records without null checks; `Load()` doesn't catch → startup failure. `SettingsSnapshot` does the same during backup import, mutating settings before validating, so a null value applies partial settings then throws. Imported ranking counts bypass `Cap` and can overflow `Sum`. | `src/Relay.Core/Features/Launcher/LauncherRanking.cs:88-99, 131-166` · `src/Relay.Core/Features/Settings/SettingsSnapshot.cs:102-104` |
| 7 | Quitting apps kills save prompts — after WM_CLOSE the code waits only 800 ms before killing the whole process tree, including apps showing save dialogs. Matching uses the shortcut filename instead of the resolved exe, so quit/restart often targets nothing or the wrong process. `Process` objects are never disposed. | `src/Relay/Platform/AppProcess.cs:30-67` |

---

## Calculator

### Crashes
- `monday in 2000000000 weeks` — `int.TryParse` passes, `7 * weeks` overflows, `AddDays` throws `ArgumentOutOfRangeException` which escapes the engine (the parser path is wrapped, the date path is not). — `CalcDateTime.cs:191-199` (bare call at `CalcEngine.cs:46`)
- `now + 9999999999 hours` — `int.Parse` overflow → uncaught `FormatException`. Regex `^\d+$` doesn't bound the value. — `CalcDateTime.cs:369-378`
- `0xFFFFFFFFFFFFFFFFF` (> Int64) — `Convert.ToInt64` throws, is swallowed, `i` is not advanced, the whole expression tokenizes to null instead of degrading to decimal or a clear error. — `CalcTokenizer.cs:99-119`

### Correctness
- No hour/minute validation in time-zone source moments: `9am in 25:99 to london` silently rolls over. — `CalcTimeZone.cs:147-154` (cf. `CalcDateTime.TryParseClock:354` which does validate)
- Negative-millisecond timestamps truncate toward zero (`-1500 ms` → `-1 s`); should be floor division. — `CalcDateTime.cs:214-216`
- `50% == 0.5` fails (percent only intercepted for `+ - * /`); `==`/`!=` use an absolute epsilon `1e-9`, wrong for large magnitudes. — `CalcParser.cs:784-795`
- `1e19 to hex` silently wraps via unchecked `(long)Math.Round` — no `ExactInteger` guard. — `CalcEngine.cs:334`
- `ratio of 99999999999999999999 to 1` — unbounded `\d+` regex; out-of-range double → `(long)` cast yields `long.MinValue`. — `CalcPercent.cs:38-39`
- Wall-clock arithmetic across DST boundaries is off by an hour (`+8 hours` over a spring-forward night done in wall-clock space); `diff` offsets computed at `context.Now` ignore DST at the user-supplied time. — `CalcTimeZone.cs:83-106`
- `Bias.Future` vs `Nearest` snap-to semantics differ between bias paths. — `CalcDateTime.cs:357-360`
- `BaseUnit` iterates `Dictionary.Values` — insertion-order-dependent choice of base unit per dimension. — `CalcUnits.cs:113-128`
- `CalcEngine.cs:36` mixes `CultureInfo.CurrentCulture.FirstDayOfWeek` with invariant-culture weekday names — inconsistent locale handling.
- Fallback source currency hardcodes USD when region unknown. — `CalcParser.cs:129`

### Parser/tokenizer internal issues (work by accident, fragile)
- Dead ternary `rightAssoc ? rbp : rbp`; hardcoded prefix/postfix binding powers disagree with the `Binding` table. — `CalcParser.cs:214-217, 291, 360`
- Dead `IEEERemainder` assignment immediately overwritten; unreachable shift-guard branch. — `CalcMath.cs:141-142, 206-224`
- `FoldLoneX` can never fire (`2x3` tokenizes as ident `x3`); implicit `x` only works with spaces. — `CalcTokenizer.cs:85, 240`
- `1,23` silently parses as a function-call argument list instead of being rejected. — `CalcTokenizer.cs:139-153`
- `sum of 1, abc, 3` silently drops non-numeric entries → 4; `Where(double.IsFinite)` hides NaN. — `CalcPercent.cs:52-54`
- Bare `catch (Exception)` in the parser swallows all bugs into a silent null. — `CalcParser.cs:53-56`
- `1e308k` can push a finite token to `Infinity` past the finiteness check. — `CalcTokenizer.cs:186-199`
- Floating error can render "1 foot 12 inches" (no carry). — `CalcFormatter.cs:128-137`

---

## Snippets

- `visited` is never removed on backtracking, so `{snippet:a} {snippet:a}` renders the second occurrence literally. — `SnippetTemplateEngine.cs:138`
- Nested braces unsupported: `{argument {x}}` terminates at the first `}` and re-emits the remainder verbatim; no escape for literal `{`. — `SnippetTemplateEngine.cs:79-90`
- Default-value regex `[^\s}]+` disagrees with declaration regex `[^}]*` — `default=hello world` parses differently depending on entry point. — `SnippetTemplateEngine.cs:151, 40`

---

## Launcher

- Weeks spanning New Year are misclassified: "This Week" requires matching calendar years instead of comparing against the current week's start. — `src/Relay.Core/Features/Clipboard/DateBucket.cs:29-33`
- Only the first substring occurrence is classified: `bar` in `foobar bar` gets the lower `Substring` tier even though a later word-start match qualifies. — `src/Relay.Core/Features/Launcher/SearchRelevance.cs:76-80`
- Favorites/aliases/visibility dictionaries are `OrdinalIgnoreCase` at runtime but default (case-sensitive) after deserialization — casing behavior changes across restart. — `LauncherRanking.cs:202-205`
- "Apps" and "Installed apps" share one URI → launcher derives the same ID → duplicate rows sharing aliases, visibility, and usage history. — `src/Relay.Core/Features/Launcher/MsSettingsCatalog.cs:23-24`
- Hotkey labels cast virtual-key codes directly to chars: F1 shows as `p`; arrows/Enter/OEM keys wrong. — `src/Relay.Core/Features/HotKeys/HotKeys.cs:97`
- Global vs per-app hotkey conflicts on the same chord are not detected. — `HotKeys.cs:124-129`
- `https://` alone passes scheme validation without `Uri.TryCreate`. — `src/Relay.Core/Features/Launcher/FallbackCatalog.cs:70-73`
- Bracket glob `[...]` patterns are classified as globs but brackets are escaped, so character classes silently match literal brackets. — `src/Relay.Core/Features/FileSearch/FileSearch.cs:108-132`

---

## Clipboard

- The 80-row truncation happens *before* type filtering — older images/files vanish from filtered views even though they exist in history. — `src/Relay/Features/Clipboard/ClipboardCoordinator.cs:27-29`
- Image ownership check `StartsWith(root)` lacks a directory-boundary check; sibling directories whose names begin with the root name pass. — `src/Relay.Core/Features/Clipboard/ClipboardStore.cs:258-262`
- Literal search inserts user text into SQL `LIKE` without escaping `%` and `_`. — `ClipboardStore.cs:154-157`
- Backup import replaces the store without draining/cancelling outstanding OCR — a stale item ID can attach one image's OCR text to another. — `src/Relay/App/AppCore.cs:589-598`
- Image/file copy-paste is fire-and-forget: no exception handling, success reported before completion, captured directories rejected by `File.Exists` on paste. — `ClipboardCoordinator.cs:68-90, 155-172`
- Static thumbnail cache never evicts; deleted images stay rooted in memory; memory streams never disposed; decode blocks the UI thread. — `src/Relay/Features/Clipboard/ClipboardThumbnails.cs:8-29`
- Legacy icon fallback detects visible RGB content but never repairs zero alpha bytes → invisible PNG. — `src/Relay/Platform/ShellIcons.cs:354-405`
- `https://` alone classified as a link. — `src/Relay.Core/Features/Clipboard/ClipboardListFilter.cs:55-58`

---

## Window management

- Half-placement branches use the raw `input.Gap` instead of the sanitized one — negative/excessive gaps produce off-screen or collapsed windows; display cycling doesn't sanitize against the destination screen. — `src/Relay.Core/Features/WindowManagement/WindowPlacement.cs:157-163, 321-336`
- Layout restore re-selects the first window with a matching process name instead of consuming matches — multiple windows of one app: the first moves repeatedly, the rest untouched. — `src/Relay/App/AppCore.cs:779-784`

---

## Dialogs / HUD

- A missing dispatcher or rejected `TryEnqueue` leaves `_pending` stuck forever; all subsequent confirmations are then auto-cancelled. Dialog construction failures have no cleanup path. — `src/Relay/Windows/Dialog/DialogPresenter.cs:20-30`

---

## Platform / Win32 interop

- Tray icon disappears permanently when Explorer restarts — no `TaskbarCreated` message handling; `Add()` sets `_added = true` even when `Shell_NotifyIcon` fails. — `src/Relay/Platform/TrayIcon.cs`
- `Process.GetProcessById` per keypress inside the low-level keyboard hook (`AppAllowed`) — hook-removal risk under load, `Process` objects never disposed → handle leak per keypress. — `src/Relay/Platform/HotKeyCenter.cs:245, 308-318`
- Snippet `_typed` buffer isn't reset on window focus change — typing in one app can complete a snippet in another. — `HotKeyCenter.cs`
- `RegisterHotKey` failure silently falls back to the global hook path without notice. — `HotKeyCenter.cs:61-66`
- `GetMonitorInfo` P/Invoke likely throws `EntryPointNotFoundException` (user32 exports only `GetMonitorInfoW`). — `src/Relay/Platform/NativeMethods.Win32.cs:160-166`
- `SendInput` P/Invoke lacks `SetLastError = true`, so logged error codes are stale; `SendUnicode` sends surrogate pairs as independent down/up pairs; partial `SendInput` failure drops the rest silently. — `NativeMethods.Win32.cs:203-218`
- Several `Marshal.GetLastWin32Error` reads after P/Invokes without `SetLastError = true` (`Shell_NotifyIcon`, `LoadImage`, `SetWindowLongPtr`) — misleading diagnostics.
- Multiple silent `catch (Exception) { }` with no logging (`AppIndex.cs:85,114,244,255`; `FileSearchService.cs:24,51`; `OpenCodeServerManager.cs:75,84,133`).

---

## Updates

- Non-JSON update responses (proxy error pages, HTML) throw uncaught `JsonException`. — `src/Relay/Platform/UpdatesClient.cs:60`
- Staging-dir delete can throw if a previous `apply.cmd` still holds files — unhandled. — `UpdatesClient.cs:88-91`
- The `tasklist`/`findstr` wait loop in apply.cmd can spin forever if any other `Relay.exe` exists; no max-wait. — `UpdatesClient.cs:125-151`
- No hash/signature verification of the zip before robocopying it over the running install dir. — `UpdatesClient.cs:104-105`
- `cmd.exe` spawn failure in the apply path is unvalidated — the update silently never applies. — `UpdatesClient.cs:153-160`

---

## AI / network

- `JsonDocument.Parse` on network/server bodies without try/catch in several spots → raw `JsonException` instead of typed errors. — `src/Relay/Platform/OpenCodeClient.cs:44, 92, 126, 234` · `src/Relay/Platform/CloudServices.cs:89`
- New `HttpClient` per call → socket churn / TIME_WAIT accumulation under repeated use. — `OpenCodeClient.cs:75` · `CloudServices.cs:74` · `UpdatesClient.cs:169-179`
- Calendar errors swallowed as "no meetings" — user can't distinguish "no permission" from "none". — `CloudServices.cs:109-125`
- All-day appointments with null `LocalId` get a new random ID per refresh → duplicate events. — `CloudServices.cs:117`
- TOCTOU port race in `FindAvailablePort` — port released before the child binds it. — `src/Relay/Platform/OpenCodeServerManager.cs:41-48`
- `EnableRaisingEvents` set after handlers attach; a process that exits before then never completes the `exited` task (only the timeout path recovers). — `OpenCodeServerManager.cs:313-324`
- `Kill(proc)` can dispose `proc` while the `Exited` handler still references it. — `OpenCodeServerManager.cs:333`
- Empty/missing `apiKey` produces a `"Bearer "` header → confusing 401s. — `OpenCodeClient.cs:75`

---

## Secrets / security

- GitHub token written to a plaintext `.env` in `%APPDATA%` with no ACL hardening — readable by any process running as the user. — `src/Relay/Platform/GitHubAuth.cs:94-96`
- `MigrateVault` wipes the vault credential even if the env file already held a *different* token; re-entry required. — `GitHubAuth.cs:49-61`
- Archive imports have no per-entry/total-size/count limits — an oversized or highly compressed archive can exhaust memory/disk during import. — `BackupArchive.cs:83-118`
- Capability flags never carried by backup (consent keys) — confirm coverage in `SettingsBackupCoverage.DeliberatelyExcluded` remains complete as new flags are added.

---

## Minor

- `.env` parsing: `KEY="value" # comment` keeps the quotes; a `#` inside a quoted value can be mistaken for a comment. — `src/Relay.Core/Features/Updates/DotEnv.cs:43-48`
- Fallback source currency hardcodes USD. — `CalcParser.cs:129`
- `AppIndex` calls `GetAppListEntriesAsync().GetAwaiter().GetResult()` (sync-over-async on possibly STA) in a per-package loop — slow startup, deadlock risk. — `src/Relay/Platform/AppIndex.cs:84, 243`
- Foreground-process lookup: `Process` objects never disposed; elevated targets silently make per-app hotkeys inert. — `AppIndex.cs:366-368`
- `explorer /select` argument building assumes no quotes in path (acceptable). — `src/Relay/Platform/FileSearchService.cs:215-220`
- `SHGetImageList` via ordinal `#727` with no HRESULT check → NRE possible at call site. — `src/Relay/Platform/NativeMethods.Shell.cs:57-60`
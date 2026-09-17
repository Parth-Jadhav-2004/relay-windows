# Relay macOS feature catalog — Windows port parity

Source: [abue-ammar/relay](https://github.com/abue-ammar/relay) `main`, fetched 2026-09-16 from GitHub raw + Contents API.

This is a planning catalog for a 1:1 native Windows port. Every feature with a `docs/features/*.md` page or a `Relay/Features/` folder is covered, including ones the README omits (camera, MCP, menu search, window layouts, updates, uninstall, support, onboarding, text injection).

**README drift:** the public README lists **34** Rectangle-style window actions. Shipped code (`WindowCommand.ID.allCases`) and `docs/features/window-management.md` both say **35**. This catalog uses 35.

**Parity:** Direct = same user job with a first-party Windows API. Adapt = same job, different OS mechanism. Drop = macOS-only; no honest native equivalent.

---

## Inventory

### Feature docs (`docs/features/`, 28 files)

`ai.md` · `apple-shortcuts.md` · `backup.md` · `calculator.md` · `calendar.md` · `camera.md` · `clipboard.md` · `custom-commands.md` · `emoji.md` · `extensions.md` · `file-search.md` · `hotkeys.md` · `launcher.md` · `mcp.md` · `menu-search.md` · `navigation.md` · `notes.md` · `palette.md` · `quick-actions.md` · `quicklinks.md` · `raycast-import.md` · `snippets.md` · `support.md` · `uninstall.md` · `updates.md` · `window-layouts.md` · `window-management.md`

There is **no** dedicated `system-actions.md`; the catalog lives in `Relay/Features/SystemActions/Model/SystemAction.swift` and is summarized in `launcher.md` § System actions.

### Source folders (`Relay/Features/`)

| Folder | Doc |
| --- | --- |
| `AI` | `ai.md` |
| `AppleShortcuts` | `apple-shortcuts.md` |
| `Backup` | `backup.md`, `raycast-import.md` |
| `Calculator` | `calculator.md` |
| `Calendar` | `calendar.md` |
| `Camera` | `camera.md` |
| `Clipboard` | `clipboard.md` |
| `CustomCommands` | `custom-commands.md` |
| `Emoji` | `emoji.md` |
| `Extensions` | `extensions.md` |
| `FileSearch` | `file-search.md` |
| `HotKeys` | `hotkeys.md` |
| `Launcher` | `launcher.md` |
| `MCP` | `mcp.md` |
| `MenuSearch` | `menu-search.md` |
| `Notes` | `notes.md` |
| `Onboarding` | (no feature doc; first-launch wizard) |
| `PaletteRowIndex.swift` | `palette.md` |
| `QuickActions` | `quick-actions.md` |
| `Quicklinks` | `quicklinks.md` |
| `Settings` | (settings shell; panes listed below) |
| `Snippets` | `snippets.md` |
| `Support` | `support.md` |
| `SystemActions` | `launcher.md` |
| `TextInjection` | `snippets.md`, `quick-actions.md` |
| `Uninstall` | `uninstall.md` |
| `Updates` | `updates.md` |
| `WindowManagement` | `window-management.md`, `window-layouts.md` |
| `WindowSwitcher` | `navigation.md` |

Palette UI lives in `Relay/Palette/`, documented by `palette.md`.

### Settings panes (`SettingsTab`)

**General:** General, Permissions.
**Launcher:** Applications, System Settings, System Actions, Commands, Quicklinks, Apple Shortcuts, Fallbacks.
**Features:** AI, Quick Actions, File Search, Notes, Snippets, Navigation, Window Management, Clipboard, Emoji & Symbols, Calendar, Extensions.
**Advanced:** Backup, About.

---

# Catalogs (extracts)

## System actions — full list (32)

From `SystemAction.ID`. Confirmation from `SystemActionCatalog.confirmation`.

| ID | Name | Confirms | Windows equivalent |
| --- | --- | --- | --- |
| `lock-screen` | Lock Screen | no | Direct — `LockWorkStation` (`user32`) |
| `sleep` | Sleep | no | Direct — `SetSuspendState` / `PowerCreateRequest` |
| `sleep-displays` | Sleep Displays | no | Adapt — `SendMessage(HWND_BROADCAST, WM_SYSCOMMAND, SC_MONITORPOWER, 2)` |
| `restart` | Restart | yes | Direct — `InitiateSystemShutdownEx` / `ExitWindowsEx(EWX_REBOOT)` (needs `SeShutdownPrivilege`) |
| `shut-down` | Shut Down | yes | Direct — `EWX_SHUTDOWN` / `EWX_POWEROFF` |
| `log-out` | Log Out | yes | Direct — `ExitWindowsEx(EWX_LOGOFF)` |
| `show-screen-saver` | Show Screen Saver | no | Direct — `SC_SCREENSAVE` |
| `play-pause` | Play / Pause | no | Direct — `keybd_event` / `SendInput` `VK_MEDIA_PLAY_PAUSE` |
| `next-track` | Next Track | no | Direct — `VK_MEDIA_NEXT_TRACK` |
| `previous-track` | Previous Track | no | Direct — `VK_MEDIA_PREV_TRACK` |
| `toggle-mute` | Toggle Mute | no | Direct — CoreAudio analog is `IAudioEndpointVolume.SetMute` |
| `volume-up` | Turn Volume Up | no | Direct — `IAudioEndpointVolume` 5% grid (`VolumeLevel.stepped`) |
| `volume-down` | Turn Volume Down | no | Direct — same |
| `set-volume` | Set Volume… | dialog slider | Direct — same + custom dialog |
| `volume-0` … `volume-100` | Set Volume to 0/25/50/75/100% | no | Direct |
| `show-desktop` | Show Desktop | no | Direct — `IVirtualDesktopManager` / `COM` `{3080F90D-D7AD-11D9-BD98-0000947B0257}` (toggle desktop) or `Shell.Application.ToggleDesktop` |
| `toggle-system-appearance` | Toggle System Appearance | no | Adapt — `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize` `AppsUseLightTheme` + `SystemUsesLightTheme` |
| `toggle-stage-manager` | Toggle Stage Manager | no | **Drop** — no Stage Manager. Closest: Snap layouts / Task View, not a toggle. |
| `open-trash` | Open Trash | no | Adapt — open Recycle Bin (`shell:RecycleBinFolder`) |
| `empty-trash` | Empty Trash | yes | Adapt — `SHEmptyRecycleBin` |
| `eject-all-disks` | Eject All Disks | no | Adapt — `CM_Request_Device_Eject` / `DeviceIoControl(IOCTL_STORAGE_EJECT_MEDIA)` on removable volumes; skip system/network |
| `toggle-hidden-files` | Toggle Hidden Files | no | Adapt — `HKCU\...\Explorer\Advanced` `Hidden` + `ShowSuperHidden`; broadcast `WM_SETTINGCHANGE` |
| `hide-all-apps-except-frontmost` | Hide All Apps Except Frontmost | no | Adapt — `ShowWindow(SW_MINIMIZE)` / `SW_HIDE` on other top-level windows of other processes (Windows has no app-hide) |
| `unhide-all-hidden-apps` | Unhide All Hidden Apps | no | Adapt — restore previously minimized; no OS “hidden apps” set |
| `quit-all-apps` | Quit All Applications | computed confirm | Adapt — enumerate visible apps via `EnumWindows` / `IApplicationActivationManager`; `WM_CLOSE` (graceful), never `TerminateProcess` by default; exclude Explorer and self |
| `dismiss-notifications` | Dismiss Notifications | no | Adapt — Windows Notification Center has no public “clear all” API; UIA against Action Center is fragile. High risk. |
| `toggle-bluetooth` | Toggle Bluetooth | no | Adapt — WinRT `Windows.Devices.Radios.Radio` (needs radio access capability) |

Confirm copy: Restart / Shut Down / Log Out share “Applications with unsaved changes may ask you to save.” Empty Trash: “The items in the Trash will be permanently deleted.” Quit All builds the count at call time.

Volume HUD: Relay draws its own because macOS only draws one for real media keys. Windows also has no in-app volume HUD for programmatic changes — keep a custom HUD.

## Window management actions — full list (35)

From `WindowCommand.ID`. Groups from `WindowCommand.Group`.

### Halves (cycle on repeat)

| ID | Name |
| --- | --- |
| `left-half` | Left Half |
| `right-half` | Right Half |
| `top-half` | Top Half |
| `bottom-half` | Bottom Half |

Cycle modes (`WindowCycle`, default `.off`): `.sizes` = ½ → ⅓ → ⅔ in place (top/bottom use vertical thirds); `.displays` = walk every display’s two half-slots L→R, wrapping.

### Quarters

`top-left-quarter` · `top-right-quarter` · `bottom-left-quarter` · `bottom-right-quarter`

### Fourths

`first-three-fourths` · `last-three-fourths`

### Thirds

`first-third` · `center-third` · `last-third` · `first-two-thirds` · `last-two-thirds`

### Sizing

| ID | Name | Notes |
| --- | --- | --- |
| `maximize` | Maximize | AX tile to `visibleFrame` (not native fullscreen) |
| `almost-maximize` | Almost Maximize | inset canvas |
| `reasonable-size` | Reasonable Size | 60% of canvas, centred, cap 1025×900 pt, idempotent |
| `maximize-height` | Maximize Height | keep X |
| `maximize-width` | Maximize Width | keep Y |
| `center` | Center | |
| `center-half` | Center Half | half width, full height, centred |
| `center-two-thirds` | Center Two Thirds | two-thirds width, full height, centred |
| `make-larger` | Make Larger | +5% of **screen**, invertible with smaller; floor `max(200×150, 15% canvas)` |
| `make-smaller` | Make Smaller | −5% of screen |
| `restore` | Restore Window | single-level restore of pre-Relay frame |

### Moving (size untouched)

`move-left` · `move-right` · `move-up` · `move-down` · `next-display` · `previous-display`

### Fullscreen

`toggle-fullscreen` — undocumented `AXFullScreen`, fallback `AXFullScreenButton`. No synthetic ⌃⌘F.

### Spaces

`previous-space` · `next-space` — synthetic Dock trackpad swipe via `CGEvent` (not a window move). macOS 27 splices IOHID payload field 4205.

**Windows mapping:** geometry/sizing/moving/display = **Direct** (`SetWindowPos`, `MonitorFromWindow`, work area via `GetMonitorInfo.rcWork`). Native maximize vs AX “maximize” must be a product choice (Windows Maximize is OS chrome). Toggle fullscreen = **Adapt** (`WS_POPUP` + monitor rect, or `ShowWindow(SW_MAXIMIZE)`). Spaces = **Adapt** (`IVirtualDesktopManager` / undocumented COM `IVirtualDesktopManagerInternal` — the latter is version-fragile). Cycle memory = portable.

## Calculator capabilities

Evaluation order (`CalcEngine.evaluate`):

1. Natural-language date/time (`CalcDateTime`)
2. Time zones (`CalcTimeZone`) — before tokenize
3. Tokenize; keep complete prefix of a trailing binary operator
4. Base conversion
5. Typed quantity arithmetic
6. Explicit unit conversion
7. Currency / crypto conversion
8. Bare-unit auto-conversion
9. Natural-language percent / ratio / list (`CalcPercent`)

**Date/time grammars A–G:** `hrs till 9am`; `days since 9jul`; `today + 3 weeks`; `jul 4 - today`; `5 weekdays from now`; `monday in 3 weeks`; `tomorrow at 9am` / `next monday`. Dotted dates are day-first; slashed are month-first. Two-digit years 00–68 → 2000s, 69–99 → 1900s. `workdays` = 8 hours as a unit; as date arithmetic skips weekends only (no holidays, never EventKit). `now to unix` / `unix 0 to date`; RFC 3339.

**Math:** precedence-climbing `CalcExpressionParser`. Functions: `sqrt`, `cbrt`, spoken `square root of` / `cube root of`, `exp`, `log` (base 10) / `log(x,b)` / `log2`, `sign`, `trunc`, `hypot`, `round(x)` / `round(x,n)` / `round 47 to nearest 5`, `pow`, `root`, `gcd`, `lcm`, `atan2`, `min`/`max`/`sum`/`avg`/`mean`/`average` (lists, including measurements). Trig + reciprocal (`sin`…`csc`) + inverse + hyperbolic. Constants `pi`, `tau`, `phi`, `e`. Comparisons `== != < <= > >=`. Integer `& | xor ~ << >>`; `^` is exponentiation. `mod` is spelled ( `%` is percent). Implicit `*` via juxtaposition and lone `x`/`×`. Scientific `1e5`. Thousands suffix `10k`.

**Units:** 150 base defs → 679 aliases after SI/transfer prefixes. Dimensions: length, mass, time, data, current, pixel, currency. Compounds: area, volume, flow, speed, acceleration, force, pressure, energy, power, frequency, data rates, electrical (`12V * 2A` → `24 W`), `px`/`ppi`/`rem`/`em` (16px root). `pt` = pints. Transfer: `MB/s` vs `Mbps`. Customary: tonnes, stone, nmi, hp, BTU, rpm, lbf, US/UK tons, UK liquid. Last unit typed wins for `+`/`-`. `to timespan`/`to duration`. Affine temperatures only same-scale add/sub.

**Currency:** generated from live fiat feed + CLDR (159 fiat codes). Hand tables: `contested` nouns, `isoNames`, `signCodes`, `crypto` (product list, also the fetch request). Region currency from `Locale.current.currency` (no location permission). Rates: cacheless session, `~/Library/Caches/<bundle>/currency-rates.json`, 24h refresh; coins best-effort 30 min retry. Offline uses last snapshot.

**Time zones:** `TimeZone.knownTimeZoneIdentifiers` city index + hand aliases (nicknames, IATA, renamed zones) + generated countries. `time in Tokyo`, `5pm ldn in sf`, `diff paris`. No geocoding.

**Percent phrases:** `20% off 500`, `15% tip on 42`, `50 as % of 200`, `50 is what % of 200`, `30 is 20% of what`, `ratio of 1920 to 1080`, `average of 10, 20, 30`.

**UI:** result card pinned at index 0; ↵ copies + records `CalculatorHistoryStore`. Empty query never cards a bare number/app name.

**Parity:** **Direct** (pure Foundation engine ports as-is). FX fetch is HTTPS. No OS calendar grant.

## Snippet placeholder syntax

Shared engine: `SnippetTemplateEngine` (also used by quicklinks). Raycast-compatible.

| Token | Result |
| --- | --- |
| `{clipboard}` | captured plain-text clipboard |
| `{clipboard offset=N}` | Nth previous text clip (`offset=1` = previous) |
| `{selection}` / `{selectedText}` | AX-read selection; canonical is `{selection}` |
| `{date}` `{time}` `{datetime}` | locale date/time |
| `{day}` | weekday name |
| `{uuid}` | fresh UUID per token |
| `{date format="yyyy-MM-dd"}` | `DateFormatter` pattern (quotes only needed for `\|`) |
| `{date locale="fr-FR"}` | cannot combine with `format` |
| `{time offset="+3h +30m"}` | signed offsets: `m` `h` `d` `M` `y` |
| `{argument}` / `{query}` | argument named Argument; canonical `{argument}` |
| `{argument name="Recipient"}` | named, prompted in order |
| `{argument default="Hi"}` | optional, no prompt |
| `{argument options="a, b, c"}` | picker |
| `{snippet:Name}` / `{snippet name="Name"}` | nested snippet, 5 levels, cycle-safe |
| `{cursor}` | final caret; first wins |

Modifiers (left to right): `uppercase` · `lowercase` · `trim` · `percent-encode` · `json-stringify` · `raw`.

Not supported: `{browser-tab}`, `{calculator}`. Unknown tokens left literal.

Frontmatter (Markdown files):

```markdown
---
name: "Meeting Notes"
keyword: "!notes"
enabled: true
show_confirmation: false
---
Template body
```

## Quicklink placeholders

Same engine. `{cursor}` and `{snippet:…}` left literal (nothing to resolve in a URL).

URL/deeplink values are **auto percent-encoded** after modifiers; `| raw` opts out (and can change destination kind). Paths are never encoded. `{query}` rewritten to `{argument}` on Raycast import. `{selectedText}` accepted as `{selection}`.

Examples from the doc:

```
https://google.com/search?q={argument}
https://github.com/search?q={argument name="Repository"}
https://translate.google.com/?text={selection}
https://chat.openai.com/?q={clipboard}
~/Notes/{date format="yyyy-MM-dd"}.md
```

Destinations (`QuicklinkDestination.detect`): path (`~/` `/` `file://`) · web (`http(s)` or bare host+TLD) · network (`smb afp nfs ftp sftp ftps`) · deeplink (any other `scheme:`) · reject. One-letter scheme = Windows drive letter (excluded). `scheme:` + all-digits remainder = `host:port` (excluded).

---

# Features by group

Each feature: name, user-facing behavior, invariants, key types/files, data stored, permissions, settings UI, Windows API, parity, edge cases.

---

## Core launcher

### Palette

- **Behavior:** Global hotkey (default unset; onboarding records it; README example ⌥Space) summons a floating non-activating panel. Type to filter, ↑/↓, ↵ activate, Esc dismisses (first press clears query). Tab rings launcher → AI chat → clipboard → launcher (chat skipped if AI off). ⌘K actions menu. Compact vs expanded; user-owned frame. Header back chevron off root. Pop to Root after hide (`popToRootTimeout`). Screens: launcher, clipboard, calculator history, emoji, file search, schedule, uninstall, quicklinks, snippets, custom-command arguments, extension command, AI, AI history.
- **Invariants:** `PaletteWindowController` owns the frame (SwiftUI `sizingOptions = []`). Flat `selection` == visible rows including lead cards (`PaletteRowIndex`, Foundation-only). Search field never unmounted. Focus restoration + paste target recorded as `previousApp` once per summon. Input source is a session, restored only if still the one Relay applied.
- **Files:** `Relay/Palette/*`, `Features/PaletteRowIndex.swift`.
- **Data:** `palettePosition` (not backed up), transparency, interface size, escape-key behavior, pop-to-root timeout.
- **Permissions:** Accessibility for paste/focus restore.
- **Settings:** General (hotkey, transparency −100…100 five detents, appearance, interface size, launch at login, menu bar, escape behavior, pop to root).
- **Windows:** `WS_EX_TOPMOST` + `WS_EX_NOACTIVATE` layered HWND; DWM backdrop (`DWMWA_SYSTEMBACKDROP_TYPE` Mica/Acrylic); `SetForegroundWindow` policy is the hard part — Windows wants activation to paste. IME: `ITfThreadMgr`.
- **Parity:** **Adapt** (non-activating panel + restore focus is the Windows tax).
- **Edges:** Footer menu must not resign first responder. Click-away catcher stays mounted. ⌘-digit slots are physical number-row, layout-independent.

### App launcher & fuzzy match

- **Behavior:** Empty query: favorites (⌘1–⌘9, ⌘0) then apps (alpha, running dot), then System Settings, quicklinks, snippets, system actions, window layouts, window commands, custom commands, built-ins. Type: fuzzy + frecency. ↵ launches / focuses. Per-app hotkey toggles focus/hide. ⌘K: Hide from Search (⇧⌘H), Favorites (⇧⌘F), Show in Finder (⌘↵), Quit (⌃⇧Q), Restart (⌘R), Uninstall. Compact bar shows first 5 favorites. Contextual: typed URL → **Open in Browser**. Fallbacks under `Use “…” with…`: AI Chat, Search Files, Run Shell Command, argumented quicklinks.
- **Invariants:** `AppEntry.Kind` is the only category. Category switch gates rows **and** hotkeys. One command, one pane, one switch (`SettingsTab.ownedCommands`). `SearchRelevance` Foundation-only. `EntryNaming` is the only namer. Aliases stay separate strings. Scopes + ranking store pure.
- **Files:** `Features/Launcher/` — `AppIndex`, `SearchRelevance`, `EntryNaming`, `SearchScopes`, `LauncherRankingStore`, `FavoritesStore`, `FallbackStore`, `AliasStore`, `RunningAppsMonitor`, `AppLauncher`.
- **Data:** `searchScopes` (tilde paths); `launcher-ranking.json`; `favoriteApps`; `launcherAliases`; `hiddenItemKeys`; fallback order **not** in backups (capability).
- **Permissions:** none for scan.
- **Settings:** Applications (scopes, enable apps, per-app alias/hotkey/visibility); System Settings; Fallbacks; Commands; General (clear learned ranking).
- **Windows:** enumerate `%ProgramData%\Microsoft\Windows\Start Menu`, `%AppData%\Microsoft\Windows\Start Menu`, `shell:AppsFolder` (including Store apps via `IApplicationActivationManager` / `AUMID`). `SHLoadIndirectString` for localized names. Running: `EnumWindows` + UWP `IPackageDebugSettings` is insufficient — use `GetForegroundWindow` + process snapshot. Focus/hide: `ShowWindow` / `SwitchToThisWindow` / `IApplicationActivationManager.ActivateApplication`.
- **Parity:** **Adapt** (Start Menu + AUMID vs `.app` bundles; no `LSUIElement` heuristic).
- **Edges:** One-subfolder-deep scan. Finder is an individual bundle so CoreServices agents stay out. Non-Latin romanization (Han/Kana/Hangul/Cyrillic). Bundle-id aliases exact-only. Hide-from-search only for kinds whose pane can undo it. Quit All excludes Explorer analog of Finder + self by PID.

### Onboarding (no feature doc)

- **Behavior:** First launch, re-runnable from Settings. 4 steps: (0) record summon hotkey + launch at login; (1) Accessibility grant; (2) optional `.rayconfig` import + passphrase + category ticks; (3) “You’re all set” / Get Started opens launcher.
- **Files:** `Features/Onboarding/*`.
- **Data:** onboarding-complete flag.
- **Windows:** first-run wizard; map Accessibility step to “Enable UI Access / Accessibility” explanation. Raycast import still useful for clipboard/snippets/hotkeys.
- **Parity:** **Adapt**.

---

## Input / hotkeys

### Hotkeys

- **Behavior:** Every action binds a combo **or** a double-tap of ⌃/⌥/⇧/⌘. Recorder is not a focusable control; local `NSEvent` monitors; engines paused while recording. Hyper Key: Caps Lock or a right-side modifier becomes ⌃⌥(⇧)⌘ system-wide. ✦ notation collapses the chord. Include Shift re-points stored combos. Double-tap fires on **second release**.
- **Invariants:** JSON under `hotkey.<action>` via `HotKeyAction.defaultsKey` (also Carbon registration id). Command shortcut and launcher row share one funnel. Mode-opening commands toggle. `HotKeyBinding` two cases, two engines. `DoubleTapDetector` Foundation-only, clock injected. Listen-only tap installs only while a double-tap is bound, never prompts. `KeyShortcut.hyperChord` is the only Hyper spelling.
- **Files:** `Features/HotKeys/` — `KeyShortcut`, `HotKeyBinding`, `HotKeyCenter`, `HotKeyManager`, `DoubleTap*`, `HyperKeyTap`, `CapsLockRemap`, `ShortcutRecorder`, `ShortcutCaptureSession`.
- **Data:** UserDefaults `hotkey.*`; indexes `boundAppBundleIDs`, `boundPaneBundleIDs`, `boundCustomCommandIDs`, `boundQuicklinkIDs`, `boundWindowLayoutIDs`, `boundAppleShortcutIDs`, `boundQuickActionIDs`. Hyper key in `AppSettings`.
- **Permissions:** Accessibility for Hyper (modifying tap) and double-tap (listen-only). Never prompted from the tap; recorder shows a warning.
- **Settings:** per-row recorders; General for Hyper Key + Include Shift.
- **Windows:** `RegisterHotKey` is combo-only and conflicts with other apps. Low-level `WH_KEYBOARD_LL` hook (or `Raw Input`) for double-tap + Hyper. Caps Lock remap: `MapVirtualKey` cannot suppress Caps at HID; need Interception driver **or** accept Toggle+chord (worse). `kioskMode` / `Scan Code Mapper` (`HKLM\SYSTEM\...\Keyboard Layout\Scancode Map`) persists across reboot — Relay explicitly does **not** survive reboot; Windows port should clear on exit too.
- **Parity:** **Adapt** (Hyper/Caps is the highest-risk input piece). Combo hotkeys **Direct-ish**.
- **Edges:** Caps Lock latch is below CGEventTap; remap to F18 via IOKit `UserKeyMapping`. fn bit scrubbed. Fast user switching drops half-held state. Deny-list: Open in Browser, Run Shell Command, Quit cannot bind. Window/system shortcuts still register while feature off, coordinator no-ops.

### Text injection (shared plumbing)

- **Behavior:** Snippets and Quick Actions paste/replace into the front app. Own editors (`NoteTextView` / `InjectableTextView`) insert in-process.
- **Invariants:** Two tiers. Renderer (Chromium/Monaco marker range) skip AX write. Confirm replacement by read-back. Events: delete keyword, then paste or 4-UTF-16-unit keystrokes (Blink `kTextLengthCap`). Pasteboard loan is expansion-only, restore if change-count unchanged.
- **Files:** `Features/TextInjection/Service/`.
- **Windows:** UI Automation `ValuePattern`/`TextPattern`; fallback `SendInput`. Chromium on Windows has the same 4-unit cap. Clipboard restore via `AddClipboardFormatListener` + OpenClipboard. UAC / elevated target: UI Access (`uiAccess=true` in manifest) or fail closed.
- **Parity:** **Adapt**. Secure Event Input analog = password fields / UAC secure desktop — refuse.

---

## Clipboard

### Clipboard history

- **Behavior:** Ships **on** (only feature switch that does). Tab or command opens split list+preview. ↵ paste (default) or copy (`clipboardDefaultAction`); ⌘↵ the other; ⌥↵ paste keep-open. Filters ⌘P: All / Text / Images / Files / Links / Emails / Colors. Pin ⌘. ; slots ⌘1–0 on pins. ⌃X delete. Drag-out is copy-only. Colour card in launcher. Optional OCR of images/PDFs (off).
- **Invariants:** `clipboardEnabled` absence outranks stored false. ↵/⌘↵ one swapped pair. Writes stamp `internalType`. Store Foundation+SQLite. Corrupt DB deleted+recreated (history is captured). Link/address/colour derived, not stored. OCR never in-process (`ClipboardTextHelper`). File URL read **before** text. `.file` is a reference, never copied. Recognized text is search metadata only.
- **Files:** `Features/Clipboard/` — `ClipboardManager`, `ClipboardStore`, `ClipboardCoordinator`, `ColorValue`, `ClipDrag`.
- **Data:** `~/Library/Application Support/<id>/clipboard.sqlite3` + `images/*.png`. Newest 1000 in memory; FTS5 trigram (min 3 chars). Pins skip retention. OCR `item_text` table.
- **Permissions:** Accessibility to paste. OCR none (local Vision).
- **Settings:** Clipboard — enable, default action, retention, ignored apps, OCR switch (machine-local, not backed up).
- **Windows:** `AddClipboardFormatListener` (no 0.5s poll). `CF_HDROP` before `CF_UNICODETEXT`. SQLite+FTS5 portable. Image: `CF_DIB`/`PNG` registered format. OCR: Windows.Media.Ocr / WinRT. Sensitive: skip `CFSTR_DROPDESCRIPTION`? Map macOS concealed types to Windows Clipboard sequence that Office uses for passwords — skip `CFSTR_INETURL` from credential UIs if possible; document as best-effort.
- **Parity:** **Adapt** (listener vs poll; paste still needs inject).
- **Edges:** Volatile roots (`/tmp`, `%TEMP%`, caches) rejected. Vanished file HUD, row kept. Backup carries path not bytes. Clear History works while feature off.

---

## Content (notes / snippets / quicklinks)

### Notes

- **Behavior:** Off by default. Show Notes toggles floating editor; Create Note; Search Notes (switcher). One `.md` = one note; filename is title; unnamed `Untitled.md` shows first line. ⌘N create, ⌘P switcher, ⌘O folder, ⌘W hide. Autosave 300 ms. Empty collection allowed.
- **Invariants:** No frontmatter/DB. Editor = disk = search string. Only active note dirty. Relay sole writer (no watcher). Off = no work. User owns window size.
- **Files:** `Features/Notes/` — `NotesRepository`, `NotesStore`, `NotesCoordinator`, `NoteTextView`.
- **Data:** `~/Library/Application Support/<id>/Notes/*.md`. Active filename in UserDefaults, not backups.
- **Permissions:** none. Snippet expansion into notes is in-process.
- **Settings:** Notes pane (enable + three command rows).
- **Windows:** Direct — `%LOCALAPPDATA%\<app>\Notes\`; `RichEdit`/`WinUI TextBox` with literal markdown; `SetWindowPos` autosave placement (`WINDOWPLACEMENT`).
- **Parity:** **Direct**.
- **Edges:** Save overwrites disk; external edit of active note lost. Import suffixes collisions.

### Snippets

- **Behavior:** Off. Enable confirms then requests Accessibility (keyword tap). Search Snippets browser; Create Snippet; launcher rows; keyword expansion in other apps. Optional confirmation HUD.
- **Invariants:** Channel-isolated path identity. Enable = keyword-consent; excluded from backups. Model+Service compile in `snippets-test`. Expansion targets key window first (`InjectionTarget.current`), not frontmost (panels are non-activating).
- **Files:** `Features/Snippets/` + `TextInjection`.
- **Data:** `.../Snippets/*.md`. Keyword tap buffer 256 chars, 15s idle reset.
- **Permissions:** Accessibility only (listen-only tap). Not Input Monitoring.
- **Settings:** Snippets (enable, show in launcher, editor, keyword).
- **Windows:** `WH_KEYBOARD_LL` for keywords; same injection stack. Manifest `uiAccess` for elevated apps.
- **Parity:** **Adapt**.
- **Edges:** Chromium 4-unit keystrokes. Nested snippets 5 levels. Import from Raycast never enables the tap. Conflicting editors via `NSFileCoordinator`.

### Quicklinks

- **Behavior:** Off. Named URL/path/deeplink/search as launcher rows + global hotkeys. Arguments as header chips. Search Quicklinks split view. Create/Import/Export commands. Open With, optional `--new-window`.
- **Invariants:** Store never deletes a corrupt DB (authored). Model Foundation+SQLite. Drawing chips parses template only. `Quicklink.precedes` is display order. Disabled = inert not gone. One template engine.
- **Files:** `Features/Quicklinks/` — `QuicklinkStore`, `QuicklinkLauncher`, `QuicklinkArchive`.
- **Data:** `.../quicklinks.sqlite3`.
- **Permissions:** none. Selection read needs Accessibility at open time.
- **Settings:** Quicklinks (enable, show in launcher, per-row enabled/root-search/alias/hotkey, selection-missing policy: clipboard vs ask).
- **Windows:** `ShellExecuteEx` / `IApplicationActivationManager`; `file://` and `https://` Direct; custom schemes via registry. `--new-window` is Chromium/Firefox argv — same honesty.
- **Parity:** **Direct** (engine) / **Adapt** (open-with picker = `AssocQueryString`).
- **Edges:** `| raw` can change destination kind. Store loads even while off (hotkey prune). Drive-letter false positive already excluded.

### Emoji picker

- **Behavior:** Palette grid, searchable. Pins, frequently used, categories. Density 6–10 columns. ↵ insert; zoom ⌘+/−/0.
- **Invariants:** Model Foundation-only. `EmojiData.generated.swift` from `Scripts/gen-emoji.js`. Interaction on the **row**, not cell (~100 MB trap).
- **Files:** `Features/Emoji/`.
- **Data:** `emoji-pinned.json`; frequent store; `emojiGridColumns`.
- **Settings:** Emoji & Symbols (columns).
- **Windows:** Direct — insert via SendInput / clipboard. Emoji-16 data portable. Segoe UI Emoji vs Apple Color Emoji.
- **Parity:** **Direct**.
- **Edges:** Colon unwrap `:+1:`. Multiword: every word must start a word.

---

## System (window mgmt, system actions)

### Window management

- **Behavior:** Off. 35 commands in launcher + bindable. Gap setting. Halves cycle. Restore. Fullscreen. Space next/prev.
- **Invariants:** Engine in AX space, flip anchored on **primary**. No `backingScaleFactor`. Model pure. `AXWindowAccess` is the one write (size→position→size). Space commands never hit `WindowMover`.
- **Files:** `Features/WindowManagement/Model/{WindowCommand,WindowPlacementEngine,WindowActionMemory,SpaceGesture}` + `Service/{AXWindowAccess,AXScreens,WindowMover,SpaceSwitcher}` + `UI/WindowCommandCoordinator`.
- **Data:** `windowManagementEnabled` (off), `windowManagementShowInLauncher` (on), `windowGap` (0), `windowCycle` (off). Memory not persisted (LRU 64).
- **Permissions:** Accessibility (already for paste); no extra class. Feature does not prompt; coordinator re-checks.
- **Settings:** Window Management pane (switch, gap, cycle, per-command visibility+hotkey, layouts section).
- **Windows:** `SetWindowPos` + `GetWindowPlacement` + `DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS)` for shadows. Work area `SPI_GETWORKAREA` / per-monitor `rcWork` (taskbar). DPI: use **physical/px consistently**; Relay’s “points, ignore scale” maps to working in **screen coordinates** via `GetDpiForWindow` only if you convert — prefer screen coords end-to-end. Non-resizable: `GetWindowLong(GWL_STYLE)` lacks `WS_THICKFRAME` → fail quiet. Fullscreen: `WS_MAXIMIZE` vs borderless — pick one and document. Virtual desktops: `IVirtualDesktopManager.MoveWindowToDesktop` is per-window; switching desktop is undocumented COM (`Win11 22621+` `IVirtualDesktopManagerInternal`) — **Adapt / high risk**. Do not port Dock-swipe CGEvent splice.
- **Parity:** Geometry **Direct**. Spaces **Adapt** (or Drop if unwilling to use undocumented COM). Stage-adjacent: n/a.
- **Edges:** Mixed-DPI multi-monitor (the AX primary-anchor bug). Terminal cell-size drift → compare observed frame not requested. Layouts must not write `WindowActionMemory`.

### Window layouts

- **Behavior:** Saved arrangements of apps/windows/displays. Run from launcher, hotkey, or pane. Capture current windows into editor. Two commands: Create Layout, Create Layout from Current Windows. Sizes as fractions of `visibleFrame`.
- **Invariants:** `resolve` is the only placement; `resolve(describe(frame))==frame` for whole-point frames. Display match by **UUID only**; missing display skips entry. Runner never touches action memory. At most one frontmost entry. Capture writes `usesPreferredGap: false`.
- **Files:** `Model/WindowLayout*.swift`, `Service/WindowLayoutRunner`, `UI/WindowLayoutCoordinator`, Settings sheets.
- **Data:** JSON in UserDefaults; `windowLayoutsShowInLauncher` (on, **separate** from commands flag). Bindings `hotkey.windowLayout.<uuid>`.
- **Permissions:** Accessibility only (no Screen Recording; titles unused).
- **Settings:** Window Management → layouts list, editor sheet (preview + inspector, 3×3 anchor, gap opt-in, bring-to-front, optional launch argument copied from quicklink text).
- **Windows:** Monitor identity: `DISPLAYCONFIG` adapter+target LUID / `GetMonitorInfo` device name — **not** as stable as `CGDisplayCreateUUIDFromDisplayID`; treat as Adapt. Place via `SetWindowPos`. Launch missing apps via AUMID. Poll for HWND up to 10s.
- **Parity:** **Adapt** (display identity + Win32 vs AX).
- **Edges:** Greedy nearest-centre window binding. Argument entries always launch new. Absent display skip (never pile onto laptop).

### Switch Windows (Navigation)

- **Behavior:** Off (`navigationEnabled`). Command `Search Windows` / `command:switch-windows`. Sync AX sweep of standard windows including minimized + other Spaces. ↵ unminimize + raise + activate. Order: not-minimized first, then app MRU (`CGWindowList` layer 0), then name. Cap 200.
- **Invariants:** Sweep synchronous. Live `AXUIElement` never leaves MainActor / never outlives show. Model doesn’t know what a window is. Order total. Accessibility gated on show **and** activate. Activate with `restoreFocus: false`.
- **Files:** `Features/WindowSwitcher/` — `WindowSwitchEntry/Order/Query`, `WindowZOrder`, `WindowSwitchSweep/Session/Coordinator`.
- **Data:** none beyond the switch. Backed up.
- **Permissions:** Accessibility. No Screen Recording (`kCGWindowName` unused; titles from AX).
- **Settings:** Navigation pane (switch, command row). No “show in launcher” extra switch.
- **Windows:** `EnumWindows` + `GetWindowText` + `IsIconic` + `SetForegroundWindow` + `ShowWindow(SW_RESTORE)`. MRU: `GetWindow(GW_HWNDNEXT)` z-order. Other virtual-desktop windows: `IVirtualDesktopManager.IsWindowOnCurrentVirtualDesktop`.
- **Parity:** **Adapt**.
- **Edges:** App quit between sweep and ↵ reports. Hung app 0.2s AX timeout → `SendMessageTimeout`.

### Menu Search

- **Behavior:** Command `Search Menu Bar Items`. Walks **frozen** front app’s menu bar. Browse by top-level menu sections; query flattens to Results. ↵ presses the item. Apple menu off by default (drop first bar item by position). Exclude-app list. Cap 20 depth / 4000 items / 200 leaves per submenu / 1s walk / 0.2s per element. Only enabled, visible, non-separator, AXPress, titled leaves.
- **Invariants:** Target frozen at open. Apple menu by position not name. Sections = snapshot runs. Walk never opens menus. Truncate rather than delay. Accessibility twice. Excluded app refused before walk. Nothing outlives show.
- **Files:** `Features/MenuSearch/` — `MenuTreeNode`, `MenuSearchItem/Shortcut/Query/Target`, `MenuSnapshotPolicy`, `AXMenuAccess`, `MenuSearchSession/Coordinator`.
- **Data:** `menuSearchDisabledApps`, `menuSearchShowsAppleMenu` (off).
- **Permissions:** Accessibility.
- **Settings:** Navigation → Search Menu Bar Items section (command, Apple menu toggle, disabled apps).
- **Windows:** UI Automation `ControlType.MenuBar` / `MenuItem` on the target HWND. Many Win32 apps have no exposed menu (owner-draw, ribbon). WPF/WinUI/UWP vary. Accept coverage loss (same as unbuilt macOS submenus).
- **Parity:** **Adapt** (high coverage risk on Win32).
- **Edges:** AX shortcut bits ≠ `NSEvent.ModifierFlags`. Self-target (Relay) classified before menu-less.

### System actions

Covered in the catalog table above. Coordinator `SystemActionCoordinator.runSystemAction(id:)` is the funnel; confirmation holds for hotkeys. Settings › System Actions. Feedback HUD for invisible effects. Nothing-to-do is success-neutral, not failure.

**Drop:** Toggle Stage Manager.
**High-risk Adapt:** Dismiss Notifications, Hide/Unhide apps (no OS hide).

### File search

- **Behavior:** Off. Command / hotkey opens screen. Spotlight filename search in configured folders. Type filter All/Folders/Documents/Images/Audio/Videos/Archives. Empty query = recents (changed 3d ∪ used 30d, merge 20). ↵ open, ⌘↵ Finder, ⌘Y in-panel Quick Look, ⇧⌘C copy file, ⌥⌘C name, ⌃⌘C path, ⇧⌘V paste file to previous app, ⌃X trash. Preview pane 16:9.
- **Invariants:** MDQuery cap 1000 then 200 rows. Model pure. Filename-only, no private index. Filter belongs to query. Hidden + app-bundle contents structural. `~/Library` never auto-scoped. Shipped ignore rules compiled-in, not persisted. Off = no Spotlight. No file permission asked. Superseded query never publishes.
- **Files:** `Features/FileSearch/` — `FileSearchQuery/Session/Service/Policy/Filter/IgnoreList`, `FileSearchCoordinator`, QuickLook surfaces.
- **Data:** `fileSearchEnabled` (off), `fileSearchScopes` (tilde), `fileSearchIgnorePatterns` (user-only).
- **Permissions:** none (thinner results vs FDA prompt).
- **Settings:** File Search pane (switch, scopes, ignore, command row).
- **Windows:** Windows Search (`ISearchQueryHelper` / `SystemIndex`) **or** Everything SDK **or** on-demand `FindFirstFile` (too slow). Recents: `IApplicationDocumentLists` / Jump Lists / `System.DateAccessed`. Preview: `IPreviewHandler` / WebView2 PDF. Quick Look analog is Preview Handler or `IShellItemImageFactory`.
- **Parity:** **Adapt** (no MDQuery). Prefer Windows Search + user scopes; do not build a private index (budget invariant).
- **Edges:** Home expansion skips Library. Ignore `fnmatch` without `FNM_PATHNAME`. 120 ms debounce; recents skip it.

### Calculator

See capabilities extract. Settings: currency on/off implied by engine; history command. Data: `CalculatorHistoryStore`; `currency-rates.json` in Caches. **Direct.**

### Uninstall Application

- **Behavior:** ⌘K → Uninstall Application on an `.application` row (not a global command). Sub-screen lists leftover files with sizes streaming in. ↵ uninstalls after confirm; ⌘↵ toggle row. Quits app first; trashes bundle last. Only clears hotkey/favorite/visibility/ranking if the **bundle** went.
- **Invariants:** `trashItem` only, never `removeItem`. Pure half Foundation-only. Detects FDA, never requests it. Locked candidates cannot be checked. Discovery never waits on sizing. Refuses to uninstall itself (running identity, so Dev channel too).
- **Files:** `Features/Uninstall/` — `UninstallTarget/Rules/Protection/Plan/Scanner/Runner/Session/Coordinator`.
- **Data:** none persisted.
- **Permissions:** none asked. Show Info uses Apple Events (Automation prompt).
- **Windows:** Adapt heavily. Roots map: `%LOCALAPPDATA%`, `%APPDATA%`, `%PROGRAMDATA%`, registry uninstall keys (`HKCU/HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall`), Start Menu shortcuts, `%LOCALAPPDATA%\Packages\<pfn>` for Store apps. Prefer the app’s own QuietUninstallString when present; leftover scanner is extra. Recycle Bin via `IFileOperation` (`FOF_ALLOWUNDO`). Never delete `%USERPROFILE%\<Name>`. Sibling rule for `app` vs `app.beta` still applies.
- **Parity:** **Adapt**.
- **Edges:** Display-name match is exact, ≥3 chars, not a Library subdir name, not shared. Home directory is not a root (VS Code `Code` vs `~/Code`).

---

## Calendar

- **Behavior:** Off. Consent dialog then EventKit. Empty-launcher **join card** for meeting in `[start−lead, min(start+lead, end)]`. Join Next Meeting hotkey. Menu bar (disabled / icon / title). Auto-join (excluded from backup). Optional camera preview as confirmation. Commands: Join Next Meeting, My Schedule, Create Event, Copy Meeting Link (not bindable), Open in Calendar (not bindable). Individual meetings as launcher rows (default next 3).
- **Invariants:** One timer (`MeetingClock`) only while watched. Auto-join once per meeting per launch, only ≥ `armedAt`. Camera settles before panel. Recurrence from EventKit predicate, never hand-rolled. `UpcomingWindow.agenda` is the only “which events count”. `calendarEnabled` is consent; written only after macOS grants; excluded from backup. Per-calendar exclusions on `CalendarStore`, not `AppSettings` (machine-specific IDs).
- **Files:** `Features/Calendar/` — `MeetingLink/Event`, `UpcomingWindow`, `AutoJoinPolicy`, `CalendarStore`, `MeetingLauncher`, `CalendarCoordinator`.
- **Data:** enable flag (local); join window minutes; include tomorrow; menu-bar display; auto-join; camera preview; hidden calendar IDs.
- **Permissions:** Calendar (EventKit Full Access analog). Camera if preview on.
- **Settings:** Calendar pane. Permissions pane re-offers enable.
- **Windows:** WinRT `Windows.ApplicationModel.Appointments` (limited) **or** Outlook COM / Microsoft Graph. Meeting URL scrape from location/body still portable (`MeetingLink.detect`). Zoom `zoommtg://` and Teams `msteams:` exist on Windows. Google `authuser` query still valid.
- **Parity:** **Adapt** (store) / **Direct** (link detection + deeplinks). Auto-join + camera preview: keep excluded from backup.
- **Edges:** Named provider beats earlier generic URL. `zoom.us/download` rejected. Menu bar click never joins. All-day / declined / cancelled dropped.

### Camera

- **Behavior:** `Open Camera` command — live mirrored preview, cycle cameras, ↵ copies PNG to clipboard and closes. Also Calendar pre-join preview (`.preview` medium, no photo output). No settings pane, no enable switch.
- **Invariants:** Lazy; `start()` is the only TCC prompt. Settle before panel, stop after fade. Escape / click-away / shot all `close()`. Mirror on connection, not view scale. Photo as PNG. `AVCaptureSession` not Sendable; only `CaptureBox` crosses.
- **Files:** `Features/Camera/` — `CameraSession`, `CameraCoordinator`, `CameraPanel/Stage/View/Button`.
- **Data:** `mirrored` remembered for the launch only, not settings/backup.
- **Permissions:** Camera (prompt on start).
- **Windows:** `Windows.Media.Capture.MediaCapture` / `IMFCaptureEngine`. PNG via WIC. **Direct**.
- **Edges:** Switch camera without blanking (`beginConfiguration`). Multiple devices only shows Switch.

---

## AI

### AI chat

- **Behavior:** Off. Tab from launcher **asks** (new chat, query submitted). Command / hotkey opens composer. ↵ send/stop. Header model switcher. ⌘K: New Chat, History, Settings, Copy Last, Stop. Attachments ⌘V (image/PDF/text-ish). Streaming survives hide. Opens-to: recent vs new. Retention prune only while on.
- **Invariants:** Off = fully off (no DB open). API keys Keychain only. HTTPS except loopback. Chat model is the route. On-device unavailable is reported, never rerouted to billed. Codex tools unavailable; sandbox read-only network-disabled. Tool loop is a decorator. Ephemeral URLSession. History local SQLite. `ask(_:)` skips open policy.
- **Files:** `Features/AI/` — providers, `AIChatCoordinator`, `ChatHistoryStore`, `AIEndpointPolicy`, `KeychainSecretStore`.
- **Data:** `ai-chats.sqlite3`; connections in UserDefaults minus keys; Keychain `aiAPIKeys`. Almost all AI keys excluded from backup.
- **Permissions:** network; Apple Intelligence entitlement on Mac. Camera unused.
- **Settings:** AI (enable, providers sheet, default model, system prompt + send switch, opens-to, retention, web search off, MCP section).
- **Windows:** HTTP providers **Direct**. “Apple Intelligence” → **Adapt** (Windows Copilot Runtime / ONNX / Phi-3 local, or Drop local and keep HTTP+CLI). Codex/Claude/OpenCode CLIs **Direct** (`CreateProcess` + PATH locator including nvm/fnm/volta). Web search OpenRouter plugin **Direct**.
- **Edges:** Staged files survive re-summon; typed draft does not. Text files inlined at `boundedContext`, not stored in message text. Vision models from OpenRouter catalog only.

### MCP

- **Behavior:** Off. Settings → AI → MCP servers (HTTP or stdio command). Tools namespaced `slug__tool` (64 char, `[A-Za-z0-9_-]`). `@slug` addresses one server. Trust: Ask (default) / Always / Never; first call three-way dialog (Always / This Chat / Don’t Allow=Esc). Tools only on HTTP API routes, not Apple Intelligence / ChatGPT-Codex.
- **Invariants:** Off = no process. Credentials Keychain only (`mcpSecrets`). HTTPS policy shared with AI. Tool call never stored as conversation turns (render record only). Dialog can grant; only Settings withholds (`.never`). Refused call is tool result content, not thrown. Max 10 rounds; result byte caps. Handle derived (`MCPSlug`). Servers start with chat, stop after 10 idle minutes. Relay exposes nothing back (no sampling/roots). `mcpEnabled` + `mcpServers` excluded from backup.
- **Files:** `Features/MCP/` — `MCPProtocol`, `MCPHTTPTransport`, `MCPStdioTransport`, `MCPTrustPolicy`, `MCPCoordinator`, `AIToolLoopProvider`.
- **Data:** server metadata UserDefaults; secrets Keychain.
- **Permissions:** none beyond running user-specified processes (stdio is arbitrary code — consent flag).
- **Windows:** **Direct** (JSON-RPC + HTTPS + stdio). `ExecutableLocator` must search `%PATH%`, `%LOCALAPPDATA%\fnm`, Scoop, WinGet links. Job objects to kill process trees on idle.
- **Edges:** Client advertises no capabilities. Unknown `@handle` sent verbatim.

### Quick Actions

- **Behavior:** Off. Four shipped: Fix Grammar (replace, diff), Rewrite (panel, diff), Translate (Apple Translation, panel, no diff), Summarize (panel always). Custom: name, glyph, prompt, preview/replace. Each has launcher command + hotkey. Result replace or panel (↵ replace, ⌘C copy, Esc dismiss).
- **Invariants:** Off = shortcuts no-op. One funnel; read `targetApp` before hide. Enable is Accessibility consent. Never inject into self / Secure Event Input. One run at a time. Own model route defaulting to Apple Intelligence. Custom actions + prompts **not** in backups. Built-in instructions treat selection as untrusted; custom cannot drop `boundary`.
- **Files:** `Features/QuickActions/` — `QuickAction`, `QuickActionPrompt`, `QuickActionCoordinator`, `TextTranslator`, `QuickActionPanel`, `CustomQuickActionStore`.
- **Data:** `.../custom-quick-actions.json`; `quickActionModel` / overrides excluded from backup.
- **Permissions:** Accessibility. Translation language downloads via System Settings on Mac.
- **Settings:** Quick Actions pane (enable, default model, per-action replace/preview, editors, custom list).
- **Windows:** Provider path **Direct**. Translate: **Adapt** (`Windows.Media.Ocr` is OCR; translation is `Windows.AI` or Azure / local Marian). Selection: UIA `TextPattern.GetSelection`. Panel: layered HWND.
- **Parity:** **Adapt**. Translate without Apple’s 47-language list — use whatever local/cloud translator you ship and be honest in the picker.
- **Edges:** Download-needed language opens panel with instructions, never silent no-op.

---

## Extensions

### Raycast extensions

- **Behavior:** Off until asked (consent to run third-party JS). Settings → Extensions: Search Registries, Import from Raycast (`~/.config/raycast` and `~/.config/raycast-x`), Add Folder. Commands appear in launcher with owner name as weak keyword. View commands take over palette (List/Grid/Detail/Form/ActionPanel). No-view run headless. Global shortcut per command. Aliases. Deeplinks `raycast://extensions/<owner>/<ext>/<cmd>` and `relay://` mirror. Background refresh on `interval`. OAuth PKCE; claims `raycast`, `com.raycast`, `relay` URL schemes.
- **Invariants:** Exactly one command / one `JSContext` at a time. Runtime `@unchecked Sendable` boundary. `RaycastRuntime.generated.js` generated, never hand-edited. `ExtensionScreen` is the only row-order. Off = fully off. `SymbolCatalog` reads `CoreGlyphs.bundle`. `extensionsEnabled` excluded from backup.
- **Files:** `Features/Extensions/` (runtime, host bridge, node shims, OAuth, catalog, manager, render tree) + `Resources/RaycastRuntime.generated.js` + `Scripts/raycast-runtime/`.
- **Data:** `.../extensions/<name>/`; `extension-data/<safe>.json`; `extension-commands.json`; `extension-support/`; Keychain `com.relay.extensions.oauth`; appearance overrides; hotkeys `hotkey.extensionCommand.<entry id>`.
- **Permissions:** none OS-level; user consent. Network/exec as the extension asks (Node shims: fs, child_process, fetch, crypto, zlib, http).
- **Settings:** Extensions (enable, show in launcher, per-extension/per-command visibility, registries, custom search PATH, storage cleanup, launcher icon override).
- **Windows:** JavaScriptCore is Apple. Port options: (1) **ChakraCore / QuickJS / V8** embedding — Adapt, large. (2) Drop runtime, keep “import command metadata only”. Honest 1:1 requires a JS engine + the shim surface (`@raycast/api`, Node builtins). URL scheme conflict with installed Raycast is a deliberate Mac trade; on Windows `raycast://` is usually free. `uiAccess` not required for JS. `child_process` must use Job Objects. `menu-bar` commands: listed but don’t open (gap remains). `AI` / `BrowserExtension` / `WindowManagement` Raycast services: import OK, call throws.
- **Parity:** **Adapt** (engine) — largest engineering item after Hyper Key. Do not claim byte-identical JavaScriptCore.
- **Edges:** One context (timer-cancel bug). Rust helpers build `-e dev`. PATH locator for pnpm/bun/yarn/npm. OAuth 5 min timeout. Build workspace `$TMPDIR/relay-install-<uuid>/`. Measured 32/37 installed extensions boot on the author’s Mac — not a guarantee.

### Custom commands

- **Behavior:** Off. Settings → Custom Commands: name + zsh text + args + run-in folder + load shell env + show output + confirm + icon. Launcher section. Import Raycast script-commands folder (`@raycast.title` + shebang). Ad-hoc **Run Shell Command** fallback always available (own checkbox, not library switch).
- **Invariants:** Model/runner AppKit-free. Confirmation cannot be bypassed; import warns. Disabled inert. Arguments never spliced (positional `$1`…).
- **Files:** `Features/CustomCommands/` — `CustomCommand`, `ShellCommandRunner`, `PseudoTerminal`, `ANSIInterpreter`, `RaycastScriptImport`.
- **Data:** JSON in UserDefaults; UUID identity.
- **Permissions:** none; running user scripts is the capability (backup warns).
- **Settings:** Commands pane (library + Import Raycast Scripts). Fallbacks pane for ad-hoc shell.
- **Windows:** **Adapt** — `cmd.exe` / `pwsh -NoProfile` / `pwsh -File`. Positional args still. Pty: ConPTY (`CreatePseudoConsole`). Stop: `GenerateConsoleCtrlEvent` / job object kill. `RELAY=1`. Load profile analog of `-ilc` is `-l` on pwsh (expensive; off by default). Working directory missing = fail, not home.
- **Parity:** **Adapt**.
- **Edges:** No timeout except Stop. Command outlives app quit. Status 127 hint is Unix-specific — map to Win32 `ERROR_FILE_NOT_FOUND`.

### Apple Shortcuts

- **Behavior:** Off. Lists via `/usr/bin/shortcuts list --show-identifiers`; runs `shortcuts run <uuid>`. Own launcher section, Shortcuts.app icon. Alias/hotkey/favorite. Failed read keeps last good library. Empty read never sweeps.
- **Invariants:** Shortcuts owns the library. `run(id:)` single funnel. Identity is UUID.
- **Windows:** **Drop** as Apple Shortcuts. Optional later Adapt: PowerToys Command Palette scripts, or `.lnk` / scheduled tasks — not 1:1. Do not pretend Power Automate is Shortcuts.
- **Parity:** **Drop**.

---

## Platform plumbing

### Backup

- **Behavior:** Settings → Backup. Export/import `.relay` with five ticks: Settings & Shortcuts, Clipboard History, Snippets, Notes, Launcher Learning. Also Raycast `.rayconfig` entry.
- **Invariants:** Hand-written `SettingsBackup` mirror; `settings-backup-test` fails if a key is uncovered. Capability flags never imported (`snippetsEnabled`, `extensionsEnabled`, `calendarEnabled`, `autoJoinMeetings`, `cameraPreview`, `quickActionsEnabled`, `ai*`, `mcp*`, fallbacks, `aiWebSearch`, …). No absolute path in archive. Format version `==` only (no migration). Extensions, AI history, Keychain, Caches never travel.
- **Layout:** `manifest.json`, `settings.json`, `clipboard/items.jsonl` + `images/`, `snippets/`, `notes/`, `learning/{ranking,emoji,calculator}.json`. AppleArchive LZFSE; Windows needs a replacement (zip + deflate is fine if filtered).
- **Windows:** **Adapt** archive container (`System.IO.Compression` + entry filter for `..` and symlinks). Settings: JSON file under `%LOCALAPPDATA%` rather than `NSUserDefaults`. Secrets stay DPAPI/`Credential Manager`, never in the file.
- **Parity:** **Adapt**.
- **Edges:** Learning **replaces**. Snippets merge by name+body and do not enable the feature. Notes suffix titles. Clipboard streams, dedupes on text/image name.

### Raycast import

- **Behavior:** `.rayconfig` = `RAYCFG3\n` + gzip header JSON + AES-256-GCM + tag. Key = scrypt(passphrase, salt, N=16384, r=8, p=1, dkLen=32). User types passphrase (Relay never reads Raycast’s Keychain). Maps apps (path after `::=::`), hotkeys (combo only), clipboard, snippets (`title`/`text`/`keyword`), quicklinks (`{Query}`→`{argument}`). Script commands are a **folder** importer, not this file. v1 and Raycast X beta formats deleted as of v0.10.5.
- **Windows:** Crypto **Direct** (same bytes). Raycast for Windows exports may differ — verify before promising. Hotkey layout-independent codes need a Win32 mapping table.
- **Parity:** **Direct** (file) / **Adapt** (keycodes, app IDs → AUMID).

### Updates

- **Behavior:** Daily GitHub Releases check (30s after launch). Native notes window. Zip by arch; signature check; `replaceItemAt`; relaunch via terminate not `exit`. Homebrew `auto_updates true` so brew does not fight. Dev bundle never updates. Prompt defers while expanding snippet, running extension, uninstalling, recording shortcut, dialog up, or palette open; re-offer 2 min × 30 min.
- **Data:** `~/Library/Caches/<id>/update-check.json` only. Nothing in AppSettings.
- **Windows:** **Adapt** — GitHub Releases zip + `MoveFileEx` replace. Code signing: Authenticode (`WinVerifyTrust`) against pinned publisher. Install dir `%LOCALAPPDATA%\Programs\Relay` to avoid needing elevation. WinGet `UpgradeBehavior: install` if packaged. Do not shell out to `xattr`. Channels via separate AppIDs / side-by-side install dirs.
- **Parity:** **Adapt**.
- **Edges:** Intel vs arm zip selection → x64 vs arm64 vs `win-x64`/`win-arm64`. No Sparkle.

### Uninstall (app’s own leftover cleaner)

Already under System. The **product** uninstall of Relay itself should mirror Mac: remove `%LOCALAPPDATA%\<id>`, `%APPDATA%`, scheduled task (login item), hotkey hooks, Credential Manager entries. Settings → About can keep a “Reset Relay” later; Mac uses the Uninstall Application feature only for **other** apps.

### Support

- **Behavior:** One window, one Polar.sh checkout URL (`SupportCoordinator.checkout` is the only copy). Reminder checkbox defaults **on**; off is forever. Schedule file `support-reminder.json` in Application Support (`firstSeenAt`, `lastAskedAt`). First ask one interval after first run, never day one. `presentIfDue` uses same `canInterruptUser` gate as updates. Reachable from palette menu, Settings → About, menu bar, launcher command `CommandID.support`.
- **Windows:** **Direct** (URL + local schedule). Login item analog doesn’t affect this.
- **Parity:** **Direct**.
- **Edges:** Showing the window is what moves the anchor. Withheld ask does not move it.

---

## Settings / permissions / menu bar (shell)

### Settings window

Searchable (`SettingsSearchCatalog`). Sidebar sections as above. Permissions pane: Accessibility + Calendar (re-offer consent). About: version, support, re-run onboarding.

**Windows:** WinUI 3 NavigationView + search. **Direct**.

### Menu bar

Two independent extras: Relay’s own (`showInMenuBar`) and Calendar’s (`calendarMenuBarDisplay`). Either/both/neither.

**Windows:** NotifyIcon in the notification area. Calendar title cap still required. **Adapt** (no `MenuBarExtra` scenes). Win11 overflow tray is a UX hit.

### Launch at login

Mac: `SMAppService` / login item. **Windows Direct:** `IStartupTask` (MSIX) or `HKCU\...\Run` / Startup folder.

---

## Capability flags excluded from backup (do not import-arm)

`snippetsEnabled` · `extensionsEnabled` · `calendarEnabled` · `autoJoinMeetings` · `cameraPreview` · `quickActionsEnabled` · `aiEnabled` + all `ai*` routing/prompt/retention/web-search · `mcpEnabled` / `mcpServers` · fallback order/checkboxes · clipboard OCR · `quickAction*` model/instructions/custom actions.

---

## Drop / Adapt / Direct — summary for planning

### Drop (macOS-only)

| Feature | Why |
| --- | --- |
| Apple Shortcuts | `/usr/bin/shortcuts` + Shortcuts.app |
| Toggle Stage Manager | no OS feature |
| Hyper Key via IOKit F18 remap | possible only with a filter driver; treat as optional later, not 1:1 day one |
| Space switch via Dock CGEvent HID splice | use virtual-desktop COM or omit |
| Apple Intelligence on-device route | no Foundation Models; optional local substitute |
| Apple Translation framework | substitute another engine |
| Claiming `raycast://` against competing Raycast-mac | different on Windows |
| JavaScriptCore-at-zero-size | must embed another engine |
| Menu Search of Apple menu | no Apple menu |
| Spotlight `MDQuery` | different indexer |
| EventKit as the calendar store | different store |
| Homebrew cask `auto_updates` | WinGet/MSIX instead |
| `NSWorkspace` hide-other-apps | no app-hide |

### Adapt (same job, different API) — the port’s real work

Palette non-activate + focus restore · hotkeys/LL hook · text injection/UIA · clipboard listener · window move/`SetWindowPos` · virtual desktops · window layouts/monitor IDs · switch windows · menu search/UIA · file search/Windows Search · system actions (appearance, recycle bin, bluetooth, eject) · notes path · snippets tap · calendar store · camera MediaCapture · uninstall roots · backup archive · updates Authenticode · custom commands ConPTY · Raycast JS engine · Quick Action translate · Quick Look preview handlers · menu bar NotifyIcon.

### Direct (portable logic or obvious Win32)

Calculator engine · snippet/quicklink template engine · MCP JSON-RPC · HTTP AI providers · CLI AI (`codex`/`claude`/`opencode`) · emoji dataset · frecency math · colour parser · Raycast `.rayconfig` crypto · support window · SQLite stores · most settings flags.

---

## Built-in commands left in Settings › Commands

(no feature switch; from `launcher.md` pane-owned list)

Calculator History · Open Camera · backup commands (export/import) · Check for Updates · Settings · About · Support · Quit.

Pane-owned (switch lives on the feature pane): AI, Quick Actions, File Search, Notes, Snippets, Navigation, Window Management, Clipboard, Emoji, Calendar, Quicklinks.

---

## Sources

- https://raw.githubusercontent.com/abue-ammar/relay/main/docs/README.md
- https://raw.githubusercontent.com/abue-ammar/relay/main/docs/features/*.md (all 28)
- https://api.github.com/repos/abue-ammar/relay/contents/Relay/Features?ref=main
- https://raw.githubusercontent.com/abue-ammar/relay/main/Relay/Features/SystemActions/Model/SystemAction.swift
- https://raw.githubusercontent.com/abue-ammar/relay/main/Relay/Features/WindowManagement/Model/WindowCommand.swift
- https://raw.githubusercontent.com/abue-ammar/relay/main/Relay/Features/Settings/SettingsTab.swift
- https://raw.githubusercontent.com/abue-ammar/relay/main/Relay/Features/Onboarding/OnboardingView.swift
- https://raw.githubusercontent.com/abue-ammar/relay/main/README.md

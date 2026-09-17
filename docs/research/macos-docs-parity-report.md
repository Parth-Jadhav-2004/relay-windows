# Relay macOS docs vs Windows — parity report

Sources (2026-09-17):

- Public docs: [https://abue-ammar.github.io/relay/docs/](https://abue-ammar.github.io/relay/docs/) (every sidebar page)
- GitHub: [abue-ammar/relay](https://github.com/abue-ammar/relay) Settings UI (`Relay/Features/Settings/`)
- Local `reference/` clone of that repo, and the current Windows tree under `src/`

macOS-only surfaces (Apple Shortcuts, Caps Lock Hyper, Stage Manager) are marked **Drop**. Everything else is a real product gap or a thinner Adapt.

Windows Settings copy that was implementation notes (API names, SQLite, HWND, `.env` essays) was stripped in `SettingsWindow.xaml` after this inventory.

---

## How they built Settings (this is the “other window” question)

macOS Settings **is also a separate window**. It is not inside the palette.

| | macOS | Windows today |
| --- | --- | --- |
| Surface | Separate titled, resizable `NSWindow` | Separate titled WinUI `Window` (`SettingsWindow`) |
| Why not in the palette | Palette is a borderless floating `NSPanel`. Settings is a normal window with traffic lights. Architecture forbids SwiftUI `Settings` / `Window` scenes on accessory apps. | Palette is a hidden HWND host. Settings is a second WinUI window. That split is correct. |
| Shell | AppKit `NSSplitViewController`: 215 pt sidebar + grouped SwiftUI `Form` | 215 px column of **Buttons** + one `ScrollViewer` of stacked `StackPanel`s |
| Size | **900 × 700**, sidebar fixed, detail min 420, autosave frame | Tokens are 900 × 700, but there is no split, no search, no history |
| Sidebar | Four groups, **22 panes** (docs say 21; Apple Shortcuts is in the app) | **Six** items: General, Permissions, Features, Hotkeys, Backup, About |
| Search | ⌘F; aliases (`ocr`, `caps lock`); jump + pulse the row | None |
| History | Back / Forward in the titlebar | None |
| Open | ⌘,, Settings command, menu bar; can land on a **specific pane** | Settings command / tray; always the last constructed window, **no pane argument** |
| Look | Stock macOS grouped Forms (System Settings), **not** the palette recipe | Mica + card toggles. Closer in *intent* than in *structure* |

So the extra window is not a bug. The gap is that macOS Settings is a **System Settings clone** (searchable sidebar, one pane per feature, row tables with alias + shortcut + hide). Ours dumps almost every feature into one Features scroll.

**Improve:** keep the separate window. Rebuild it as a split view: grouped sidebar, one pane per feature, search catalog, `Show(tab)` from the palette. Do not put Settings inside the palette.

Public spec: [Settings](https://abue-ammar.github.io/relay/docs/reference/settings/). Repo: `SettingsCoordinator.swift`, `SettingsSplitViewController.swift`, `SettingsTab.swift`.

---

## Getting started / install / permissions / palette

### Getting started
[https://abue-ammar.github.io/relay/docs/](https://abue-ammar.github.io/relay/docs/)

macOS: skippable welcome — bind a launcher shortcut (none ships bound), launch at login, Accessibility, optional Raycast import. Most features **off**; clipboard **on**. ⌘K is how you learn actions.

Windows: **no onboarding**. Alt+Space is always bound. Many features default **on** (file search, notes, quicklinks, custom commands, navigation, window management). No Raycast import. Ctrl+K exists but only for a few rows plus Settings/Quit.

**Missing:** first-run wizard, unbound-until-chosen launcher chord, re-run from About. **Improve:** ship features off except clipboard; teach Ctrl+K with real per-row actions.

### Install
[https://abue-ammar.github.io/relay/docs/install/](https://abue-ammar.github.io/relay/docs/install/)

macOS: Homebrew tap, Stable vs Beta as **separate apps** with isolated settings, self-update, self-signed + `xattr` once.

Windows: unpackaged `%LOCALAPPDATA%\Programs\Relay`, GitHub zip, `com.relay.windows` vs `.dev`. No installer, no WinGet/store, no beta channel app.

**Missing:** packaged install, channel apps side by side for users (we only isolate Debug vs Release).

### Permissions
[https://abue-ammar.github.io/relay/docs/permissions/](https://abue-ammar.github.io/relay/docs/permissions/)

macOS: asked only when a feature needs them. Settings → Permissions shows Accessibility + Calendars with deep links.

Windows: one “Request calendar access” button. Camera/TCC happens on first preview. No status rows, no deep link to Windows privacy settings.

**Improve:** status list (Calendar, Camera, Notifications if we ever need them) and “Open Windows Settings” links. Do not dump API names into the pane.

### The palette
[https://abue-ammar.github.io/relay/docs/palette/](https://abue-ammar.github.io/relay/docs/palette/)

macOS: one floating panel; features are launcher or stacked screens. Esc walks back. Tab = launcher → AI → clipboard. Compact mode, follow cursor, drag to reposition, pop-to-root timeout, Escape policy, interface size, input-source switch, ⌘Escape to root.

Windows: real palette (acrylic, tab ring, stack, file-search drill-down). **Missing:** compact mode, remembered position, pop-to-root, Escape policy, interface size (token exists, unused), first Esc clearing the query, physical ⌘1–9 slots.

---

## Launcher

### App launcher
[https://abue-ammar.github.io/relay/docs/launcher/](https://abue-ammar.github.io/relay/docs/launcher/)

**They have:** apps + System Settings panes + commands + quicklinks + snippets + system actions + window commands/layouts + custom commands + Quick Actions + extensions + meetings. Empty query is sectioned. Typed query is one ranked list plus calculator/color/Open in Browser cards and fallbacks. Exact name/alias always wins. Learned ranking. App actions: Open, Show in Finder, favorite, hide, restart, quit, uninstall. Per-app global shortcut. Search scopes.

**We have:** Start Menu / Desktop / Store apps, 30 `ms-settings:` URIs, commands, favorites, aliases, hide, frecency. Calculator card. Window commands if the flag is on.

**Missing / thin:** Open in Browser for typed URLs, fallbacks, per-app hotkey, running-app indicator, Show in Explorer / Restart / Quit / Uninstall on the app row (Ctrl+K), Applications pane + search scopes, empty-query section order (Meetings, Extensions, …).

### Favorites
[https://abue-ammar.github.io/relay/docs/launcher/favorites/](https://abue-ammar.github.io/relay/docs/launcher/favorites/)

macOS: ⇧⌘F, ⌥⌘↑/↓ reorder, ⌘1–9/0, compact icons.

Windows: Ctrl+F pin, empty-query section. **Missing:** reorder, number-key launch, compact strip.

### Aliases
[https://abue-ammar.github.io/relay/docs/launcher/aliases/](https://abue-ammar.github.io/relay/docs/launcher/aliases/)

macOS: per-item field in Settings, tag in the list, Raycast import.

Windows: type an item id by hand on the Features pane. **Missing:** in-pane alias field on each row; Settings search for the item.

### Fallbacks
[https://abue-ammar.github.io/relay/docs/launcher/fallbacks/](https://abue-ammar.github.io/relay/docs/launcher/fallbacks/)

macOS: “Use … with” under every search (AI, Search Files, Run Shell, argument quicklinks). Order **not** backed up.

Windows: **absent.** High-value add.

### System Settings (OS panes)
[https://abue-ammar.github.io/relay/docs/launcher/system-settings/](https://abue-ammar.github.io/relay/docs/launcher/system-settings/)

macOS: every System Settings pane, localized, hide/alias/shortcut.

Windows: 30 hardcoded `ms-settings:` URIs. **Improve:** fuller catalog + hide/alias per pane.

### System actions
[https://abue-ammar.github.io/relay/docs/launcher/system-actions/](https://abue-ammar.github.io/relay/docs/launcher/system-actions/)

macOS: **31** actions (docs sometimes say 31; older catalog said 32). Groups: session, media, volume (including Set Volume… slider), desktop, files, Bluetooth.

Windows: **30** (Stage Manager **Drop**). Real lock/sleep/restart/media/volume keys/recycle/eject/hidden files. Weak: Set Volume (HUD only), Bluetooth (opens Settings), Dismiss notifications (Win+A).

**Improve:** volume picker dialog, actual Bluetooth radio toggle if we can do it without hacks, hide/alias/shortcut per action in a System Actions pane.

### Commands (built-in + custom)
[https://abue-ammar.github.io/relay/docs/launcher/commands/](https://abue-ammar.github.io/relay/docs/launcher/commands/)

macOS: built-ins live on the **feature’s** Settings pane. Custom: zsh, `$1` args, confirmation, output window, env load, Raycast script import.

Windows: `BuiltinCommands` (~19). Custom = name + exe path, confirm, no args UI, no output, no shell fallback.

**Missing:** Commands pane, argument prompts, ConPTY output, Run Shell Command fallback, Raycast scripts.

### Quicklinks
[https://abue-ammar.github.io/relay/docs/launcher/quicklinks/](https://abue-ammar.github.io/relay/docs/launcher/quicklinks/)

macOS: off by default; `{argument}` `{selection}` `{clipboard}` `{date}`…; two-pane search; JSON import/export; Open in new window; confirm delete.

Windows: JSON, `{argument}`/`{query}` only, add-from-Features, seed User folder. Defaults **on**.

**Missing:** placeholder chips, selection/clipboard tokens, import/export, dedicated pane, off-by-default.

### Uninstall an app
[https://abue-ammar.github.io/relay/docs/launcher/uninstall/](https://abue-ammar.github.io/relay/docs/launcher/uninstall/)

macOS: related files, everything to Trash, FDA rows locked, never deletes Relay.

Windows: type 3+ letters, scan AppData folder names, Recycle. **Missing:** installer leftovers, registry QuietUninstall, Start Menu, package family, size list, checkboxes.

### Apple Shortcuts — **Drop**
In the macOS app Settings (Launcher group). Not on the public sidebar. No Windows equivalent.

---

## Features

### Clipboard
[https://abue-ammar.github.io/relay/docs/features/clipboard/](https://abue-ammar.github.io/relay/docs/features/clipboard/)

macOS: on by default. Text/images/files/colors. Pin, type filter ⌘P, OCR **opt-in**, retention, ignored apps (Keychain/Passwords), paste vs copy, keep-open.

Windows: SQLite + FTS, images, files, OCR **always on**, pin, preview pane. Search cap 80.

**Missing:** type filters, colors, ignored apps, OCR switch, retention, default-action, keep-open paste, pin number keys.

### Calculator
[https://abue-ammar.github.io/relay/docs/features/calculator/](https://abue-ammar.github.io/relay/docs/features/calculator/)

macOS: always on. Units, 159 currencies + crypto (24h), dates/TZ, hex, **color card**, history screen.

Windows: real Core engine (units, TZ, currency, percent, compact k, history). FX 6h, **no crypto**, **no color card**. Strong feature; polish the card chrome and add `#rgb` / color copy.

### Snippets
[https://abue-ammar.github.io/relay/docs/features/snippets/](https://abue-ammar.github.io/relay/docs/features/snippets/)

macOS: off; `.md` + frontmatter; keyword expansion = consent; full Raycast placeholders; argument UI; conflict editor; folder on disk.

Windows: JSON; keyword hook when enabled; subset of tokens; missing argument → HUD, no prompt; add boxes on Features (no list).

**Missing:** `.md` files, editor, argument form, modifiers, `{date format=}`, Show in launcher, dedicated pane.

### Notes
[https://abue-ammar.github.io/relay/docs/features/notes/](https://abue-ammar.github.io/relay/docs/features/notes/)

macOS: off; separate floating window; `.md`; Search Notes; Create Note; snippets inside the editor.

Windows: floating `NotesWindow`, autosave, New/Delete. Defaults **on**. **Missing:** Search Notes command, folder reveal, off-by-default.

### File search
[https://abue-ammar.github.io/relay/docs/features/file-search/](https://abue-ammar.github.io/relay/docs/features/file-search/)

macOS: Spotlight, no extra permission, recents, type filter, Quick Look, copy path/name, Trash.

Windows: disk walk + volume drill-down (`D` → `D:` → folders). Filters, Recycle. **Missing:** Windows Search / Everything indexer, preview pane, paste-file-into-previous-app. Volume browse is a Windows-native extra worth keeping.

### Calendar & meetings
[https://abue-ammar.github.io/relay/docs/features/calendar/](https://abue-ammar.github.io/relay/docs/features/calendar/)

macOS: join card on empty launcher, Join Next, schedule, create event, copy link, optional menu-bar item, auto-join, camera preview. Consent flags never backed up.

Windows: `AppointmentManager` 7-day list, Schedule mode, auto-join if both flags on. **Missing:** empty-launcher join card, Join Next / Create Event / Copy link / Open in Calendar, tray title, per-calendar exclude.

### Camera
[https://abue-ammar.github.io/relay/docs/features/camera/](https://abue-ammar.github.io/relay/docs/features/camera/)

macOS: no feature switch. Borderless panel; photo → clipboard PNG; mirror; switch camera; click-outside closes.

Windows: gated on `CameraPreview`; preview or “open Camera app”. **Missing:** snapshot to clipboard, cycle devices, mirror, click-outside dismiss.

### Navigation
[https://abue-ammar.github.io/relay/docs/features/navigation/](https://abue-ammar.github.io/relay/docs/features/navigation/)

macOS: Switch Windows (all Spaces, restore minimized) + Search Menu Bar (front app, 4000 items, exclude apps).

Windows: window list + focus. Menu search = classic `GetMenu` only (“No classic menu bar” for most WinUI/WPF apps).

**Missing:** UI Automation menu walk, exclude-app list, virtual-desktop-aware switcher.

### Window management
[https://abue-ammar.github.io/relay/docs/features/window-management/](https://abue-ammar.github.io/relay/docs/features/window-management/)

macOS: **35** commands, gap 0–64, cycling ½-⅓-⅔ or displays, custom sizes, restore memory.

Windows: **35** IDs, engine is real. Fullscreen = F11; spaces = Win+Ctrl arrows. Gap hardcoded 8. No Settings for gap/cycle/custom sizes. Flag defaults **on**.

**Improve:** Settings pane (gap, cycle, show in launcher, per-command shortcut). Real fullscreen. Display cycle UI.

### Window layouts
[https://abue-ammar.github.io/relay/docs/features/window-layouts/](https://abue-ammar.github.io/relay/docs/features/window-layouts/)

macOS: visual editor (per-display, 3×3, launch missing apps).

Windows: save process+frame; restore first window of that process. **Missing:** editor, monitor identity, launch missing apps.

### Emoji & symbols
[https://abue-ammar.github.io/relay/docs/features/emoji/](https://abue-ammar.github.io/relay/docs/features/emoji/)

macOS: always on; generated Emoji-16 grid; pins; frequent; skin; 6–10 columns.

Windows: **50** hardcoded glyphs in a **list**. Stub.

**Missing:** generated catalog, grid, pins, skin tone, columns.

---

## AI

### AI Chat
[https://abue-ammar.github.io/relay/docs/ai/](https://abue-ammar.github.io/relay/docs/ai/)

macOS: Apple Intelligence, CLI (Codex/Claude/OpenCode), BYO keys, attachments, web search, history DB, idle timeout. **No AI setting in backups.**

Windows: OpenCode spawn + HTTPS key fallback. Palette chat. JSON history.

**Missing:** attachments, streaming that survives hide, model picker, web search, Apple Intelligence analog (Windows Copilot Runtime — optional later), conversation list.

### Quick Actions
[https://abue-ammar.github.io/relay/docs/ai/quick-actions/](https://abue-ammar.github.io/relay/docs/ai/quick-actions/)

macOS: Fix Grammar, Rewrite, Translate (on-device), Summarize, custom; result overlay. Nothing backed up.

Windows: flag stores clipboard text as “Selected text” and **copies** it. JSON list never run.

**Missing:** the whole feature. Highest-impact AI add after chat polish.

### MCP
[https://abue-ammar.github.io/relay/docs/ai/mcp/](https://abue-ammar.github.io/relay/docs/ai/mcp/)

macOS: HTTP or local command, trust levels, `@handle`, idle-stop. Switch + list never backed up.

Windows: `McpServerSpec` JSON + status string. **No client, no editor.** Stub.

---

## Extensions
[https://abue-ammar.github.io/relay/docs/extensions/](https://abue-ammar.github.io/relay/docs/extensions/)  
Install / compatibility / configuring are sibling pages.

macOS: Raycast extensions in the **palette** via JavaScriptCore (not Electron). Registries, import from Raycast, configure, aliases, shortcuts.

Windows: toggle + “deferred”. **Not started.** Large, later.

---

## Reference

### Shortcuts
[https://abue-ammar.github.io/relay/docs/reference/shortcuts/](https://abue-ammar.github.io/relay/docs/reference/shortcuts/)

macOS: large table of unchangeable, key-position shortcuts per screen.

Windows: a few (Tab, Esc, Ctrl+K, Ctrl+F, Ctrl+P, Ctrl+Enter). **Missing:** documented, consistent chord set; Emacs-style Ctrl+N/P; number keys.

### Hotkeys
[https://abue-ammar.github.io/relay/docs/reference/hotkeys/](https://abue-ammar.github.io/relay/docs/reference/hotkeys/)

macOS: nothing ships bound; recorder + conflict bubble; double-tap modifiers; Hyper via Caps Lock / right modifiers (`hidutil`). Feature Enable kills that feature’s shortcuts.

Windows: Alt+Space always; extra `RegisterHotKey`; double-tap + Hyper chord via LL hook; **14** bindable IDs in Settings. Caps Lock Hyper = **Drop** (or later).

**Missing:** recorder on every command/app/action row; conflict UI; window-command / system-action / layout / quicklink bindings.

### Backup
[https://abue-ammar.github.io/relay/docs/reference/backup/](https://abue-ammar.github.io/relay/docs/reference/backup/)

macOS: `.relay`; **category checkboxes**; same-version; capability flags excluded.

Windows: zip export/import; always all mirrored keys; flags excluded (correct). **Missing:** ticks, version guard messaging.

### Import from Raycast
[https://abue-ammar.github.io/relay/docs/reference/import-from-raycast/](https://abue-ammar.github.io/relay/docs/reference/import-from-raycast/)

Windows: **absent.**

### Updates
[https://abue-ammar.github.io/relay/docs/reference/updates/](https://abue-ammar.github.io/relay/docs/reference/updates/)

macOS: daily check (30s then 2h), changelog window, signature, never interrupt palette/dialog.

Windows: 30s quiet check, GitHub zip, `.env` token, apply.cmd. No changelog window, no busy-gate, no Authenticode pin, x64-only workflow. Check path works; download-replace not proven on a *newer* tag.

### Support
Polar checkout on the website. In-app titled window + ~monthly reminder.

Windows: Support window (GitHub + license), 30-day reminder, **no Polar URL**. Fine as a Windows stand-in until there is a store/checkout.

---

## Settings copy that was noise (removed from the live window)

These read as engineering notes, not System Settings chrome:

- “Dark is the design. Light inverts the ink.”
- “Start with Windows from the current user Run key.”
- “SQLite FTS. Tab rings here.” / “35 placement commands. No Stage Manager.”
- Permissions paragraph naming AppointmentStore, MediaCapture, HWND hooks
- OpenCode install essay and backtick CLI
- File search DerivedData/Pods (macOS leftovers)
- Hotkeys HWND / keyboard-hook paragraph
- Backup capability-flag essay
- About `.env` / `RELAY_GITHUB_TOKEN` instructions (status line already reports token state)

Kept: control labels, placeholders, dynamic status (`AboutIdentity`, `AboutUpdateStatus`, `HotKeyStatus`, `OpenCodeStatus`).

---

## Scoreboard

| Area | macOS | Windows | Verdict |
| --- | --- | --- | --- |
| Palette shell | Full | Working, thinner | Improve |
| App launcher | Full | Working, thinner | Improve |
| Calculator | Full | Strong | Polish + color/crypto |
| Clipboard | Full | Strong core | Filters, OCR switch, ignored apps |
| File search | Spotlight | Disk walk + volumes | Keep volumes; add indexer |
| Window tiling | 35 + settings | 35, no pane | Add gap/cycle UI |
| Window layouts | Editor | Snapshot | Rebuild |
| System actions | 31 | 30, some weak | Fix volume/BT |
| Notes | Full | Working | Search command |
| Snippets | Full | Partial | Editor + tokens |
| Quicklinks | Full | Partial | Pane + tokens |
| Hotkeys | Every row | 14 IDs | Recorders everywhere |
| Calendar | Full | Partial | Join card + commands |
| Camera | Snapshot panel | Preview | Snapshot + cycle |
| Navigation / menus | AX + menu walk | Win32 menus | UIA |
| AI chat | Multi-provider | OpenCode + HTTPS | Attachments, picker |
| Quick Actions | Full | Stub | Build |
| MCP | Full | Stub | Build |
| Extensions | JSC runtime | Stub | Later |
| Settings shell | 22 searchable panes | 6-button dump | Rebuild (same separate window) |
| Onboarding | 4 steps | None | Add |
| Fallbacks | Yes | None | Add |
| Emoji | Generated grid | 50 list | Generate + grid |
| Raycast import | Yes | None | Add |
| Updates | Sparkle-class | GitHub zip | Changelog + busy-gate |
| Backup ticks | Yes | Always-all | Add |
| Apple Shortcuts | Yes | — | Drop |
| Caps Lock Hyper | Yes | Chord only | Drop / later |
| Stage Manager | Yes | — | Drop |

---

## What to add or improve (priority)

**Product shell (do first — this is what you noticed)**
1. Rebuild Settings as a split view with macOS pane groups (minus Apple Shortcuts). Search. Open a named pane from the palette.
2. Onboarding: pick shortcut, launch at login, skippable.
3. Default new features **off** except clipboard (match Getting started).
4. Ctrl+K actions on apps: Open, Reveal, Favorite, Hide, Restart, Quit, Uninstall.

**Launcher completeness**
5. Fallbacks (AI / Search Files / Run Shell / argument quicklinks).
6. Open in Browser when the query looks like a URL.
7. Favorites reorder + number keys.
8. Per-item alias/shortcut/hide tables (Applications, System Settings, System Actions, Commands).

**Features that exist but are thin**
9. Clipboard type filter, ignored apps, OCR opt-in, retention.
10. Emoji generated grid.
11. Snippet editor + argument prompt + remaining tokens.
12. Quicklink placeholders + import/export.
13. Window gap/cycle/custom sizes; layout editor.
14. Calendar join card + Join Next / Copy link.
15. Camera snapshot to clipboard.
16. Menu search via UI Automation.
17. Custom command args + output.
18. Uninstall leftovers with sizes.

**Stubs to either build or hide the toggle**
19. Quick Actions (Fix Grammar / Rewrite / Translate / Summarize).
20. MCP server editor + actual client.
21. Hide Extensions until there is a runtime.

**Later / large**
22. Raycast `.rayconfig` import.
23. Extensions (JavaScriptCore or equivalent — huge).
24. WinGet / installer, arm64 zip, changelog update window.

**Do not port**
- Apple Shortcuts
- Stage Manager
- Caps Lock `hidutil` Hyper (optional later)
- SwiftUI Settings scene (they did not use it either)
- Putting Settings inside the palette

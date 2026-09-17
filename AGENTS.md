# Relay for Windows

A native Windows 11 tray launcher: fuzzy app launcher, global and per-app hotkeys, clipboard
history, an inline calculator, snippets, quicklinks, window management. WinUI 3 + .NET 9,
unpackaged, running as a tray accessory with no taskbar button. Microsoft packages only.

## Posture: latest-only, always

**Relay for Windows targets current Windows 11 and nothing else.** .NET 9, Windows App SDK
current. There is no Windows 10 floor, no Electron fallback, and no compatibility shim.

Write code as if the platform released yesterday:

- Prefer WinUI 3 / Windows App SDK APIs. Migrate, never wrap.
- A deprecated API is a defect.
- Never introduce backwards compatibility unless explicitly asked.

`RegisterHotKey` and a low-level keyboard hook (only while double-tap, Hyper, or snippet
keywords need them) are capability-gap dependencies, same role Carbon plays on macOS.

## Where things are

| Folder | Holds |
| --- | --- |
| `src/Relay.Core` | Pure models. No WinUI, no Win32. The harness compiles this project. |
| `src/Relay/App` | `App` and `AppCore` — the composition root |
| `src/Relay/DesignSystem` | WinUI mapping of `Theme`; `Theme.cs` in Core is the only token source |
| `src/Relay/Platform` | Win32 shims: tray, hotkeys, paths, login item |
| `src/Relay/Palette` | palette window, coordinator, `PaletteState` lives in Core |
| `src/Relay/Windows` | Settings, Dialogs, HUD, Onboarding |
| `src/Relay/Features` | one folder per feature; Model stays in Core |
| `tests/Relay.Harness` | standalone harness, no VSTest |
| `reference/` | read-only macOS repo |

| Read it before you | Doc |
| --- | --- |
| change how anything is wired | [docs/architecture.md](docs/architecture.md) |
| add or restyle a view | [docs/ui.md](docs/ui.md) and `reference/docs/ui.md` |
| touch a feature | `reference/docs/features/` plus the Windows catalog in `docs/research/` |

## Non-negotiables

- **`AppCore` is the sole owner.** New long-lived state goes on `AppCore`, wired in `Start()`.
  Views reach a feature's **coordinator**, not a store.
- **A file under `Relay.Core` / `Features/*/Model/` may not import WinUI or Win32.**
  The harness compiles Core, so this is enforced by compilation.
- **Dark is the baseline.** `Theme.Colors` ramps resolve per appearance; `AppCore.ApplyAppearance()`
  is the only assignment from `AppSettings.Appearance`.
- **Relay presents its own dialogs** — never `ContentDialog` or `MessageBox`. Questions go through
  `DialogPresenter`, readouts through `MessageHudPresenter`.
- **A capability flag is never carried by a backup:** `SnippetsEnabled`, `AiEnabled`,
  `CalendarEnabled`, `McpEnabled`, `ExtensionsEnabled`, `QuickActionsEnabled`, and related consent
  keys stay in `SettingsBackupCoverage.DeliberatelyExcluded`.
- **Dev vs Release identity:** `com.relay.windows.dev` vs `com.relay.windows`, isolated
  `%APPDATA%` roots.
- **Do not paste Swift** from `reference/`. Reimplement from docs and invariants.

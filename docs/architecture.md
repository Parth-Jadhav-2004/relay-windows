# Architecture (Windows)

macOS source of truth: [`reference/docs/architecture.md`](../reference/docs/architecture.md).
This file records only the Windows composition. Do not fork policy here.

## Layers

Same four layers as macOS:

1. **Pure** — `src/Tinycast.Core`. Foundation / BCL only. The harness compiles this project, so WinUI
   or P/Invoke in Core is a broken build, not a convention.
2. **Effect** — `src/Tinycast/Platform` and `Features/*/Service`. Win32, WinRT, filesystem, HTTP.
3. **Observable state** — stores and `PaletteState` owned by `AppCore`.
4. **View** — WinUI windows. Coordinators are the only mutation surface a view calls.

## Single-owner core

`AppCore.Shared.Start()` is the only boot sequence. `App.OnLaunched` calls it and nothing else.

`AppCore` owns settings, `PaletteState`, the palette window (HWND host for tray + hotkeys),
coordinators, `DialogPresenter`, and `MessageHudPresenter`. Do not add a second singleton.

Dev identity is `com.tinycast.windows.dev`; Release is `com.tinycast.windows`. Settings roots are
`%APPDATA%\Tinycast\<identity>\`.

## Windows mapping

| macOS | Windows |
| --- | --- |
| `LSUIElement` accessory | tray `NOTIFYICONDATA` + `AppWindow.IsShownInSwitchers = false` |
| Carbon `RegisterEventHotKey` | `RegisterHotKey` on the palette HWND |
| `NSPanel` palette | borderless `OverlappedPresenter`, `DesktopAcrylicBackdrop`, 40% scrim |
| `NSAlert` | `DialogPresenter` / `DialogWindow` |
| HUD | `MessageHudPresenter` |
| `SMAppService` | HKCU Run key (`LaunchAtLogin`) |
| Keychain | Credential Locker (later) |

The palette window is created at start and **hidden, never closed**, so its HWND stays valid for
hotkeys and the tray. Settings, onboarding, dialogs, and the HUD have independent lifecycles.

Confirmation lives in coordinators, never runners. Capability flags never ride a backup —
see `SettingsBackupCoverage`.

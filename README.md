# Tinycast for Windows

A native Windows 11 tray launcher: command palette, global hotkeys, clipboard history,
calculator, file search, snippets, window management. **WinUI 3 + .NET 9**, unpackaged. AGPL-3.0.

This is a from-scratch rewrite of [Tinycast for macOS](https://github.com/abue-ammar/tinycast).
It copies architecture, UX, and invariants — not Swift, not Electron, not PowerToys.

## Run

```powershell
dotnet run --project src\Tinycast\Tinycast.csproj -p:Platform=x64
```

Alt+Space opens the palette. The tray icon is in the notification area (you may need to show hidden icons). Right-click it for Settings or Quit.

Isolated settings live in `%APPDATA%\Tinycast\com.tinycast.windows.dev\` for Debug builds, and
`%APPDATA%\Tinycast\com.tinycast.windows\` for Release.

## Updates

Release builds check [GitHub Releases](https://github.com/Parth-Jadhav-2004/tinycast-windows/releases)
for `Tinycast-windows-x64.zip` about 30 seconds after launch, and whenever you click
**Check for updates** in Settings → About (or the palette command). That button becomes
**Download and restart** when a newer tag is ready — there is no extra confirm window.

The repo is private, so the GitHub API needs a token with `repo` scope. Put it in
`TINYCAST_GITHUB_TOKEN` or in `%APPDATA%\Tinycast\com.tinycast.windows\.env`:

```
TINYCAST_GITHUB_TOKEN=ghp_your_token
```

See `.env.example`. The token is never shown in Settings and is never backed up.

Publish a build:

```powershell
git tag v0.3.5
git push origin v0.3.5
```

The Release workflow publishes a self-contained zip. Dev (`DEBUG`) builds do not install those zips.

## Tests

```powershell
.\scripts\run-tests.ps1
```

The harness compiles and asserts the pure Core models. `Tinycast.Core` must not import WinUI or P/Invoke.

## Layout

| Path | Holds |
| --- | --- |
| `src/Tinycast.Core` | Pure models. No WinUI, no Win32. |
| `src/Tinycast` | AppCore, tray, palette, Settings, Dialog/HUD, Win32 shims |
| `tests/Tinycast.Harness` | Standalone harness (no XCTest equivalent) |
| `docs/` | Windows architecture + UI deltas |
| `prototype/` | Throwaway HTML look-dev |

`reference/` is a local clone of the macOS spec repo. It is gitignored; do not paste Swift from it.

## License

GNU Affero General Public License v3.0. See `LICENSE` and `NOTICE`.

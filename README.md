# Relay for Windows

A native Windows 11 command palette and productivity launcher. Relay lives in the system tray — no taskbar button — and opens with a global hotkey whenever you need to find an app, run a command, or reach a feature.

Built with **WinUI 3** and **.NET 9**. Unpackaged, Microsoft packages only. Targets current Windows 11.

## Getting started

```powershell
dotnet run --project src\Relay\Relay.csproj -p:Platform=x64
```

Press **Alt+Space** to open the palette. Right-click the tray icon for Settings or Quit.

## Features

### Command palette

- Fuzzy search across apps, settings, commands, and built-in actions
- Inline calculator with unit, currency, timezone, and color conversions
- Keyboard-first navigation with a compact, translucent palette window

### Launcher

- Application search with aliases, favorites, and usage-based ranking
- Shortcuts to Windows Settings pages
- System actions — lock, sleep, restart, volume, show desktop, and more
- Custom shell commands with captured output
- Quicklinks for URLs, searches, and clipboard-driven workflows
- Configurable fallbacks when no direct match is found

### Productivity

- **Clipboard history** — browse, pin, filter, and preview past copies; optional OCR on images
- **Snippets** — keyword expansion with templates, arguments, and frontmatter
- **Notes** — markdown notes with search and a floating editor
- **File search** — fast indexed search across configured folders and drives
- **Emoji & symbols** — searchable grid with skin tones and pinning

### Window & navigation

- **Window management** — 35 tiling actions (halves, thirds, quarters, maximize, center, move between displays, and more)
- **Window layouts** — save and restore multi-window arrangements
- **Navigation** — window switcher and in-app menu search for the focused application

### AI & automation

- **AI chat** — conversational assistant with configurable models and MCP server support
- **Quick actions** — run actions on selected text (grammar, translation, and custom prompts)

### Calendar

- Upcoming meetings with join links and optional auto-join

### Hotkeys

- Global palette chord (default Alt+Space)
- Per-app and global hotkeys for any Relay command or action
- Double-tap and Hyper key modifiers

### Settings & data

- Full settings UI organized by General, Launcher, Features, and Advanced
- Settings backup and restore with selective categories
- First-launch onboarding wizard
- Light, dark, and system appearance

## License

GNU Affero General Public License v3.0. See [LICENSE](LICENSE) and [NOTICE](NOTICE).

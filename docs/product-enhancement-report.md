# Relay for Windows: Product Enhancement, UI/UX Modernization & Roadmap Report

> **Document Version:** 1.0.0  
> **Target Architecture:** Windows 11 (x64 / ARM64), WinUI 3, Windows App SDK, .NET 9  
> **Codebase Scope:** `src/Relay`, `src/Relay.Core`, `tests/Relay.Harness`  
> **Date:** September 2026

---

## Table of Contents
1. [Executive Summary & Current State Analysis](#1-executive-summary--current-state-analysis)
2. [UI & Visual Experience Modernization (Windows 11 Fluent Design)](#2-ui--visual-experience-modernization-windows-11-fluent-design)
3. [Performance & Execution Speed Optimizations](#3-performance--execution-speed-optimizations)
4. [Feature Representation & Usability Refinements](#4-feature-representation--usability-refinements)
5. [First-Time Installation & Personalized Onboarding Experience](#5-first-time-installation--personalized-onboarding-experience)
6. [High-Value New Features & System Capabilities](#6-high-value-new-features--system-capabilities)
7. [Architecture & Technical Debt Remediation](#7-architecture--technical-debt-remediation)
8. [Phased Implementation Roadmap](#8-phased-implementation-roadmap)

---

## 1. Executive Summary & Current State Analysis

**Relay** is a native, unpackaged Windows 11 productivity launcher and tray companion application written in C# (.NET 9) with WinUI 3. It mirrors the speed, design invariants, and utility of modern desktop command launchers (e.g., Raycast, Alfred, Spotlight) while adapting strictly to Windows paradigms.

### 1.1 Architecture Highlights
The project is built on a four-tier architecture:
1. **Pure Domain Core (`src/Relay.Core`)**: Business models, tokenizer/math parser, snippet templates, ranking, and search ranking algorithms. Free of WinUI and Win32 P/Invoke dependencies.
2. **Platform & Effects (`src/Relay/Platform`)**: Low-level Win32 hooks (`SetWindowsHookEx`), WinRT APIs (`Windows.ApplicationModel`, `PackageManager`), SQLite FTS indexer, and process controls.
3. **Observable State (`AppCore`, `PaletteState`, Coordinators)**: Single-owner state store managing active query, search results, hotkey registrations, and background tasks.
4. **View Layer (`src/Relay/Palette`, `Windows/*`)**: WinUI 3 presentation windows.

### 1.2 Identified Opportunities for Growth
While the core architecture is sound, an in-depth audit reveals significant opportunities across four dimensions:
- **Visual Design Inconsistency**: The main launcher palette implements custom desktop acrylic and border clipping, but secondary surfaces (`SettingsWindow.xaml`, `OnboardingWindow.xaml`, `NotesWindow.xaml`) rely on bare, unstyled controls without the polish of Windows 11 Fluent cards or animations.
- **Rendering & Keystroke Latency**: Dynamic UI element generation in `PaletteWindow.xaml.cs` (clearing and rebuilding `StackPanel` rows on every keystroke) introduces GC pressure and layout overhead.
- **Onboarding & First-Time User Experience (FTUE)**: The current onboarding flow is a bare 440×280 modal with generic buttons, missing brand identity, workflow personalization, and interactive tutorials.
- **Feature Discoverability**: Powerful utilities (e.g., window snapping, snippet expansions, clipboard OCR, calculator units) lack rich visual preview representations such as syntax highlighting, interactive chips, and inline action triggers.

---

## 2. UI & Visual Experience Modernization (Windows 11 Fluent Design)

### 2.1 Palette Window (`src/Relay/Palette/PaletteWindow.xaml`)

```
+---------------------------------------------------------------------------------------------+
|  [Search Glyph]  Search apps, files, commands, or math...       [ Mode Pills: All | Clip | Files ]  |
+---------------------------------------------------------------------------------------------+
|  CATEGORIES / RESULTS                                    |  PREVIEW PANE (Split View)         |
|  ------------------------------------------------------+------------------------------------|
|  [App Icon]  Visual Studio Code            [Enter Open]|  File: launch.json                 |
|              C:\Users\...\Code.exe                      |  Size: 1.8 KB  Modified: 4m ago    |
|                                                         |  --------------------------------  |
|  [Calc Icon] 128 * 1024 = 131,072          [Copy]       |  Syntax Highlighted Preview        |
|              = 128 KiB                                  |  ```json                           |
|                                                         |  {                                 |
|  [Clip Icon] "git commit -m 'feat: ui'"   [Paste]      |    "version": "0.2.0"              |
|              Terminal • 12s ago                         |  }                                 |
+---------------------------------------------------------------------------------------------+
|  [Menu Circle] Quick actions (Ctrl+K)                  [Capsule: Run as Admin (Ctrl+Enter)]  |
+---------------------------------------------------------------------------------------------+
```

1. **Backdrop & Elevation Depth**:
   - Upgrade from flat dark/light scrims to **Desktop Acrylic with Elevation Glow**: Combine `DesktopAcrylicBackdrop` with an outer border brush using `SurfaceStrokeColorDefaultBrush` and an inner 1px soft highlight (`ElevationBorder`).
   - Standardize corner radii using DWM window attributes (`DWMWA_WINDOW_CORNER_PREFERENCE = DWMWCP_ROUND`).
2. **Search Input & Dynamic Scope Pills**:
   - Integrate scope filter chips into the search box header: `[ All ]`, `[ Clipboard ]`, `[ Files ]`, `[ AI ]`, `[ Snippets ]`. Users can navigate scopes using `Tab` / `Shift+Tab` or hotkeys (`Ctrl+1`, `Ctrl+2`).
   - Add micro-animations to the search glyph: smooth transition to an indeterminate circular progress ring during async index queries.
3. **Row Item Design & Typography**:
   - **Visual Result Badges**: Add subtle pill tags for result types (`App`, `Command`, `Calculator`, `File`, `Setting`) using system pastel tints.
   - **Keyboard Keycap Accents**: Render keyboard shortcuts (e.g. `↵`, `Tab`, `Ctrl+K`) inside embossed keycap badges with subtle 3D border depth (`BorderThickness="1"`, `CornerRadius="4"`).
   - **Fuzzy Match Highlighting**: Render text matches using `TextBlock.Inlines` with `Run` elements styled with semi-bold weight and accent color (`SystemAccentColorLight2`).
4. **Fluid Micro-Interactions**:
   - Implement WinUI Composition Spring animations: rows subtly scale (1.00 -> 1.01) and transition background color over 80ms on hover and selection.

---

### 2.2 Settings Window (`src/Relay/Windows/Settings/SettingsWindow.xaml`)

The settings surface should align with the modern Windows 11 Settings architecture:
- **Adopt `SettingsCard` & `SettingsExpander`**: Replace bare horizontal stacks and raw textboxes with structured cards featuring leading icons, titles, secondary descriptive subtitles, and right-aligned interactive controls (toggles, combos, buttons).
- **Navigation Sidebar Enhancement**:
  - Replace the plain `StackPanel` with a styled `NavigationView` containing categorized sections (`General`, `Keyboard Shortcuts`, `Clipboard & Privacy`, `Window Snapping`, `Integrations & AI`, `About`).
  - Add search filtering across settings cards with live result highlighting.
- **Interactive Hotkey Recorder**:
  - Replace raw textboxes with an interactive keyboard chord recorder component that visually depicts active keypresses (Ctrl, Alt, Shift, Win + Key) and flags conflicts immediately with warning badges.

---

### 2.3 Notes & Floating Scratchpad (`src/Relay/Windows/Notes/NotesWindow.xaml`)

- **Dual-Pane Markdown Experience**:
  - Add split-view mode: live markdown editing on the left, rendered HTML/RichText on the right (via `Markdig` and WinUI `RichTextBlock`).
- **Quick Floating Actions**:
  - Add **"Always on Top"** pin button for floating reference while coding or taking notes.
  - Action bar shortcuts: "Copy Plain Text", "Copy Formatted", "Export to Obsidian/Notion", "Clean Whitespace".
  - Status bar showing word count, reading time, and auto-save state.

---

## 3. Performance & Execution Speed Optimizations

### 3.1 Virtualized List Rendering in `PaletteWindow`
* **Current Issue**: In `PaletteWindow.xaml.cs`, every keystroke executes `RowHost.Children.Clear()` and reallocates 15 to 40 complete WinUI XAML visual trees (Grids, Borders, TextBlocks, Buttons).
* **Optimization**:
  - Replace `StackPanel` with `ItemsRepeater` or virtualized `ListView` with a custom `DataTemplateSelector`.
  - Maintain a pre-allocated pool of row visual containers.
  - Update row text and glyph bindings in-place without tearing down visual trees.
  - **Latency Impact**: Reduces render time per keystroke from **18–35ms** down to **< 2ms**, delivering locked 60/120 FPS scrolling and typing.

### 3.2 Asynchronous Debounced Search Channels
* **Current Issue**: Rapid typing triggers simultaneous searches across all coordinators (Applications, Shell index, SQLite FTS, Calculator, File search) synchronously on the dispatcher.
* **Optimization**:
  - Partition search providers into **Instant Providers** (App cache, Calculator, System Actions: 0ms delay) and **Deferred Providers** (File Search, Full-Text Clipboard, AI: 35ms trailing debounce via `System.Threading.Channels.Channel<string>`).
  - Cancel in-flight background searches via `CancellationTokenSource` when the query string changes.

### 3.3 Non-Blocking Application Indexing (`AppIndex.cs`)
* **Current Issue**: `AppIndex.cs` calls `.GetAwaiter().GetResult()` on WinRT `GetAppListEntriesAsync()` in a loop during startup, blocking the UI thread and causing cold-start delay.
* **Optimization**:
  - Shift package scanning entirely to background workers via `Task.Run`.
  - Cache discovered applications in a compact local cache file (`%APPDATA%/Relay/cache/apps-index.json`).
  - Subscribe to `Windows.ApplicationModel.PackageCatalog.PackageInstalling` and `PackageUninstalling` events for incremental live updates without full rescans.

### 3.4 Low-Level Keyboard Hook Optimization (`HotKeyCenter.cs`)
* **Current Issue**: `HotKeyCenter.cs` executes `Process.GetProcessById` on every keypress inside the low-level keyboard hook to check per-app exclusions.
* **Optimization**:
  - Track active window changes globally using `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)`.
  - Store the active process identity in a volatile field. The low-level keyboard hook checks this pre-cached value in $O(1)$ time, eliminating process handle allocations and kernel transitions.

### 3.5 Bounded Memory Cache for Clipboard Thumbnails
* **Optimization**:
  - Implement a bounded Least-Recently-Used (LRU) cache (capped at 50 decoded bitmaps) in `ClipboardThumbnails.cs`.
  - Ensure decoded `MemoryStream` and `SoftwareBitmap` objects are deterministically disposed when evicted to prevent memory leaks during heavy image copying.

---

## 4. Feature Representation & Usability Refinements

| Feature Area | Current Representation | Proposed Upgraded Representation |
| :--- | :--- | :--- |
| **Calculator & Conversions** | Single line: result as title, formula as subtitle. | **Interactive Unit Card**: Multi-currency output (e.g. `$100` shows EUR, GBP, JPY simultaneously), visual equation tree, percentage breakdown cards, and click-to-copy tokens. |
| **Clipboard History** | Raw text list; basic image preview. | **Smart Content Chips**: Syntax-highlighted code snippets, interactive color swatch cards for hex/RGB colors (`#3B82F6`), link previews with favicons, and image EXIF metadata badges (resolution, format, file size). |
| **File Search** | Filename and path text only. | **Quick Look Preview (Spacebar)**: Integrated modal file preview supporting Markdown, JSON, PDF, image zoom, and code syntax highlighting without opening external applications. |
| **Window Snapping** | Raw numeric grid coordinates in settings textboxes. | **Visual 3×3 Grid Picker**: Interactive desktop canvas representation in the palette. Hovering over slots visually highlights the target monitor quadrant or third. |
| **Snippets** | Plain string template replacement. | **Dynamic Form Fillers**: When expanding snippets with arguments (e.g. `{name}`, `{date}`, `{ticket}`), open a clean inline form overlay in the palette with tab-indexed input pills before pasting. |
| **AI Assistant** | Plain unformatted chat strings. | **Streaming Markdown Renderer**: Live token streaming, code blocks with "Copy" and "Insert" buttons, token usage counters, and quick prompt follow-up chips. |

---

## 5. First-Time Installation & Personalized Onboarding Experience

The initial launch sets the tone for user adoption. The current 440×280 modal should be replaced with a modern, guided setup journey.

```
+---------------------------------------------------------------------------------------------+
|  RELAY SETUP WIZARD                                                       [Step 1 of 4]  (X)|
+---------------------------------------------------------------------------------------------+
|                                                                                             |
|        ⚡ Welcome to Relay                                                                  |
|        Your lightning-fast companion for Windows 11.                                       |
|                                                                                             |
|   Choose your primary workspace profile:                                                    |
|                                                                                             |
|   +---------------------+   +---------------------+   +---------------------+               |
|   | 💻 Developer        |   | ✍️ Writer / Office   |   | 🚀 Power User       |               |
|   | Git, Terminal,      |   | Clipboard OCR,      |   | Window Snap,        |               |
|   | Snippets, VS Code   |   | Notes, Calendar     |   | Custom Commands     |               |
|   +---------------------+   +---------------------+   +---------------------+               |
|                                                                                             |
|                                                               [ Skip ]    [ Continue -> ]   |
+---------------------------------------------------------------------------------------------+
```

### 5.1 Frictionless Distribution & Installation
- **Windows Package Manager (`winget`)**: Publish official package manifests (`winget install relay`).
- **Modern User-Scope Installer**: Package as a self-contained, per-user installer (installing to `%LOCALAPPDATA%\Programs\Relay`) requiring zero administrative UAC prompts, automatically placing the tray icon and autostart registration.

### 5.2 4-Step Interactive Onboarding Wizard (`OnboardingWindow`)
1. **Step 1: Persona-Driven Preset Selection**:
   - **Developer**: Pre-configures developer quicklinks (GitHub, StackOverflow), terminal commands, code snippets, and path browsing.
   - **Productivity & Office**: Pre-configures clipboard OCR, upcoming meeting auto-join, and Markdown notes.
   - **Power User**: Enables window snapping layouts, system quick toggles, regex file search, and AI assistant.
2. **Step 2: Interactive Hotkey Configuration**:
   - Visual interactive keyboard graphic displaying `Alt + Space`.
   - Real-time conflict detection: If `Alt + Space` is taken (e.g. by PowerToys Run), the wizard gracefully suggests `Ctrl + Space` or `Win + Shift + Space` with one-click resolution.
3. **Step 3: Interactive Sandbox ("Take a Test Drive")**:
   - The user is invited to press their chosen shortcut right inside the wizard to trigger a simulated palette search.
   - Micro-challenges: "Type `calc 45 * 12`", "Type `clip` to view clipboard history".
   - Rewarding micro-animation (confetti or checkmark glow) upon completion.
4. **Step 4: Smart Permissions & Background Preferences**:
   - Clear explanatory cards for background run at startup, calendar integration, and system tray visibility with toggles.

---

## 6. High-Value New Features & System Capabilities

### 6.1 System Quick Toggles & Control Center
Integrate instant system controls directly from the palette:
- **Audio Controls**: Mute/Unmute microphone, toggle audio output device (Headphones ↔ Speakers), set volume level.
- **Display & Power**: Lock screen, sleep displays, empty recycle bin, toggle Dark/Light mode, flush DNS cache (`ipconfig /flushdns`).
- **Network**: Toggle Wi-Fi / Bluetooth status, display local IP and public IP with one click to copy.

### 6.2 Windows Terminal & WSL Profile Integration
- Detect installed Windows Terminal profiles and WSL distributions (`Ubuntu`, `Debian`, `Arch`).
- Direct palette actions: `wsl: Open Bash in ~/projects`, `wt: Open PowerShell as Admin`.

### 6.3 Contextual Global AI Actions
- Dedicated global chord (e.g. `Alt + Shift + Space`) that reads the currently selected text in any Windows app and opens an instant contextual menu:
  - *Summarize Selection*
  - *Fix Grammar & Tone*
  - *Explain Code / Stack Trace*
  - *Convert to JSON / Markdown Table*
  - *Translate to Selected Language*

### 6.4 Windows System Media Transport Controls (SMTC)
- Integrate with WinRT `GlobalSystemMediaTransportControlsSessionManager`.
- When media is playing (Spotify, Apple Music, YouTube), display an active playback card in the launcher with album art, track title, scrubber bar, and media controls (Play/Pause, Previous, Next).

### 6.5 Script Plugin & Extension Engine
- Create an open extension folder (`%APPDATA%\Relay\extensions\`).
- Support executable scripts in Python, Node.js, PowerShell, and C# Scripting (`.csx`).
- Relay passes query input via JSON on `stdin` and reads list item results from `stdout`, opening the door to community-driven integrations.

---

## 7. Architecture & Technical Debt Remediation

Prior to visual rollouts, key reliability issues documented in `KNOWN_ISSUES.md` should be addressed:
1. **Atomic Settings & Backup Writes**: Replace direct `File.WriteAllText` in `SettingsStore.cs` with write-to-temporary-file and `File.Replace` to prevent corrupted configurations upon sudden power loss or crashes.
2. **SQLite FTS Index Synchronization**: Ensure external-content FTS entries in `ClipboardStore.cs` are purged prior to deleting backing content rows to prevent FTS index corruption.
3. **Child Process & Command Injection Hardening**: Sanitize command execution paths in `OpenCodeServerManager.cs` and `CommandProcess.cs`, eliminating unescaped shell invocations.
4. **Dialog Window Dispatcher Deadlock Prevention**: Implement timeout guards and exception unwrapping in `DialogPresenter.cs` so rejected dispatcher queues do not deadlock subsequent confirmation dialogs.

---

## 8. Phased Implementation Roadmap

```mermaid
graph TD
    subgraph Phase 1: Core Performance & Hardening
        P1A[Address Critical Storage & FTS Bugs]
        P1B[ItemsRepeater Virtualized Row Pooling]
        P1C[Asynchronous Debounced Search Pipeline]
        P1D[Hook Process Lookup Optimization]
    end
    subgraph Phase 2: Design System & Onboarding
        P2A[Redesign 4-Step Interactive Onboarding]
        P2B[SettingsCard & NavigationView in Settings]
        P2C[Windows 11 Acrylic & Elevation Styling]
        P2D[Dual-Pane Markdown Scratchpad]
    end
    subgraph Phase 3: Power Features & Ecosystem
        P3A[Fuzzy Match Highlighting & Rich Previews]
        P3B[System Quick Toggles & SMTC Media Card]
        P3C[Contextual AI Quick Actions]
        P3D[Community Script Plugin Engine]
    end
    Phase 1 --> Phase 2 --> Phase 3
```

### Milestone Schedule

| Phase | Focus Area | Key Deliverables | Expected Impact |
| :--- | :--- | :--- | :--- |
| **Phase 1** | **Stability & Speed** | - Implement `ItemsRepeater` row recycling in `PaletteWindow`<br>- Asynchronous debounced search channels<br>- Fix atomic settings writes & FTS delete order<br>- Low-level hook foreground PID caching | Keystroke response < 2ms;<br>Zero UI freezes;<br>Robust state persistence. |
| **Phase 2** | **Visual Polish & FTUE** | - 4-step onboarding wizard with profile presets<br>- Settings window overhaul using `SettingsCard`<br>- Desktop Acrylic elevation and keycap badges<br>- Markdown preview in Notes window | Professional Windows 11 feel;<br>Higher first-run conversion;<br>Intuitive settings discovery. |
| **Phase 3** | **Power-User Features** | - Fuzzy search character highlighting<br>- QuickLook spacebar preview pane<br>- System quick toggles & Media Controller card<br>- Contextual AI selection assistant<br>- File-based script plugin SDK | Feature parity with Raycast/Alfred;<br>Complete daily-driver utility for power users. |

---

*Report prepared for the Relay Windows development repository.*

# UI & Design System (Windows)

macOS source of truth: [`reference/docs/ui.md`](../reference/docs/ui.md) and
`Relay.Core/DesignSystem/Theme.cs`. Dark is the design. Light inverts the ink.

## Surface

- Palette surface = **Desktop Acrylic + Theme.Colors.PanelScrim** (0.40 dark / 0.55 light).
- No gray chrome, no `ContentDialog`, no `MessageBox`.
- Panel radius **26**, row **10**, dialog **20**. Clip the root visual; do not rely on DWM's 8px round.
- Header and footer float over the list. Rows dissolve under the bars (gradient mask), they do not clip.
- Glass (frost alpha) only on the action capsule and menu circle.
- Window size is owned in code (`WindowChrome.ResizeDips`). XAML does not drive the HWND frame.
- Test over a **light wallpaper**. Transparency bugs hide on a dark desktop.

`AppCore.ApplyAppearance()` is the only place `AppSettings.Appearance` becomes `ElementTheme`.
`.System` maps to `ElementTheme.Default`.

## Settings

Settings is a titled Mica window with a 215px sidebar and grouped forms. It does **not** use the
palette recipe. Interface size never scales Settings, Onboarding, Support, or About.

## Tokens

Never hardcode a spacing, radius, or size that exists on `Theme`. Add a token instead.
`InterfaceMetrics` scales palette-adjacent surfaces only; Settings reads `Theme` unscaled.

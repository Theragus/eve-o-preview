# Changelog

All notable changes to EVE-O Preview are listed here. The section for a version is used
verbatim as the GitHub Release notes when that version is published, so keep each entry
user-facing: what changed and why it matters, not how it was implemented.

Format: `## [version] - YYYY-MM-DD` headings, newest first, with `Added`, `Changed`, `Fixed`
subsections as needed.

## [10.0.0.19] - 2026-10-08

### Changed
- The Robin helper DLL is now loaded from `%LocalAppData%\Eve-O Preview\Robin` instead of the
  temp folder, and the application no longer embeds its libraries with Costura. Both reduce
  antivirus false positives; behaviour is unchanged.
- The FPS / Audio tab and the README now state that only these two features load a helper into
  the EVE client, and that nothing is injected while both are off.

## [10.0.0.18] - 2026-10-08

### Fixed
- Settings window: the tab strip no longer stays light grey in dark mode. Tabs, strip and page
  border now follow the theme, and the selected tab shows a gold accent bar.

## [10.0.0.17] - 2026-10-08

### Added
- The settings window and tray menu follow the Windows light/dark theme (Windows 11).

### Fixed
- Previews no longer keep a keyboard-hook subscription while "Clicks pass through previews" is off.

## [10.0.0.16] - 2026-10-08

### Fixed
- Internal: keyboard hook for the click-through modifier is only registered while the option is enabled.

## [10.0.0.15] - 2026-10-08

### Added
- "Clicks pass through previews unless this key is held" (General tab) with a choice of Alt, Ctrl or
  Shift. While enabled, mouse input goes to whatever is behind a preview; hold the chosen key to
  hover, right-click or click a preview to switch to that client. Replaces the 10.0.0.14 "Alt + click"
  option, which has to be re-enabled once.

## [10.0.0.14] - 2026-10-08

### Added
- Option to only switch clients with Alt + click on a preview (superseded in 10.0.0.15).

### Fixed
- Tray icon menu could open underneath the taskbar, hiding the Exit entry.

## [10.0.0.13] - 2026-10-08

### Added
- GitHub Actions build: every push produces a downloadable build, and releases are published from
  the Actions tab.

### Fixed
- Windows 11 power throttling: EVE clients hidden behind other windows were scheduled as low
  priority (EcoQoS) with a coarse timer, so their previews stuttered until the client was focused.
  EVE-O Preview now opts every client out of that throttling while it runs.
- CPU affinity is now also updated when you switch clients outside EVE-O Preview (Alt+Tab,
  taskbar, clicking the game window). Previously the newly focused client kept its background
  CPU mask until the next hotkey or preview click.

# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

PadSharp is a small, fast, cross-platform **scratchpad** editor in C#, built with Avalonia + AvaloniaEdit.
Design aim: open instantly, never lose text (hot exit), stay out of the way. Prefer cutting to adding.

C# 15 on .NET 11; must stay cross-platform (Windows, macOS, Linux) with no Windows-only libraries. The only
Windows-specific call is the optional one that puts the `--screenshot` window behind others, skipped elsewhere.

## Commands

There is no test project.

- Build (this is also the lint): `dotnet build`
- Run: `dotnet run --project PadSharp.csproj -- [files...]`
- Visual check without taking focus: `bin/Debug/net11.0/PadSharp.exe [files...] --screenshot out.png`
  (renders ~120 frames behind other windows, saves a PNG of the window, exits 0). This is how changes are
  verified. Only the main window is captured, not menus or dialogs.
- Native AOT release: `dotnet publish PadSharp.csproj -c Release -r win-x64` (or `linux-x64`, `osx-arm64`).

A running PadSharp locks `bin/Debug` and makes the build fail with MSB3061; close it first.

## Lint and AOT rules (enforced by the build)

`TreatWarningsAsErrors` with .NET analyzers at `AnalysisMode=Recommended`, code style enforced in build
(`.editorconfig`), XML docs generated. `PublishAot=true` also turns on the trim/AOT analyzers for every
normal build, so AOT-unsafe code fails `dotnet build`. Consequences:

- No reflection-based JSON: add types to the source-generated `SettingsJson` context (`AppSettings.cs`).
- No `new StyleInclude(uri)` in code; include XAML styles from compiled XAML (see `src/EditorStyles.axaml`).
- Self-assignment, culture-less string ops etc. are errors (CA rules); CA1303/CA1305-family are relaxed in `.editorconfig`.
- Highlighting definitions are read from embedded XML resources, not files.

On publish, the ILC prints two notes about `CreateTargetForHwnd` in Avalonia.Win32's COM vtables; they come
from the package and are harmless.

## Architecture

The UI is built in code (no XAML views); `App.cs` sets up the Fluent theme and theme-resource overrides,
`MainWindow` owns the menu, toolbar, tabs, status bar and all commands. It is a partial class split by seam:
`MainWindow.cs` (layout, menu, toolbar, file and edit commands, theme), `MainWindow.Session.cs` (hot exit and
disk-change checks), `MainWindow.Find.cs` (acting on the find bar), `MainWindow.Status.cs` (status bar, pickers,
menu checks, title). Put new code in the file of its seam.

- **Document** (`Document.cs`): one tab = one `TextEditor` + `LineNumberGutter` + tab header. Holds file path,
  EOL kind, encoding, language and dirty state (undo-stack based, plus `MarkDirty` for EOL/encoding changes).
  `ApplySettings(settings, zoom)` pushes every editor preference; MainWindow calls it for all documents.
- **Text I/O** (`TextFile.cs`): load/save with encoding + EOL detection. The editor always holds `\n`-normalised
  text; EOLs are re-applied on save.
- **Languages** (`Languages.cs`): catalog of languages (name, extensions, highlighting definition name) and detection (extension, then first line: shebang, `<?xml`, ...).
  Save As re-detects from the new extension; reload keeps the current language.
- **Syntax colours** (`SyntaxTheme.cs` + `syntax/*.xshd`): every definition — AvaloniaEdit's built-ins and
  the embedded custom ones (Batch, INI, Rust, Shell, YAML) — is re-registered lazily with colours chosen by matching
  the xshd colour *names* to roles in the current `SyntaxPalette`. New custom languages: add an `.xshd`
  (embedded via the csproj glob), list its name in `SyntaxTheme.Custom`, and add a `Language` entry.
- **Theming** (`Look.cs`): `Palettes.Dark`/`Light` (surfaces + syntax) and `Palettes.Accents` (gradient
  variants). The shared brushes on `Look` are mutated in place by `Look.Apply(palette, accent)`; controls hold
  references to them, so a theme switch needs no rebuild. Always use those shared brushes for themed colours —
  a new `SolidColorBrush` won't follow theme changes. After a palette change MainWindow calls
  `SyntaxTheme.Reset()` and `Document.RefreshTheme()`.
- **Settings** (`AppSettings.cs`, `SettingsWindow.cs`): JSON at `%APPDATA%\PadSharp\settings.json`, loaded in
  `App`, passed to MainWindow. Every change goes through `MainWindow.OnSettingsChanged` (apply theme → apply
  editor settings → save). Preferences apply live; there is no OK/Cancel. `--screenshot` runs load but never
  save settings — that file is the user's real one, so back it up if a test needs to write it.
- **Find/Replace** (`FindBar.cs` + `TextSearch.cs`): a bar docked above the status bar raising `FindRequested`;
  MainWindow acts on it. The search logic is pure and lives in `TextSearch`.
- **Session / hot exit** (`Session.cs`): `session.json` + `buffers/*.txt` next to settings.json. Saved every 2 s
  when something changed and on close; restored on start. Only the instance holding `session.lock` uses it
  (others prompt to save on close). `--screenshot` runs never claim, restore or save it.

## Behaviour

The current rules for user-visible behaviour. Update this section when you change them.

**Tabs and hot exit**
- New tabs are `Untitled-N`, N = smallest positive number not used by another open untitled tab.
- Closing the window never asks: all tabs, including unsaved text, come back on the next start.
- Closing a single tab with changes asks `Save changes to "NAME"?` (Save / Don't Save / Cancel).
- Closing the last tab leaves a fresh untitled tab. New documents are UTF-8 with CRLF line breaks.
- On window activation, files changed elsewhere are reloaded silently (caret kept); if the tab has unsaved
  changes, it asks Reload / Keep Mine.

**Status bar**, left to right: language name (or a transient message) · `Ln 12, Col 4` (+ `(N selected)`,
+ `OVR` in overtype) · `N lines` · `CRLF`/`LF`/`CR` · `UTF-8`/`UTF-8 BOM`/`UTF-16 LE`/`UTF-16 BE`/`ANSI`.
Clicking language, line endings or encoding opens a picker. Column counts a tab as reaching the next tab stop.
Numbers use the locale's thousands separators.

**Find bar** (Ctrl+F, Ctrl+H for replace): searches as you type, always wraps; Enter / Shift+Enter = next /
previous, F3 / Shift+F3 work with the bar closed, Esc closes. Toggles: match case (`Aa`), whole word (`W`).
Shows `3 of 12`, `No results`, or `Replaced N`.

**Line endings and encodings on open**
- The first line break found decides the EOL style; none found means CRLF.
- BOMs are honoured; otherwise valid UTF-8 is read as UTF-8, anything else as ANSI.

**Language**
- Detected automatically: extension first; otherwise the first line (`#!` interpreter, `<?xml`,
  `<!DOCTYPE html`/`<html`, `<?php`); otherwise Plain Text. Override via the flat `Language` menu or the
  status bar. The session stores only an override; other tabs are detected again on restore.
- Save As under a new name switches to the new extension's language (unknown extension keeps the current one);
  reload keeps the language. The Save As type list starts with the current language.
- C and TypeScript borrow the C++ and JavaScript definitions.

**Zoom**: Zoom In/Out (Ctrl+Mouse Wheel, Ctrl+Plus/Minus), Reset Zoom (Ctrl+0); one point per step, range -10..+60.

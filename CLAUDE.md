# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

PadSharp is a small, fast, cross-platform **scratchpad** editor in C#, built with Avalonia + AvaloniaEdit.
Design aim: open instantly, never lose text (hot exit), stay out of the way. Prefer cutting to adding.

C# 15 on .NET 11; must stay cross-platform (Windows, macOS, Linux) with no Windows-only libraries. The only
Windows-specific call is the optional one that puts the `--screenshot` window behind others, skipped elsewhere.

## Commands

- Build (this is also the lint): `dotnet build PadSharp.slnx` (app + tests)
- Test: `dotnet test` (xUnit v3 on Microsoft.Testing.Platform, see `global.json`)
- Run: `dotnet run --project PadSharp.csproj -- [files...]`
- Visual check without taking focus: `bin/Debug/net11.0/PadSharp.exe [files...] --screenshot out.png`
  (renders ~120 frames behind other windows, saves a PNG of the window, exits 0). Only the main window is
  captured, not menus or dialogs.
- Launcher: `dist/ed.cmd` (Windows) and `dist/ed` (POSIX, LF only, see `.gitattributes`) are copied next to the
  program on build/publish for the matching target. They find the program in their own folder, so users may
  rename them; keep it that way.
- Portable / throwaway data: set `PADSHARP_DATA=<folder>` to keep settings and the session there instead of
  `%APPDATA%\PadSharp`.
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

## Code health (enforced)

`tests/PadSharp.Tests/CodeHealth/CodeHealthTests.cs` fails `dotnet test` when anything in `src/` goes over these
limits: method ≤ 30 lines, cyclomatic ≤ 10, cognitive ≤ 15, nesting ≤ 2, parameters ≤ 4, type ≤ 500 lines per
file, copied 10-line blocks ≤ 3 %. Fix a breach by splitting along a seam, or by turning a group of parameters into
a record (`SearchQuery`, `SearchHit`, `SessionTab`). Only a breach that is genuinely better left alone may go into
`CodeHealthTests.Exceptions`, and it needs a written reason. An exception whose code is back within the limits fails
too, so the list only shrinks. Never raise a limit to make a change pass. The `code-health` skill reads the gate's
report (`PADSHARP_CODE_HEALTH_REPORT`).

## Tests (required)

**All code must have a test class testing it.** Every file in `src/` has a matching test class in
`tests/PadSharp.Tests/`, named after the file: `TextFile.cs` → `TextFileTests`, `MainWindow.Find.cs` →
`MainWindowFindTests`, `EditorStyles.axaml.cs` → `EditorStylesTests`. `ConventionTests` fails the run when one
is missing, and also when a `syntax/*.xshd` backs no language. A new or changed behaviour gets a test in the
same change; a bug fix gets a test that fails without the fix.

- Tests run the real `App` on Avalonia's headless platform (`TestSupport.cs`). Use `[AvaloniaFact]` /
  `[AvaloniaTheory]` for **every** test, also pure logic: a plain `[Fact]` touching any Avalonia type binds the
  UI dispatcher to the wrong thread and breaks later tests. Tests run one at a time (shared static state).
- Use `Sandbox` for anything touching files, settings or the session: it points `AppSettings.DataFolder` at a
  temp folder (the module initializer already keeps tests away from the real `%APPDATA%`), opens main windows
  with `sandbox.Open(...)`, and restores the look on dispose.
- Drive the UI like a person: `window.Press(Key.N, RawInputModifiers.Control)`, `window.Type("text")`, menu
  items by header, dialogs via `window.OwnedWindows`. `MainWindow` exposes `Current`, `Documents`, `FindBar`
  and `StatusParts` for assertions; prefer those over adding more test-only members.
- Theory data can't use the app's internal enums as parameter types; pass `(int)` and cast back.
- Not covered by automated tests (check by hand or with `--screenshot`): the file pickers, the status-bar
  picker pop-ups, drag and drop, and the `--screenshot` run itself.

## Architecture

The UI is built in code (no XAML views); `App.cs` sets up the Fluent theme and theme-resource overrides,
`MainWindow` owns the menu, toolbar, tabs, status bar and all commands. It is a partial class split by seam:
`MainWindow.cs` (construction, layout, window placement, lifetime, messages, screenshot), `.Menu.cs` (menus, toolbar,
shortcuts, Cmd on macOS), `.Files.cs` (tabs, open, save, close, drag and drop), `.Edit.cs` (text commands, line
endings, encoding, language, go to line), `.View.cs` (zoom, theme, accent, preferences, About), `.Find.cs` (acting
on the find bar), `.Status.cs` (status bar, pickers, menu checks, title) and `.Session.cs` (hot exit, disk-change
checks). Put new code in the file of its seam; a new file needs its own `<File>Tests` class.

- **Document** (`Document.cs`): one tab = one `TextEditor` + `LineNumberGutter` + tab header. Holds file path,
  EOL kind, encoding, language and dirty state (undo-stack based, plus `MarkDirty` for EOL/encoding changes).
  `ApplySettings(settings, zoom)` pushes every editor preference; MainWindow calls it for all documents.
- **Text I/O** (`TextFile.cs`): load/save with encoding + EOL detection. The editor always holds `\n`-normalised
  text; EOLs are re-applied on save.
- **Languages** (`Languages.cs`): catalog of languages (name, extensions, highlighting definition name) and detection (extension, then first line: shebang, `<?xml`, ...).
  Save As re-detects from the new extension; reload keeps the current language.
- **Syntax colours** (`SyntaxTheme.cs` + `syntax/*.xshd`): every definition — AvaloniaEdit's built-ins and
  the embedded custom ones (Batch, INI, Rust, Shell, TeX, YAML) — is re-registered lazily with colours chosen by matching
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

**One instance**: `Program.Main` claims the session lock before Avalonia starts. If another PadSharp owns it, the
arguments go to that window over a current-user named pipe (`SingleInstance`), and this process exits. The owning
window runs `SingleInstanceServer`, opens what it receives and comes to the front. If the hand-off fails, the
start continues as a window that doesn't own the session. `--screenshot` runs never forward or claim.

**Window**: size, position (moved back on screen if its monitor is gone), maximized state and zoom are kept in
settings on close. Screenshot runs use 1100x720 with no zoom. Shortcuts are written `Ctrl+…` and become `Cmd+…` on
macOS (`MainWindow.ForPlatform`); `Ctrl+Tab` stays. Zoom keys: `Ctrl+=`, `Ctrl+Shift+=`, `Ctrl+-` and the numpad.

**Tabs and hot exit**
- New tabs are `Untitled-N`, N = smallest positive number not used by another open untitled tab.
- Closing the window never asks: all tabs, including unsaved text, come back on the next start.
- Closing a single tab with changes asks `Save changes to "NAME"?` (Save / Don't Save / Cancel).
- Closing the last tab leaves a fresh untitled tab. New documents are UTF-8 with CRLF line breaks.
- On window activation, files changed elsewhere are reloaded silently (caret kept); if the tab has unsaved
  changes, it asks Reload / Keep Mine. The session stores each file's timestamp, so a change made while closed
  is noticed too.
- Session and settings files are written to `*.tmp` and renamed into place. A failed session save shows
  `⚠ Unsaved text not protected: …` in the status bar until a save works. Unhandled exceptions on the UI thread
  save the session first (`SaveSessionBeforeCrash`). Async commands go through `Run`, which shows errors.
- A file loaded with mixed line endings shows `Mixed → CRLF` (etc.) until a line ending is chosen or it is saved.

**Status bar**, left to right: language name (or a transient message) · `Ln 12, Col 4` (+ `(N selected)`,
+ `OVR` in overtype) · `N lines` · `CRLF`/`LF`/`CR` · `UTF-8`/`UTF-8 BOM`/`UTF-16 LE`/`UTF-16 BE`/`ANSI`.
Clicking language, line endings or encoding opens a picker. Column counts a tab as reaching the next tab stop.
Numbers use the locale's thousands separators.

**Find bar** (Ctrl+F, Ctrl+H for replace): searches as you type, always wraps; Enter / Shift+Enter = next /
previous, F3 / Shift+F3 work with the bar closed, Esc closes. Toggles: match case (`Aa`), whole word (`W`),
regular expression (`.*`: .NET syntax, `^`/`$` per line, empty matches skipped, `$1`/`${name}` in Replace,
1 s match timeout). Shows `3 of 12`, `No results`, `Replaced N`, or `Invalid regular expression`.

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

**Zoom**: Zoom In/Out (Ctrl+Mouse Wheel, Ctrl+Numpad +/−), Reset Zoom (Ctrl+0); one point per step, range -10..+60.

## Repository files

`README.md` is for end users (shortcuts, hot exit, data folder); `CONTRIBUTING.md` for contributors. User-visible
changes get a line under `[Unreleased]` in `CHANGELOG.md`, written for users. The version is `<Version>` in
`PadSharp.csproj` (shown in About). MIT license; `LICENSE`, `THIRD-PARTY-NOTICES.md` and `README.md` are copied to
the publish folder, so update the notices when a shipped package is added. CI (`.github/workflows/ci.yml`) builds
and tests on Windows, Linux and macOS and publishes Native AOT artifacts. Pushing a `v*` tag runs
`.github/workflows/release.yml`: version and changelog check, then packages (Windows zip; Linux tarball with
`dist/linux/install.sh`, `.desktop` and icon; macOS `PadSharp.app` from `dist/macos/Info.plist`), optional
signing via secrets, and a GitHub release. Everything under `dist/` except `.cmd` is LF (`.gitattributes`); Dependabot groups the Avalonia packages
(the headless test package pins the xUnit version).

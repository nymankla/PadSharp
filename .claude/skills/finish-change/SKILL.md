---
name: finish-change
description: Check that a change to PadSharp is really finished before it is committed - build and lint, AOT safety, a visual check with --screenshot, and the CLAUDE.md sections this particular diff requires. Use when asked to finish, wrap up or check a change, before "commit" or "commit & pr", and after implementing a feature or fix.
---

Works out from the diff what this change has to update, updates it, and runs the checks. It doesn't commit or push
unless the user asked for that. `CLAUDE.md` holds the rules; this is how to apply them to one diff.

## 1. See what changed

```powershell
git status --short
git diff --stat
```

Never stage anything from `bin/`, `obj/` or a settings/session file copied out of `%APPDATA%\PadSharp`. Stage
files by name; never `git add -A` or `git add .`.

## 2. Go through what the diff requires

| If the diff... | Then also |
|---|---|
| changes anything in `src/` | Tests in the file's `<File>Tests` class (a new file gets a new test class; `ConventionTests` checks). A bug fix gets a test that fails without it |
| changes what the user sees (menu, status bar, dialog, find bar, prompt text, shortcut) | The **Behaviour** section of `CLAUDE.md`, `README.md` when a shortcut or feature changes, and a line under `[Unreleased]` in `CHANGELOG.md` written for users |
| adds or upgrades a package that ships with the app | `THIRD-PARTY-NOTICES.md` |
| releases a version | `<Version>` in `PadSharp.csproj` and a dated heading in `CHANGELOG.md` replacing `[Unreleased]` |
| changes how a part works (a file in the Architecture list, a new file, settings or session format) | The **Architecture** section of `CLAUDE.md` |
| adds a setting | `AppSettings` property with a default, clamping in `Sanitize`, a control in `SettingsWindow`, applied in `Document.ApplySettings` or `MainWindow.ApplyTheme` |
| changes `SessionState`/`SessionTab` | Old `session.json` files must still load (new properties get defaults); never lose a buffer |
| adds JSON types | Register them in `SettingsJson` (source-generated; reflection JSON fails the AOT analyzers) |
| adds a language | `.xshd` in `syntax/` if AvaloniaEdit has none, its name in `SyntaxTheme.Custom`, a `Language` entry |
| adds a colour | Use or add a shared brush on `Look`; a new `SolidColorBrush` won't follow theme changes |
| adds a feature | Ask whether it earns its place: the aim is a small scratchpad. Say what it costs in lines |

## 3. Run the checks

Close any running PadSharp first: it locks `bin/Debug` and the build fails with MSB3061.

```powershell
dotnet build PadSharp.slnx
dotnet test
```

Report the test totals. The build is the lint: warnings are errors, analyzers at `Recommended`, code style enforced, and the trim/AOT
analyzers run on every build. Fix the code; don't suppress a rule or edit `.editorconfig` to get around it.

For a change to file I/O, encodings or AOT-sensitive code, also publish once:

```powershell
dotnet publish PadSharp.csproj -c Release -r win-x64
```

## 4. See it work

Render the window with the files the change affects. Screenshots go to the scratchpad, never into the repo.

```powershell
bin/Debug/net11.0/PadSharp.exe <files...> --screenshot <scratchpad>\after.png
```

Then read the PNG. Only the main window is captured, not menus or dialogs, and `--screenshot` never loads or saves
the session. Say plainly what could not be seen this way (dialogs, menus, the find bar, hot exit) and was not
verified.

Optionally, run `code-health` and compare with its last baseline when the change touched `MainWindow` or added
a lot of code.

## 5. Report

Say what passed, what was updated, what was left out and what could not be verified. Don't report a check as passed
if it was skipped.

# Changelog

All notable changes to PadSharp are listed here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0] - 2026-11-23

### Added

- Tabs with **hot exit**: closing PadSharp never asks to save. Tabs, unsaved text and cursor positions come back on
  the next start, and work is kept every couple of seconds while you type.
- Files changed by another program reload automatically. If the tab has unsaved changes, you choose
  **Reload** or **Keep Mine**.
- A find bar inside the window (`Ctrl+F`, `Ctrl+H` for replace) that searches as you type, wraps around and shows
  `3 of 12`. Options: match case, whole word and regular expressions (with `$1` groups in Replace). A Replace All
  undoes in one step.
- A status bar showing language, position, selection, line count, line endings and encoding. Click the language,
  line endings or encoding to change them.
- Syntax colouring for 24 languages, detected from the file name or the first line. Batch, INI, Rust, Shell, TeX
  and YAML use PadSharp's own definitions.
- Dark and light themes that can follow the system, and seven accent colours.
- A Preferences window that applies changes immediately: font, size, tab width, spaces instead of tabs, word wrap,
  line numbers, current-line highlight and whitespace.
- Line commands: duplicate line (`Ctrl+D`), delete line (`Ctrl+Shift+L`), upper case (`Ctrl+Shift+U`) and
  lower case (`Ctrl+U`). Also Go to Line (`Ctrl+G`), zoom, and Always on Top.
- The `ed` launcher (`ed.cmd` on Windows) next to the program. It starts PadSharp from a terminal and works under
  any name.
- `PADSHARP_DATA` keeps settings and the session in a folder of your choice, for portable use.
- Native AOT builds for Windows, Linux and macOS that need no .NET runtime installed: a zip for Windows, a tarball
  with an installer (app menu entry, icon, `padsharp` command) for Linux, and `PadSharp.app` for macOS.
- PadSharp runs once: starting it again, for example with `ed notes.txt`, opens the files in the running window.
- Window size, position, maximized state and zoom are remembered between runs.
- Zoom with `Ctrl+=` / `Ctrl+-` on the main keyboard, not only the numpad.
- On macOS, shortcuts use `Cmd` instead of `Ctrl`.
- Files with mixed line endings show `Mixed → CRLF` in the status bar before saving converts them.
- Unsaved text is also kept if PadSharp crashes, and the status bar warns when it can't be kept. Settings and
  kept tabs are written in full before they replace the old copy.
- A file changed while PadSharp was closed is noticed when its unsaved tab comes back.

[Unreleased]: https://github.com/nymankla/PadSharp/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/nymankla/PadSharp/releases/tag/v0.1.0

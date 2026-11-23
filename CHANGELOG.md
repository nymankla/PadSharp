# Changelog

All notable changes to PadSharp are listed here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
- Native AOT builds for Windows, Linux and macOS that need no .NET runtime installed.

### Fixed

- TeX files are now coloured. The definition that came with the editor component never loaded.
- Starting PadSharp with a file that doesn't exist shows a message instead of crashing.

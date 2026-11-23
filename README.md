# PadSharp

A small, fast scratchpad editor for Windows, macOS and Linux.

Open it, type, close it. Nothing gets lost: your tabs and any text you haven't saved come back the next time you
start PadSharp, even if you never saved them as a file.

## Getting started

PadSharp is a self-contained folder: no installer, and no .NET runtime to install.

- **Start it:** run `PadSharp` (or `PadSharp.exe` on Windows).
- **Open files from the command line:** `PadSharp notes.txt todo.md`
- **Open files any time:** drag them onto the window, or use **File → Open** (`Ctrl+O`).

### The `ed` command

Next to the program is a small launcher script, `ed` (`ed.cmd` on Windows). Put the PadSharp folder on your
`PATH`, and from any terminal:

```
ed                     # open PadSharp
ed notes.txt todo.md   # open these files (relative names are fine)
```

It starts PadSharp and hands the prompt straight back to you; it doesn't wait for the editor to close.

**You can rename it to anything you like**: `pad`, `e`, `note`. The script finds PadSharp in its own folder,
not by its name, so any name works. Keep it in the same folder as PadSharp. On Linux and macOS you can instead
link to it under any name from a folder that is already on your `PATH`:

```
ln -s /opt/PadSharp/ed ~/bin/pad
```

On Linux and macOS, `ed` is also the name of the classic Unix line editor, which may already be installed. If
typing `ed` starts that one instead, rename the script (for example to `pad`). If the script won't run after you
copied it from Windows, make it executable with `chmod +x ed`.

### Building it yourself

You need the .NET 11 SDK:

```
dotnet publish PadSharp.csproj -c Release -r win-x64      # or linux-x64, osx-arm64
```

The program, with the matching `ed` launcher, ends up in `bin/Release/net11.0/<platform>/publish/`.

## Never lose a scratch note

- **Closing PadSharp never asks to save.** Every tab, including untitled ones and unsaved changes, is kept and
  reopens next time, with the cursor where you left it.
- Your work is also kept every couple of seconds while you type, so a crash or power cut loses almost nothing.
- **Closing a single tab** that has changes *does* ask: **Save**, **Don't Save** or **Cancel**.
- **Files changed by another program** are reloaded automatically when you switch back to PadSharp. If you have
  unsaved changes in that tab, PadSharp asks whether to **Reload** or **Keep Mine**.
- A dot on each tab shows its state: green is saved, amber has unsaved changes.

If you open a second PadSharp window, only the first one keeps your tabs between runs. The second one asks to save
when you close it, like a classic editor.

## Keyboard shortcuts

The shortcuts are the same on every system, macOS included: they use `Ctrl`, not `Cmd`.

### Files and tabs

| Keys | Action |
|---|---|
| `Ctrl+N` | New tab |
| `Ctrl+O` | Open files |
| `Ctrl+S` | Save |
| `Ctrl+Alt+S` | Save as |
| `Ctrl+Shift+S` | Save all |
| `Ctrl+W` | Close tab |
| `Ctrl+Shift+W` | Close all tabs |
| `Ctrl+Tab` / `Ctrl+PageDown` | Next tab |
| `Ctrl+Shift+Tab` / `Ctrl+PageUp` | Previous tab |
| Middle-click a tab | Close it |
| Close the window (`Alt+F4` on Windows) | Quit; your tabs are kept |

### Editing

| Keys | Action |
|---|---|
| `Ctrl+Z` / `Ctrl+Y` | Undo / redo |
| `Ctrl+X` / `Ctrl+C` / `Ctrl+V` | Cut / copy / paste |
| `Ctrl+A` | Select all |
| `Ctrl+D` | Duplicate the current line |
| `Ctrl+Shift+L` | Delete the current line |
| `Ctrl+Shift+U` | Selection to UPPERCASE |
| `Ctrl+U` | Selection to lowercase |
| `Insert` | Switch between insert and overtype (the status bar shows `OVR`) |
| `Ctrl+G` | Go to line |

### Find and replace

| Keys | Action |
|---|---|
| `Ctrl+F` | Open the find bar (it starts with the selected text) |
| `Ctrl+H` | Open the find bar with replace |
| `Enter` / `Shift+Enter` | Next / previous match (while in the find box) |
| `F3` / `Shift+F3` | Next / previous match, also with the find bar closed |
| `Enter` (in the replace box) | Replace this match and go to the next one |
| `Ctrl+Alt+Enter` (in the replace box) | Replace all |
| `Esc` | Close the find bar |

The find bar searches as you type and wraps around the end of the document. It shows where you are, such as
`3 of 12`. **Aa** matches upper and lower case exactly, and **W** finds whole words only. A Replace All can be
undone in one step with `Ctrl+Z`.

### View

| Keys | Action |
|---|---|
| `Ctrl+Mouse wheel` | Zoom in / out |
| `Ctrl+Numpad +` / `Ctrl+Numpad −` | Zoom in / out |
| `Ctrl+0` | Reset zoom |
| `Ctrl+Shift+D` | Switch between dark and light |
| `Ctrl+,` | Preferences |
| `F1` | About PadSharp |

## The status bar

The bottom bar shows, from left to right:

| Example | Meaning | Click it to… |
|---|---|---|
| `Rust` | The language used for colouring | pick another language |
| `Ln 12, Col 4 (5 selected)` | Cursor position, and how many characters are selected | |
| `120 lines` | Number of lines | |
| `CRLF` | Line endings: `CRLF` (Windows), `LF` (Unix/macOS) or `CR` (classic Mac) | change them for this file |
| `UTF-8` | Text encoding used when saving | change it for this file |

The same choices are also in the **Edit → Line Endings**, **Encoding** and **Language** menus. Changing line endings
or encoding counts as a change, so save the file to keep it.

## Languages

PadSharp colours these automatically from the file name, or from the first line (for example `#!/bin/bash` or
`<?xml`): ASP, Batch, C, C#, C++, CSS, Diff, HTML, INI, Java, JavaScript, JSON, Markdown, PHP, PowerShell, Python,
Rust, Shell, SQL, TeX, TypeScript, Visual Basic, XML and YAML. Anything else is plain text.

If PadSharp guesses wrong, click the language in the status bar (or use the **Language** menu) to pick another.
PadSharp remembers that choice for the tab.

## Preferences

Open **Settings → Preferences** (`Ctrl+,`) or the sliders button on the toolbar. Changes apply immediately; there is
no OK button.

- **Theme:** follow the system, dark or light. The sun/moon button on the toolbar switches quickly.
- **Accent colour:** Neon, Ocean, Aurora, Sunset, Ember, Forest or Mono. It colours the status bar, the tab
  underline, the cursor and the selection.
- **Font and font size**, **tab width**, and whether `Tab` inserts spaces.
- **Word wrap**, **line numbers**, **highlight the current line** and **show whitespace**.

**View → Always on Top** keeps the window above other windows.

## Where PadSharp keeps its data

| System | Folder |
|---|---|
| Windows | `%APPDATA%\PadSharp` |
| macOS and Linux | `~/.config/PadSharp` (or `$XDG_CONFIG_HOME/PadSharp` when that is set) |

The folder holds `settings.json` (your preferences, safe to edit by hand), `session.json` (your open tabs) and
`buffers/` (text you haven't saved yet).

**Portable use:** set the environment variable `PADSHARP_DATA` to a folder, for example on a USB stick, and PadSharp
keeps everything there instead.

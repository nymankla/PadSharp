#!/bin/sh
# Installs PadSharp for the current user; no root needed. Run it from the unpacked PadSharp folder.
#   ~/.local/share/padsharp/   the program (this folder is copied there)
#   ~/.local/bin/padsharp      the terminal command; it works under any name, so rename it if you like
#   menu entry and icon        PadSharp appears in your desktop's app menu and in "Open with"
# ./install.sh --uninstall removes all of that again (your settings and open tabs stay).
set -eu
here=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
data=${XDG_DATA_HOME:-$HOME/.local/share}
app=$data/padsharp
bin=$HOME/.local/bin
desktop=$data/applications/padsharp.desktop
icon=$data/icons/hicolor/scalable/apps/padsharp.svg

if [ "${1:-}" = "--uninstall" ]; then
    rm -rf "$app" "$desktop" "$icon"
    if [ -L "$bin/padsharp" ]; then
        rm -f "$bin/padsharp"
    fi
    echo "PadSharp removed. Settings and open tabs are kept in ${XDG_CONFIG_HOME:-$HOME/.config}/PadSharp."
    exit 0
fi

mkdir -p "$app" "$bin" "$(dirname "$desktop")" "$(dirname "$icon")"
if [ "$here" != "$app" ]; then
    cp -R "$here"/. "$app"/
fi

chmod +x "$app/PadSharp" "$app/ed"
ln -sf "$app/ed" "$bin/padsharp"
cp "$app/padsharp.svg" "$icon"
sed "s|@EXEC@|$app/PadSharp|" "$app/padsharp.desktop" > "$desktop"
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$data/applications" || true
fi

echo "PadSharp installed. Start it from your app menu, or run: padsharp [files]"
case ":$PATH:" in
    *":$bin:"*) ;;
    *) echo "Note: add $bin to your PATH to use the padsharp command." ;;
esac

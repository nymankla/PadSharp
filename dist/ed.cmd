@echo off
rem ed: start PadSharp with the given files and return to the prompt.
rem Usage: ed [files...]
rem Rename this file to anything you like (pad.cmd, e.cmd, ...): it finds PadSharp.exe in its own folder,
rem not by its name. Keep it next to PadSharp.exe, and put that folder on your PATH.
start "" "%~dp0PadSharp.exe" %*

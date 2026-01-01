# FFXIV Login Commands

Run chat commands automatically when a character logs in. Supports per-character profiles,
global commands, delays, ordering, and execution logs.

## Features

- Per-character profiles with independent command lists.
- Global commands applied to all characters.
- Per-command delay and ordering controls.
- Run modes: every login or once per session.
- Execution queue, manual run/skip controls, and history logs.
- Import/export settings as JSON.

## Command

- `/ffxivlogincommands` opens the main window.

## Build

1. Open `FFXIVLoginCommands.sln` in Visual Studio or Rider.
2. Build `Release` (or `Debug` for dev).
3. Output DLL is at `FFXIVLoginCommands/bin/x64/Release/FFXIVLoginCommands.dll`.

## License

See `LICENSE.md`.

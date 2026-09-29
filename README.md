# Scape Switch

A compact Windows tray controller for the Fractal Scape headset dock. This version retains the original HID report handling, debounce, reconnect loop, audio endpoint switching and optional media pause. It adds a status panel and a proper device picker.

## What changed

- Left-click the tray icon for a square, compact panel showing **Docked**, **In use**, or **Searching**, plus the current Windows audio output and the last switch result. The tray glyphs are distinct: an amber loudspeaker for docked, lime headphones for in use, and grey for searching. The panel's state text, edge marker, and active button use the same mode color.
- Use **Speakers** or **Headset** in the panel to switch the output manually. Manual switching does not change the reported physical dock state or pause media.
- Settings now lists active Windows playback devices for both dock states. It stores the endpoint ID and keeps the former name match as a fallback when Windows changes an ID. An offline saved device remains visible in the list.
- **Sync on launch** is an opt-in setting. With it off (the default), startup behaves like the original: the first HID heartbeat establishes state without changing audio.
- Tray updates are marshalled to the UI thread. Generated icon handles are released after replacement. A second copy of this version exits instead of running another HID loop.
- Configuration is still `dockconfig.json` beside the EXE and keeps the original field names, so an existing file can be copied over.

## Run

1. Quit the existing FractalDockSwitch tray app to avoid two programs responding to the same dock.
2. Extract the Windows build ZIP to a writable folder. Run `FractalDockSwitch.exe`.
3. Left-click the tray icon and open **Settings**. Confirm the speaker and Fractal Scape playback devices; save. Right-click for the log console or Exit.

This is a framework-dependent .NET 6 Windows build, matching the original project. Your Windows PC already has the .NET 6 desktop runtime. It does not install a service, driver, or startup entry.

## Project layout

- `Program.cs`: original HID and audio path with UI-thread dispatch, manual switching, and configuration changes.
- `Ui.cs`: status panel and settings window.
- `FractalDockSwitch.csproj`: .NET 6 WinForms project using the original HidSharp and Dubya.WindowsMediaController package versions.

Build with a .NET SDK on Windows: `dotnet restore` then `dotnet build -c Release`. The provided executable uses the binaries from the uploaded working project for unchanged third-party assemblies; the revised application assembly was compiled against the matching .NET 6 reference packs.

## Put the source on GitHub

Extract this source ZIP, open a terminal in the extracted folder, and run `git init -b main`, `git add .`, and `git commit -m "Initial Scape Switch source"`. Create an empty repository on GitHub, then run `git remote add origin https://github.com/YOUR-NAME/ScapeSwitch.git` and `git push -u origin main`. Replace `YOUR-NAME` with your account name. If you already have a repository for this project, clone it and copy these source files into that clone instead.

The `.gitignore` keeps compiled files and your local `dockconfig.json` out of commits. The Windows build ZIP is suited to a GitHub Release download.

## Design

The panel and tray tile use the same deep-black `#101216` base as Lumen, with `#1B1E23` reserved for controls. Divider lines and muted text share Lumen's palette. The two functional mode accents remain amber for docked speakers and pale lime for headset in use. The physical headset state and current Windows output are shown separately because manual switching can make them differ. The panel is deliberately small enough to work as a tray popup; configuration stays in a separate window.

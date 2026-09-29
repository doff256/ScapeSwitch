# Scape Switch

Scape Switch is a Windows tray app for the Fractal Scape headset. It detects when the headset is docked or removed and switches the default audio output between your speakers and headset.

## Features

- Automatically switches playback devices when the dock state changes.
- Shows the dock state and current output in a compact tray panel.
- Lets you switch outputs manually from the panel.
- Lets you choose the playback device for each mode.
- Can pause media on a dock change and sync the output when the app starts.
- Reconnects when the headset receiver is unplugged and plugged back in.

## Requirements

- Windows 11
- Fractal Scape headset and receiver
- .NET 6 Desktop Runtime to run the app, or a .NET SDK to build it

## Build and run

```powershell
dotnet restore
dotnet build -c Release
```

Run `FractalDockSwitch.exe` from the build output. Left-click its tray icon to see the status panel and open **Settings** to select your speaker and headset devices. Right-click the icon for the log or to exit.

Settings are saved as `dockconfig.json` next to the executable. The file is specific to your PC and is excluded from Git.

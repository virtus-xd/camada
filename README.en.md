# Camada

[Türkçe](README.md) · **English**

[![CI](https://github.com/virtus-xd/camada/actions/workflows/ci.yml/badge.svg)](https://github.com/virtus-xd/camada/actions/workflows/ci.yml) · License: [Apache 2.0](LICENSE)

Camada replaces the Windows taskbar with a sleek glass island at the top of the screen. The island is small by default and shows the time, the music that is playing and your notifications. Hover over it and it expands: pinned apps, open windows, the system tray, Wi‑Fi, Bluetooth, volume, brightness, a calendar, clipboard history and screenshots are one click away.

The optional voice assistant Ada wakes up when you say "Hey Ada": it opens apps, adjusts volume and brightness, searches the web, takes notes, sets reminders and tells you the weather. Ada runs on your own Google Gemini API key and is off by default.

<!-- Screenshot: to be added with a wallpaper we have the rights to -->

## Features

- A glass island at the top of the screen that replaces the taskbar
- Now playing, the clock and notifications at a glance
- Pinned apps, open windows and the system tray
- Quick settings for Wi‑Fi, Bluetooth, volume and brightness
- Clipboard history (Win+Alt+V) and screenshots
- Optional voice assistant with "Hey Ada"
- The taskbar comes back with one click

## Status

Early release. The user interface is Turkish only for now; an English UI is planned. Camada is coming to the Microsoft Store.

## Taskbar safety net

Camada can hide the Windows taskbar; this is toggled with **"Görev çubuğunu gizle"** (Hide taskbar) in the island's right-click menu. The taskbar can always be restored:

- `CamAda.exe --restore-taskbar` closes the island and brings the Windows taskbar back.
- You can put a shortcut for this on your desktop: `.\tools\create-restore-shortcut.ps1 -ExePath <path to CamAda.exe>` ("Görev çubuğunu geri getir").
- The Microsoft Store version also adds a **"Görev çubuğunu geri getir"** (Restore taskbar) entry to the Start menu.
- If Camada closes unexpectedly, a separate watchdog restores the taskbar automatically.
- Unexpected errors are written to `%LOCALAPPDATA%\CamAda\crash.log`; you can attach this file when reporting a bug.

## Requirements

- Windows 11 22H2 or later, x64
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) to build from source
- Your own Google Gemini API key for Ada (optional)
- The Client ID of your own Spotify developer app for Spotify (optional)

## Building from source

```powershell
git clone https://github.com/virtus-xd/camada.git
cd camada
dotnet build CamAda.slnx
dotnet test tests/CamAda.Core.Tests
```

Create the restore shortcut **before** running the app, then start it:

```powershell
.\tools\create-restore-shortcut.ps1 -ExePath .\src\CamAda\bin\Debug\net10.0-windows10.0.22621.0\CamAda.exe
dotnet run --project src/CamAda
```

For a permanent install, run `.\tools\install.ps1`: it builds the Release version, installs it to `%LOCALAPPDATA%\Programs\CamAda`, creates the restore shortcut and starts Camada. Camada then starts automatically when you sign in. If PowerShell blocks scripts: `powershell -ExecutionPolicy Bypass -File .\tools\install.ps1` (applies to this command only).

## Project structure

| Folder | Contents |
| --- | --- |
| `src/CamAda.Core` | Platform-independent, tested logic (`net10.0`) |
| `src/CamAda` | WPF UI and Windows integration: island, panels, tray, audio, Ada |
| `tests/CamAda.Core.Tests` | xUnit tests for `CamAda.Core` |
| `tools/` | Install, restore shortcut and helper scripts |
| `docs/superpowers/specs/` | Design decisions and their reasoning (Turkish) |

## Privacy

Camada needs no account, shows no ads and collects no usage statistics. Only features you turn on go online: Ada (Google Gemini, with your own key), weather (Open-Meteo) and Spotify. The "Hey Ada" wake word is detected on your PC only. The Gemini key, Ada's memory and the Spotify token are stored on your PC, encrypted with Windows DPAPI. Details: [privacy policy](https://camada-azure.vercel.app/gizlilik).

## Contributing, security and support

- To contribute: [CONTRIBUTING.md](CONTRIBUTING.md)
- To report a vulnerability: [SECURITY.md](SECURITY.md)
- Code of conduct: [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)
- To support the project: [GitHub Sponsors](https://github.com/sponsors/virtus-xd)

## License

Camada is licensed under the [Apache License 2.0](LICENSE); third-party components are listed in [NOTICE](NOTICE). The "Camada" name and logo are not covered by the license.

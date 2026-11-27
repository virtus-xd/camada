<p align="center">
  <img src="docs/media/camada-banner.jpg" alt="Camada logo: a glowing glass letter C">
</p>

<h1 align="center">Camada</h1>

<p align="center"><b>A glass island that replaces the Windows 11 taskbar, with an optional voice assistant.</b></p>

<p align="center">
  <a href="https://apps.microsoft.com/detail/9PH3GB5S05MM?referrer=appbadge&mode=direct"><img src="https://get.microsoft.com/images/en-us%20dark.svg" alt="Get it from Microsoft" width="200"></a>
</p>

<p align="center">
  <a href="https://github.com/virtus-xd/camada/releases/latest"><img alt="Latest release" src="https://img.shields.io/github/v/release/virtus-xd/camada?label=release"></a>
  <a href="https://github.com/virtus-xd/camada/actions/workflows/ci.yml"><img alt="CI" src="https://github.com/virtus-xd/camada/actions/workflows/ci.yml/badge.svg"></a>
  <a href="LICENSE"><img alt="License: Apache 2.0" src="https://img.shields.io/badge/license-Apache%202.0-blue"></a>
  <img alt="Windows 11" src="https://img.shields.io/badge/Windows-11-0078D4">
</p>

<p align="center"><b>English</b> · <a href="README.tr.md">Türkçe</a></p>

Camada replaces the Windows taskbar with a sleek glass island at the top of the screen. The island is small by default and shows the time and the music that is playing. Hover over it and it expands: pinned apps, open windows, the system tray, Wi‑Fi, Bluetooth, volume, brightness, a calendar, clipboard history and screenshots are one click away.

The optional voice assistant Ada wakes up when you say "Hey Ada": it opens apps, adjusts volume and brightness, searches the web, takes notes, sets reminders and tells you the weather. Ada runs on your own Google Gemini API key and is off by default.

https://github.com/user-attachments/assets/d1baf50b-d273-454d-ac15-0a2342cc7311

## Features

- A glass island at the top of the screen that replaces the taskbar
- Now playing and the clock at a glance
- Pinned apps, open windows and the system tray
- Quick settings for Wi‑Fi, Bluetooth, volume and brightness
- Clipboard history (Win+Alt+V) and screenshots
- Optional voice assistant with "Hey Ada"
- The taskbar comes back with one click

## Install

- **Microsoft Store** (free): [Camada on the Microsoft Store](https://apps.microsoft.com/detail/9PH3GB5S05MM)
- **winget:** `winget install --id 9PH3GB5S05MM --source msstore`
- **From source:** see [Building from source](#building-from-source)

## Status

Early release. The interface is available in English and Turkish: it follows your Windows display language, and you can switch it with **Dil / Language** in the island's right-click menu. Ada speaks the interface language and switches to another language when you ask. Camada is available on the Microsoft Store.

## Taskbar safety net

Hiding the Windows taskbar is **off by default**. Turn it on or off with **Hide taskbar** in the island's right-click menu. On its first run Camada also registers itself to start when you sign in; turn that off with **Start with Windows** in the same menu. **Exit** closes the app.

When the taskbar is hidden, it can always be restored:

- `CamAda.exe --restore-taskbar` closes the island and brings the Windows taskbar back. It does not change the setting: if hiding is on, Camada hides the taskbar again the next time it starts. To turn hiding off for good, uncheck **Hide taskbar** in the menu.
- You can put a shortcut for this on your desktop: `powershell -ExecutionPolicy Bypass -File .\tools\create-restore-shortcut.ps1 -ExePath <path to CamAda.exe>` ("Görev çubuğunu geri getir").
- The Microsoft Store version adds a **Restore taskbar** entry to the Start menu, and uninstalling it brings the taskbar back.
- If Camada closes unexpectedly, a separate watchdog restores the taskbar automatically.
- Unexpected errors are written to `%LOCALAPPDATA%\CamAda\crash.log` (Store version: `%LOCALAPPDATA%\Packages\VirtusTech.Camada_6ccshz4mw4fd6\LocalCache\Local\CamAda\crash.log`). You can attach this file when reporting a bug; check it for personal data first.

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

Create the restore shortcut **before** running the app (you will want it once taskbar hiding is on), then start it. On the first run Camada registers itself to start at sign-in (see [Taskbar safety net](#taskbar-safety-net)).

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\create-restore-shortcut.ps1 -ExePath .\src\CamAda\bin\Debug\net10.0-windows10.0.22621.0\CamAda.exe
dotnet run --project src/CamAda
```

For a permanent install, run `.\tools\install.ps1`: it builds the Release version, installs it to `%LOCALAPPDATA%\Programs\CamAda`, creates the restore shortcut and starts Camada. If PowerShell blocks scripts, use the same form as above: `powershell -ExecutionPolicy Bypass -File .\tools\install.ps1` (applies to this command only).

## Uninstalling

1. Right-click the island, uncheck **Start with Windows** and **Hide taskbar**, then choose **Exit**.
2. If you installed with `install.ps1`, run `powershell -ExecutionPolicy Bypass -File .\tools\uninstall-dev.ps1` (add `-WhatIf` to preview). It removes the sign-in entry, the desktop shortcut and `%LOCALAPPDATA%\Programs\CamAda`, and keeps your settings and Ada's data.
3. If you ran Camada straight from a build, its sign-in entry may still point to the build folder. Delete it (the `CamAda` value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`):
   `Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name CamAda -ErrorAction SilentlyContinue`
4. To also remove settings and Ada's data: `%APPDATA%\CamAda` and `%LOCALAPPDATA%\CamAda`. Ada's notes (Documents\Ada Notları) and screenshots (Pictures) stay where they are.

The Microsoft Store version is removed from Windows Settings → Apps; the taskbar comes back automatically.

## Project structure

| Folder | Contents |
| --- | --- |
| `src/CamAda.Core` | Platform-independent, tested logic (`net10.0`) |
| `src/CamAda` | WPF UI and Windows integration: island, panels, tray, audio, Ada |
| `tests/CamAda.Core.Tests` | xUnit tests for `CamAda.Core` |
| `tools/` | Install, uninstall, restore shortcut, packaging and helper scripts |
| `packaging/` | MSIX manifest, assets, Store identity and listing texts (`tools/package.ps1` builds the package) |
| `site/` | The privacy policy page |
| [docs/superpowers/specs/](docs/superpowers/specs/) | Design decisions and their reasoning (Turkish) |

## Privacy

Camada needs no account, shows no ads and collects no usage statistics. Only features you turn on go online: Ada (Google Gemini, with your own key), weather (Open-Meteo) and Spotify. The "Hey Ada" wake word is detected on your PC only. The Gemini key, Ada's memory and the Spotify token are stored on your PC, encrypted with Windows DPAPI. Details: [privacy policy](https://camada-azure.vercel.app/gizlilik).

## Contributing, security and support

- To contribute: [CONTRIBUTING.md](CONTRIBUTING.md)
- To report a vulnerability: [SECURITY.md](SECURITY.md)
- Code of conduct: [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)
- To support the project: [GitHub Sponsors](https://github.com/sponsors/virtus-xd) or [Buy Me a Coffee](https://buymeacoffee.com/virtusxd)

## License

Camada is licensed under the [Apache License 2.0](LICENSE); third-party components are listed in [NOTICE](NOTICE). The "Camada" name and logo are not covered by the license.

# Leo · Desktop companion and local action hub

[简体中文](README.md) | **English**

Bring apps, tasks, notes, football, music and local actions into a customizable Windows companion. Enable only what you need.

**Latest release: v1.05.0 · Windows x64.** Includes Chinese–English UI, the current module foundation and later fixes, with a SHA-256 checksum.

[Download](https://github.com/LeoLi006/leo-desktop/releases/tag/v1.05.0) · [Issues](https://github.com/LeoLi006/leo-desktop/issues)

## Documentation

Each guide has a separate English edition. Use the language link at the top of any page to switch; translations are no longer interleaved.

| Guide | Contents |
| --- | --- |
| [User guide](src/USER_GUIDE.en.md) | Setup, everyday use, settings and troubleshooting |
| [Product and developer guide](src/PRODUCT_AND_DEVELOPER_GUIDE.en.md) | Features, architecture, interfaces and extension requirements |
| [Modules and packages](src/MODULES.en.md) | Local apps, JSON, ZIP, stdio and recovery |
| [Skin format](src/SKIN_FORMAT.en.md) | Jerseys, badges, themes and sharing |
| [Architecture](src/Architecture/README.en.md) | Lifecycle, data boundaries, samples and integration |
| [Roadmap](src/ROADMAP.en.md) | Delivered scope and remaining work |
| [Change log](src/RELEASE_NOTES.en.md) | Releases and source improvements |
| [Verification and performance](src/PERFORMANCE.en.md) | Historical evidence, methods and limitations |
| [Localization](src/Localization/README.en.md) | Resources and new UI text |
| [Animation maintenance](src/ANIMATION-NOTES.en.md) | Docking, textures, rendering and checks |
| [Jersey assets](src/Assets/MatchDay/Kits/README.en.md) | Asset provenance and usage limits |
| [Related work](src/RESEARCH.en.md) | Reuse research and selection principles |

## Features

- **Companion and themes:** editable name/size, multi-monitor docking, hover reveal, fullscreen hiding, five team skins and custom clothing/UI themes.
- **Launcher:** pinyin/aliases, pins, recent apps, path completion, drop actions, custom programs and icons.
- **Planning and records:** daily/weekly/calendar views, recurrence/exceptions, reminders, notes, protected clipboard history and quiet logs.
- **Optional capabilities:** football, QQ Music single-song requests, media/PotPlayer, Clash, terminal, Edge, read-only computer status and existing mail-client launch.
- **Controlled brain:** local commands first, streaming chat, local speech recognition and bounded tool plans with preview/confirmation.
- **Module foundation:** unified actions, dependencies, results, entries and lifecycle; data-only app packages plus declarative/worker extensions.

Local features need no model API. Free chat/complex understanding use your configured provider. Football, songs and updates have separate network sources. Mail does not retrieve accounts/messages; Huorong integration only opens the client.

## Getting started

1. Download `Leo-v1.05.0-win-x64.zip` from the release page, extract everything and run `Install-Leo.cmd`.
2. Right-click the companion to open settings and configure optional modules/local paths.
3. Switch between Chinese and English in Settings→General→Interface language.
4. API chat and the local SenseVoice model are optional, separately configured dependencies.

Click to toggle search, drag to move, and press Esc to dismiss the current window. Buttons, menus and shortcuts follow enabled modules.

## Development and contribution

Windows x64, the SDK specified by [global.json](global.json), Git and PowerShell are required.

```powershell
./scripts/Build.ps1 -Locked
./.build/Release/LeoLauncher.exe
```

Read the [developer guide](src/PRODUCT_AND_DEVELOPER_GUIDE.en.md) before extending Leo. Worker processes are not a permission sandbox; internal modules still require host composition. Include relevant checks and exclude personal settings, keys, models, conversations and runtime directories. Review licensing and asset rights before redistribution.

## UI examples

Historical demo screenshots; fixtures are not current and layouts may differ from main.

| Custom skin | Notes |
| --- | --- |
| ![Custom skin](src/docs/screenshots/v0.53-new-skin.png) | ![Notes](src/docs/screenshots/v0.53-notes.png) |

# Leo · A desktop companion you configure your way

[简体中文](README.md) | **English**

Find apps, tasks, notes, football and everyday actions faster, and combine useful steps. Use just the pet if you prefer; every optional feature does not need to be enabled.

**Windows x64 · Latest release v1.05.0 · Local features need no model API.**

[Download](https://github.com/LeoLi006/leo-desktop/releases/tag/v1.05.0) · **[Three-minute quick start](src/QUICK_START.en.md)** · [Find a feature](src/QUICK_START.en.md#where-do-i-find-it) · [Troubleshooting](src/QUICK_START.en.md#if-something-is-wrong)

## First time? Do these four things

1. Download `Leo-v1.05.0-win-x64.zip`, extract everything and run `Install-Leo.cmd`.
2. Click the sheep, type an app name and open a result. Click again to dismiss search.
3. Click the planner icon below it and add one task, or right-click→Planner & notes→Quick capture.
4. Right-click→Settings: General changes name, size and startup; Features enables only wanted tools. Click Save settings.

No need to understand modules, interfaces or implementation. API chat, speech models and third-party applications are optional setups.

## Cannot find a feature?

| Entry | What is there |
| --- | --- |
| **Click the sheep** | Apps, file/folder paths, clipboard and the top shortcut bar |
| **Chat / planner / expand-tools below it** | Chat, tasks and enabled terminal, Clash and Edge utilities |
| **Planner & notes context menu** | Daily/weekly planning, capture, notes, notification records and focus |
| **Apps & media context menu** | Apps, chat, music/media and optional entries |
| **Routines / Appearance context menus** | Multi-action routines, skins and theme editor |
| **Settings** | Feature switches, program paths, icons, language, hotkeys, backup and checks |

**Enabled but no button?** Check Settings→Features→Entry visibility: it may be menu-only or search-only. Scroll down for lower controls and save changes afterward.

[See all entry points and steps→](src/QUICK_START.en.md)

## Use what suits you

- **Pet only:** name, size, skin, docking/hover reveal; disable unneeded tools.
- **A cleaner desktop:** app search, pins/aliases, file paths and your own app shortcuts.
- **Quick organization:** daily/weekly/calendar tasks, recurrence/reminders and notes that can become tasks.
- **Football and music:** followed teams, fixtures/standings, watch links, QQ Music song requests and media controls.
- **Fewer repeated steps:** combine existing actions; explicit operations run locally first.
- **Only when needed:** computer status, existing mail-client entry, model chat and local speech.

Leo complements specialist applications. Email stays in your mail client; music/media need their clients, and Huorong is launch-only. Free chat requires your own provider; speech requires a separate SenseVoice model. Football/song lookup uses separate network sources.

## Product users only need these guides

| Guide | Read it when |
| --- | --- |
| **[Quick start](src/QUICK_START.en.md)** | New to Leo or cannot find a feature |
| [Full user guide](src/USER_GUIDE.en.md) | Need settings, limits or further troubleshooting |
| [Change log](src/RELEASE_NOTES.en.md) | Want to see what changed |

Chinese/English pages are separate with a language link at the top. GitHub documentation is current; the original v1.05.0 archive retains its release-time guides and was not repackaged for these documentation changes.

## Updates and feedback

Update to the original installation folder and retain a private backup. Releases include SHA-256 checksums and a bundled Windows desktop runtime, excluding personal settings, keys, speech models and external clients.

Start with Settings→Checks and [troubleshooting](src/QUICK_START.en.md#if-something-is-wrong). If needed [report an issue](https://github.com/LeoLi006/leo-desktop/issues) with version, reproduction and sanitized screenshots; never upload keys or the whole runtime folder.

<details>
<summary><strong>Developers and extension authors: architecture, interfaces and builds</strong></summary>

Product users can skip this section.

- [Product and developer guide](src/PRODUCT_AND_DEVELOPER_GUIDE.en.md)
- [Modules/packages](src/MODULES.en.md) · [Architecture](src/Architecture/README.en.md)
- [Skin format](src/SKIN_FORMAT.en.md) · [Animation maintenance](src/ANIMATION-NOTES.en.md) · [Jersey assets](src/Assets/MatchDay/Kits/README.en.md)
- [Verification/performance](src/PERFORMANCE.en.md) · [Localization](src/Localization/README.en.md)
- [Positioning research](src/RESEARCH.en.md) · [Candidate roadmap/status](src/ROADMAP.en.md)

Requires Windows x64, the [global.json](global.json) SDK, Git and PowerShell.

```powershell
./scripts/Build.ps1 -Locked
./.build/Release/LeoLauncher.exe
```

Internal modules still require host composition; worker extensions are not a permission sandbox. Include relevant checks and exclude personal data, keys, models and runtime folders. Review project/third-party terms, trademarks and asset rights before redistribution.

</details>

# User guide

[简体中文](USER_GUIDE.md) | **English**

**New here, or cannot find a feature? Start with the [quick start](QUICK_START.en.md). Use this page for further reference.**

Applies to v1.05.0. The older v1.00.0 binary does not contain every later change.

[Home](../README.en.md) · [Quick start](QUICK_START.en.md)

## Setup

Download the complete ZIP from GitHub Releases, extract it and run Install-Leo.cmd. When upgrading, use the original installation location and retain a private backup. Do not directly overwrite a personal installation with development output.

Right-click the companion → Settings for name, size, shortcuts, startup, modules, API, migration and runtime checks. Select Chinese or English at the top of General and save. Computer status and mail are off by default. UI language does not translate personal content or change speech/model language.

## Everyday controls

| Action | Behavior |
| --- | --- |
| Click | Toggle app search; the companion stays fixed while search is open |
| Drag | Move; release near a monitor edge to dock |
| Hover docked head | Reveal companion and buttons |
| Esc | Dismiss the current window or panel |
| Right-click | Grouped records, apps/media, routines, appearance and settings |

Hiding an entry changes presentation; disabling rejects its actions and stops owned background work. It does not close an external application.

## Applications and files

Search names, pinyin and aliases such as wx, weixin or vsc. Right-click an app to set aliases; drag pins to reorder. Candidates use two rows of seven. Enter a full file/folder path, including spaces, quotes or environment variables.

Use the grid icon for apps and the folder icon for combined file/folder search (Ctrl+1 / Ctrl+2); each opening defaults to apps. File and folder name matches appear together. Name searches cover the selected directory and its subdirectories (Documents initially); click Search in to change it, or paste a full path. Select with arrows and press Enter to open. Click VS Code · new window or press Ctrl+Enter to open the selected file/folder in a new VS Code window. If VS Code is not found, select Code.exe. Searches run locally without a model, skip hidden/system entries and links, and stop at 3 seconds, 30,000 entries or 80 results; narrow the scope when prompted. The selected search directory and manually selected Code.exe last for the current run.

Use arrows to select, Enter to open, Tab for path completion, Ctrl+L for input and Shift+F10 for the candidate menu. Drop an executable to add a module; other files, text and links show relevant actions.

Application settings support path, icon, arguments, working directory and administrator launch. Choose from 18 built-in icons or a custom image. Shortcuts support ordering/overflow. Path repair requires confirmation.

## Tasks, notes and clipboard

Use daily, weekly, calendar and overdue views; local natural input, recurrence/exceptions, context actions, double-click editing, date-drop rescheduling, postponement and supported undo. Review parsed input before adding. Snooze reminders for 10 minutes or one hour and configure quiet hours. Reminders are not guaranteed while Leo is closed.

Configure the quick-note shortcut; Enter saves and Esc cancels. Search/pin notes or convert them to tasks. Search for the clipboard action to open history/pins. Listening and sensitive-content protection are configurable, but cannot detect every secret.

## Football, music and media

Manage club/national-team following, results, upcoming matches, team fixtures and standings; save watch links and add actual matches to the planner. On network failure cached content remains; check timestamps/staleness. Background schedule synchronization depends on source availability.

QQ Music provides quick single-song requests: search and select a real result; Leo starts/waits for the client when needed. Controls and lyrics depend on client state/configuration. Manage playlists in QQ Music; playlist sync is not provided. PotPlayer/media controls target configured clients or Windows media sessions.

Clash starts silently and waits before proxy operations when necessary. Follow observed results. Disabling the module does not turn off the proxy.

## Computer status, Huorong and mail

The read-only status view shows CPU, memory, aggregate network, system-disk space and process ranking, with a Task Manager shortcut. It does not kill processes or clean files. Optional alerts use CPU 90% or memory 92% sustained for30 seconds, with a15-minute cooldown. High usage does not imply malware.

Huorong personal is a client shortcut, not scanning/security reporting. Mail opens existing Foxmail/Thunderbird/classic Outlook or a selected EXE/LNK; QQ/Gmail can open websites. Configure campus mail in the original client. Leo collects no mail passwords and does not retrieve messages or unread counts.

## Chat, speech and routines

Configure your compatible endpoint, model and key privately. Explicit commands run locally first; facts come from data modules and complex understanding uses the model. Tool plans require the existing validation/preview rules and real target IDs.

Speech uses a separately installed local SenseVoice model. Check microphone permissions and model setup; English UI does not automatically configure English recognition. Stop/close discards stale results. Routines are sequential existing actions with cancellation/recovery, not an arbitrary scripting system.

## Skins and configuration

Choose five built-in skins, create independent themes, import jerseys/badges and edit colors/brightness/radius/opacity. Planner quotes use one per line; empty disables them. See [skin format](SKIN_FORMAT.en.md). Preview profile changes; public share packages are not private backups.

## Troubleshooting

Start with Settings → Runtime checks: inspect enablement, paths, dependencies and error types. Installed does not mean operational. Routine success goes to quiet logs. Before retrying a failure, check whether an external effect already happened.

Report version/commit, Windows environment, reproduction and sanitized screenshots. Do not upload runtime directories, keys, private conversations/clipboard or complete personal logs.

## Speech model, migration and usage

Source users can run the existing repository script below to install the local model. The initial download requires network access; model files stay out of Git. Binary-package users should check the package's own instructions rather than assume repository scripts exist in the installation.

```powershell
./scripts/Install-VoiceModel.ps1
```

Recognition runs locally; recognized text is sent to your configured provider if routed to model conversation. Leo has no general web-browsing tool. Browser search and dedicated fixture sources do not mean every model reply was verified online.

Use Settings → General → Configuration migration/recovery. Review the import summary and retain backups. Migration exports differ from public shares: they may contain tasks, URLs, aliases and program paths, so keep them private. API keys, chat, clipboard and speech models are excluded; do not upload migration files to GitHub.

Usage defaults to 100 model-generation requests daily;0 is unlimited. Failed/cancelled requests can still count if sent; listing models does not count. Tokens reflect provider-reported values, not estimated cost. Disable unsupported token reporting if necessary. Local functionality remains available after reaching the model limit.

## AI Briefing: agents and full-duplex voice research

- **Find it**: click Leo, then the newspaper shortcut at the top (or More if it overflows). Search AI新闻 / 论文 / 全双工, or open AI Briefing from the context menu.
- **Views**: switch Brief / Research at the top right. Brief is the default, with news-style headlines and short excerpts; Research retains the existing abstracts and metadata. Both put papers before GitHub, with newest dates first within each group. Switching retains filters and does not refetch.
- **Content**: relevant recent arXiv papers and GitHub repositories active in the last 30 days. Filter by Agents, Full-duplex / Voice, Papers or Open source. The text box filters fetched titles, abstracts and authors locally.
- **Read**: click a card to open the original paper or repository. Read this item plays one card; Read top stories plays up to five visible items. Stop cancels reading, as does closing the window. No microphone or SenseVoice model is used.
- **Interpretation**: each source has a fetch timestamp. Paper dates mean first publication; repository dates mean last push. Projects are selected by total stars among recently active repositories, **not daily star growth or a comprehensive ranking**. Titles and abstracts remain in their original language. No generated scientific claims or automatic translation; pronunciation depends on installed system voices.
- **Network and resources**: on-demand refresh when opened or explicitly requested; fresh results are reused for 10 minutes, with at least one minute between attempts. A failed/rate-limited source retains its own previous items and timestamp. Closing cancels in-flight refresh. There is no persistent news polling.
- **Control**: Settings → Features → AI Briefing. Entry visibility is separate from enablement. The companion-only profile disables the module.
- **Cost and privacy**: no model API key or paid model calls; no access to private GitHub repositories, chat or clipboard. Only public metadata is cached; full papers/repositories are not downloaded. First use requires a network connection.

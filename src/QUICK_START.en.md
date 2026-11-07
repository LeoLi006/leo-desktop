# Leo quick start

[简体中文](QUICK_START.md) | **English**

For v1.05.0. Read the first two sections once; afterward use the task lookup table. **App search, tasks and quick notes work without a model API. You do not need API setup to get started.**

[Download Windows version](https://github.com/LeoLi006/leo-desktop/releases/tag/v1.05.0) · [Full user guide](USER_GUIDE.en.md) · [Troubleshooting](#if-something-is-wrong)

## Get started in three minutes

1. **Install:** download `Leo-v1.05.0-win-x64.zip`, extract everything, then run `Install-Leo.cmd`. For an update, choose your existing Leo installation folder.
2. **Find an app:** click the sheep, type an app name, then click a result or press Enter. Click the sheep again to hide search.
3. **Remember one task:** click the planner icon below the sheep, enter a task and add it. Alternatively right-click the sheep→Planner & notes→Daily planner.
4. **Make it yours:** right-click→Settings. General changes name, size and startup; Features enables only the tools you want. Click Save settings when finished.

Want only a pet? No API or other application is required. Settings→General→Profiles lets you preview a pet-only template and confirm the switch; alternatively disable unwanted features yourself.

## Learn the four entry points

| Entry | Where/how | What it opens |
| --- | --- | --- |
| **Click the sheep** | Click again to dismiss app search | Apps, file paths, clipboard and the top shortcut bar |
| **Below the sheep** | Chat, planner and an expand-tools button | Chat/planner; enabled terminal, Clash and Edge utilities after expansion |
| **Right-click the sheep** | Grouped menu | Records, notes, focus, apps/media, routines, appearance, settings and exit |
| **Top of app search** | Click the sheep first | Visible football, QQ Music, media and custom apps; overflow goes under More |

Icons/order can be customized. Hover top shortcuts for names or use text menus rather than identifying features by pictures alone.

**Cannot find the sheep?** Check monitor edges: docking can leave only its head visible; hover to reveal it. Find Leo among Windows hidden tray icons and right-click for Settings. Default Ctrl+Alt+Space opens search. Leave fullscreen first if fullscreen hiding is enabled.

## Where do I find it?

“Right-click” below means the sheep. Optional functions must be enabled. Scroll down inside long settings pages.

| I want to… | Go here |
| --- | --- |
| Find an app, folder or file | Click the sheep→search field |
| Pin an app or set aliases | Right-click an app result icon |
| Add my own program/change its icon | Settings→Features→Apps & icons… |
| Enable football, QQ Music, Clash, etc. | Settings→Features→check it, then Save settings |
| Keep a feature but hide its button | Settings→Features→Entry visibility…; confirm, then Save settings |
| Daily/weekly tasks, calendar or overdue tasks | Planner icon below the sheep, or Planner & notes→Daily planner |
| Capture one line | Planner & notes→Quick capture… |
| Search/pin notes or convert to a task | Planner & notes→Quick notes… |
| Clipboard history and pins | Click sheep→search `clipboard` |
| Notifications/operation logs | Planner & notes→Notification inbox |
| Temporary focus | Planner & notes→Focus |
| Football, followed teams and standings | Football shortcut at top of app search, or Apps & media→football entry |
| Request a song, pause or lyrics | QQ Music top shortcut, or Apps & media→Music |
| PotPlayer/system media controls | Media top shortcut, or Apps & media→Media controls |
| Admin terminal, Clash or web search | Expand utilities below sheep, or corresponding Apps & media entry |
| Computer status, Task Manager or Huorong | Enable Computer status; use its top shortcut or Apps & media→Module entries |
| Open/configure email client | Enable Mail client; use its top shortcut or Apps & media→Module entries |
| Chat or speak | Chat icon below sheep, or Apps & media→Text chat…/Speak now… |
| Run several actions together | Routines→new custom routine option |
| Skin, theme or jersey | Appearance→skin submenu / Theme & skin editor… |
| Name, size, language or startup | Settings→General |
| Change keyboard shortcuts | Settings→Hotkeys |
| Configuration backup/migration or updates | Settings→General→migration/recovery or update check |
| Diagnose a feature/path problem | Settings→Checks, or the module-status button near bottom of Features |

> **Missing an entry? Do not reinstall first.** Check its switch in Features, then Entry visibility. Menu only hides shortcuts; Search only hides both buttons and menus. Confirm that dialog, then click Save settings. Hiding is different from disabling.

## Common tasks

Expand only the section you need.

<details>
<summary><strong>Find an app, pin it and set short aliases</strong></summary>

1. Click the sheep and search an app name, pinyin or existing alias.
2. Right-click its result and choose Pin or the alias option. Separate multiple aliases with spaces.
3. Search the alias next time. Drag pinned icons to reorder; up to 14 pins are supported.

Enter a full file/folder path to open it. Use arrows to select, Tab to complete a path and Enter to open. First launch may need time for app indexing.

Use the grid icon for apps and the folder icon for combined file/folder search (Ctrl+1 / Ctrl+2); each opening defaults to apps. File and folder name matches appear together. Name searches cover the selected directory and its subdirectories (Documents initially); click Search in to change it, or paste a full path. Select with arrows and press Enter to open. Click VS Code · new window or press Ctrl+Enter to open the selected file/folder in a new VS Code window. If VS Code is not found, select Code.exe. Searches run locally without a model, skip hidden/system entries and links, and stop at 3 seconds, 30,000 entries or 80 results; narrow the scope when prompted. The selected search directory and manually selected Code.exe last for the current run.

</details>

<details>
<summary><strong>Add a program or change its name, icon and position</strong></summary>

1. Settings→Features→Apps & icons: add an executable/shortcut. Alternatively drop an EXE/LNK onto the sheep and confirm.
2. Select the path, name and icon. Most users can leave arguments/working directory blank.
3. Confirm the editor, then Save settings. Choose Shortcut bar in Entry visibility if you want a visible button.

Drag top shortcuts to reorder. More contains overflow items and the ordering dialog. Search-only capabilities do not have shortcut buttons.

</details>

<details>
<summary><strong>Add tasks, repeat them, reschedule and set reminders</strong></summary>

1. Open Daily planner, select a date in the calendar, enter a title and add it.
2. Switch Daily/Weekly views at the top; use the one-time/repeat selector to change recurrence. Check a task to complete it; double-click its title to edit.
3. Drag a task onto a calendar date to reschedule. Right-click it for reminder/rescheduling actions. Recurring tasks can apply an action to only this occurrence.

Overdue collects older incomplete work; postponement moves unfinished tasks. Quiet hours/backups are at the lower left. Leo must keep running for reminders. Chinese natural-date expressions have a confirmation preview; English UI does not imply equivalent English date parsing. English titles can use an explicitly selected date/time.

![Empty planner with calendar, daily/weekly tabs and reminder settings](docs/screenshots/quickstart-planner-en.png)

Illustration from isolated sample data; dates, skin and user-supplied quotes may differ.

</details>

<details>
<summary><strong>Quick notes, clipboard and dropped content</strong></summary>

- **Capture:** Planner & notes→Quick capture; Enter saves and Esc cancels. Default Ctrl+Alt+N.
- **Manage notes:** search/pin in Quick notes. Select a note, convert to a task and choose its date.
- **Clipboard:** search `clipboard`. If new items are missing, enable Save clipboard history in General. Protection options are in the same page.
- **Drop content:** files, images, text or links dropped onto the sheep show applicable actions; they do not automatically execute every action.

</details>

<details>
<summary><strong>Follow teams, view fixtures and save watch links</strong></summary>

1. Open football and use ＋ Manage teams on the left to select clubs/national teams and save.
2. Click a followed team for recent matches/standings. Use the in-window back control to return.
3. A match card's planner/reminder action adds it to tasks after checking date/reminder. Edit Watch links on the left to save four URLs; clicking one opens Edge.

Updates need network access. Check cache timestamps after failure. Leo opens your selected websites; it does not supply match broadcasts.

</details>

<details>
<summary><strong>QQ Music, PotPlayer, proxy and browser search</strong></summary>

1. Enable the needed tools in Features. If an application cannot be found, choose its installed program in Apps & icons.
2. Search a song/artist in QQ Music's Leo window, choose a result and play. Leo starts the client if necessary. Playback, pause and lyrics depend on client readiness/song access; manage playlists in QQ Music itself.
3. Media controls target PotPlayer or available Windows media sessions. Clash is in expanded utilities below the sheep; the Edge icon expands a search field.

Ctrl+Alt+L toggles QQ Music lyrics by default; change it in Hotkeys. Disabling Leo's music/proxy module does not close the client or undo an enabled proxy.

</details>

<details>
<summary><strong>Computer status, Huorong and email</strong></summary>

These start disabled. Enable Computer status or Mail client in Features and save first.

- Computer status shows CPU/memory/network and process ranking. Bottom buttons open Task Manager/Huorong; Huorong's path is configured there. Background high-usage alerts have a separate checkbox.
- Mail opens an existing client; configure its path in the mail window. QQ/Gmail web entries are available. Configure campus email in the original mail client.

Leo does not kill processes, scan for viruses or retrieve email messages. Those operations stay in the original applications.

</details>

<details>
<summary><strong>Chat, voice and multi-action routines</strong></summary>

- Click the chat icon. Start with its existing local task/query choices. Free chat requires your endpoint, model and key in Settings→API, saved separately.
- Right-click the chat icon or use Apps & media→Speak now. Default Ctrl+Alt+V. The current package excludes the SenseVoice model; use text until the model is prepared. Checks helps verify the model/microphone setup.
- Routines→new custom routine: name it, add steps and save. Run it by name in that menu; edit/delete is there too.

Fixed routines use no model API. Plans requiring confirmation display before execution. Stop does not undo steps already completed.

</details>

<details>
<summary><strong>Appearance, profiles, shortcuts and updates</strong></summary>

- Appearance switches skin/opens the editor. Name, size and interface language are in General.
- General→Profiles previews a set of feature/entry choices. Review differences before confirming.
- In Hotkeys, click a field and press the combination, then save. Backspace/Delete disables it. Choose another combination if occupied.
- General contains update checks and configuration migration/recovery. Backups can contain personal tasks/URLs; do not publish them.

</details>

## Default shortcuts

| Keys | Action |
| --- | --- |
| Ctrl+Alt+Space | Toggle app search |
| Ctrl+Alt+N | Quick capture |
| Ctrl+Alt+V | Open chat and speak; speech model must be prepared |
| Ctrl+Alt+L | QQ Music lyrics; enabled/configured music client required |
| Esc | Dismiss current window/panel, not Leo itself |

If customized, follow the actual values in Settings→Hotkeys.

## If something is wrong

| Symptom | First step |
| --- | --- |
| Missing button | Check Features, Entry visibility and More overflow |
| Settings seem unchanged | Save General/Hotkeys/Features; API saves separately |
| App not found | Allow indexing, try full name, check Enable local app indexing, or add it manually |
| Invalid client path/music or proxy unresponsive | Checks, then reselect the program in Apps & icons |
| No reminder | Check Leo is running, task date/time, quiet hours and focus |
| Cannot speak | Check model/microphone; use text rather than reinstalling the entire app |
| No fresh football data | Check network/cache time and refresh later |
| Need operation history/errors | Planner & notes→Notification inbox; routine success is quiet |
| Want to exit | Right-click sheep/tray icon→Exit Leo; Esc only dismisses the current window |

Still stuck? [Report an issue](https://github.com/LeoLi006/leo-desktop/issues) with version, clicks, expected/actual behavior and sanitized screenshots. Never attach a key or the entire runtime folder.

For more operations read the [full user guide](USER_GUIDE.en.md). Architecture/protocol/developer documents are optional for product users.

## AI Briefing: news and paper/project briefs

- **Find it**: click Leo, then the newspaper shortcut (or More if it overflows). Search AI新闻 / 论文 / 全双工, or open AI Briefing from the context menu's module entries.
- **Brief**: original headlines and short excerpts, papers before GitHub, newest dates first within each group. Filter papers/projects separately. Repositories are active in the last 30 days and selected by total stars, not daily star growth.
- **News**: separate RSS feeds from Google News, QbitAI, OpenAI and Google DeepMind provide publisher reports and official announcements, newest first. arXiv abstracts and repository entries are excluded. Google News links open via its aggregation page; other feeds link to the original article.
- **Chinese headlines**: in News, click Chinese titles · API to use your configured relay/model. Only public headlines are sent, not abstracts, chat or clipboard. Batches of 20, up to 60 headlines / 3 requests per click, count toward existing usage limits and may incur model charges; this is not free offline translation. Translations are cached. Show original / Show Chinese changes the display without another call. Excerpts remain in their original language. Switching fields or closing cancels pending translation; requests already sent still count.
- **Field selector**: 24 choices span all AI, agents, full-duplex voice, speech, LLMs, reasoning, NLP, multimodal models, vision, image/video generation, robotics, RL, RAG, alignment/safety, evaluation, training, edge/compression, systems/chips, AI4Science, medicine, recommendation, graph learning, privacy and industry/policy. The field is shared across both views; switching queries the relevant categories/keywords rather than filtering old agent results. Feed coverage and keyword matching are limited, so this is not exhaustive news or literature search.
- **Read aloud**: click a card to open its source. Read this item or Read top stories plays original headlines and short excerpts (up to 5 items). Stop or close the window to cancel. No recording, SenseVoice requirement, automatic translation or generated scientific conclusions.
- **Cache and network**: fetches on opening or changing selection; text filtering remains local. Each mode/field has its own cache and original source timestamps. Failed sources retain only that selection's older content. Fresh results are reused for 10 minutes, with at least a minute between refreshes of the same selection. Switching cancels obsolete requests; there is no persistent news polling. Older caches upgrade on new queries.
- **Control and cost**: Settings → Features → AI Briefing; entry visibility is separate. The companion-only profile disables it. Fetching, reading aloud and cached views do not call a model. Only manual headline translation uses your configured model API/key. No private repository, chat or clipboard access. Only public metadata and headline translations are cached; full papers/repositories are not downloaded.

## Web AI quick input: Gemini / DeepSeek / ChatGPT

- **Find it**: expand Leo's foot tools and click the original search button. Above the input is a separate group of four target icons: Edge (official logo), Gemini (sparkle), DeepSeek (blue whale), ChatGPT (white knot logo). The selected target stays highlighted. Type, select a target and press Enter: Edge searches the web; the three AI sites open their new-chat entry, fill and submit. Selecting a target never submits or closes the input, and preserves your draft. The last selection is kept for the running session; restart defaults to Edge. Click the search button or disclosure again to hide both input and target icons. Entry visibility follows the Edge module settings.
- **Page notices**: all three AI sites use the same concise messages in Leo's selected UI language, independently of the site's language.
- **Upgrade an existing extension**: at `edge://extensions/`, reload the former “Leo → Gemini” extension. Its new name is **Leo Web AI**, version **1.2.0**. Approve the added ChatGPT/DeepSeek sites if prompted, and ensure it is enabled. No additional extension installation is needed; the `gemini-bridge` folder stays in place.
- **First installation**: right-click any web AI button. Enable Edge Developer mode, choose Load unpacked and select the opened `gemini-bridge` folder. Sign in to each site separately. Without the enabled extension, only the website opens; nothing is filled or submitted automatically.
- **Limits**: no model API or key; each website's own account access, quotas and connectivity requirements apply. Up to 2,000 characters, a 60-second wait and a Cancel option. Existing drafts are preserved; manual edits stop automation. Uncertain sends are not retried. Handle sign-in, CAPTCHA and consent prompts yourself; Leo does not bypass them.
- **Privacy and troubleshooting**: the extension only runs on these three sites, without accessing chat history, credentials or other websites. Prompt text travels in a URL fragment that is consumed immediately; Leo does not log it. The browser/site may still observe or retain URLs/content. Hand-offs expire after two minutes and refresh does not resubmit. Site changes may require adapter updates; copy the prompt for manual sending on timeout. After reloading the extension, start a new request from Leo; old tabs do not resend.

## Mojian: Word / Markdown handwriting conversion

Optionally connect [Mojian handwriting tool](https://github.com/LeoLi006/handwriting-tool) through Leo's existing local-application modules. Open its independent GUI from the shortcut bar, menu or search. Mojian converts Word `.docx`, Markdown and TXT to handwriting-style PNG/PDF locally; rendering, fonts and preview stay in Mojian and do not use Leo's model API.

- **Automatic setup**: download/extract Mojian and follow its [Leo integration guide](https://github.com/LeoLi006/handwriting-tool/blob/main/integrations/leo/README.en.md), specifying your own installed Leo folder. The script backs up settings, avoids duplicate entries and preserves existing entry preferences. Shared packages contain no personal paths/settings.
- **Manual setup**: create a Windows `.lnk` shortcut to Mojian's `启动GUI.vbs`, then add that shortcut in Leo Settings → Features → Application modules. Alternatively import the [data-only package](Integrations/Mojian/mojian.leoapp.json) and choose your local shortcut in the preview. Save settings and search `墨笺`; automatic setup also adds `mojian`, `shouxie` and `handwriting`. Check More if the shortcut overflows.
- **Scope**: disable/remove the entry without deleting Mojian or documents. Download both tools separately; Leo does not bundle Python, the converter or your documents. This integration/docs update does not rebuild older release packages. Unattended conversion/export from Leo is not implemented.

## Application folders and drag-to-open

- **Application folder**: click Leo, right-click an application tile, then choose Show application in folder. Explorer selects the real executable, resolving Start menu shortcuts rather than showing their shortcut folder. Custom applications use their current configured path. Store apps, URLs and internal Leo entries without an ordinary program directory do not get this action. Pinning and aliases remain available; invalid paths produce an isolated error.
- **Choose after dropping**: in the normal visible state, drop files/folders onto Leo's head, face or body. The existing panel appears near Leo within the current screen, with the active theme/language. Nothing runs merely because it was dropped. Use Tab/arrows, Enter to choose, and Esc to close.
- **Open options**: the system default app (Explorer for folders); VS Code in a new window, always using `--new-window`, when Code.exe is available; PotPlayer for a single media file only when its module and executable are available. Choose another app offers Windows Open With first, letting Windows provide file-type recommendations, plus manually configured apps and an EXE browser. Configured apps are not advertised as compatible/system-recommended. Saved arguments, working directory and administrator options are preserved; UAC can occur only after explicit selection.
- **Batch scope**: all accepted paths and their count are shown. Up to 100 items; duplicate paths are merged. If any input no longer exists, reject the request instead of silently dropping it. Default opening submits each item; VS Code receives all paths in one new-window request; Reveal locates every item. Windows Open With, manually chosen programs and PotPlayer currently support one item only. Batch restrictions are explicit, never a silent first-item-only success. The shared action payload-size limit still applies; split very long path lists.
- **Secondary actions**: copy paths, reveal, save a note, add a task and add an application module are below the opening options. Notes/tasks aggregate the list rather than creating one record per file; tasks show an editable confirmation. Programs, scripts and shortcuts are not executed by default; add a module or explicitly choose an app to inspect them.
- **Receipts and limits**: a submitted request does not prove the target application opened the file. Associations may be missing and applications may not support the format; Windows owns its recommendations and association UI. Closing/cancelling stops later work but cannot recall submitted requests. Partial failures report the submitted count and do not retry automatically. Windows privilege differences can restrict drag-and-drop between processes.

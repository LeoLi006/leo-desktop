# Q1 当前动作清册

由 `q1_audit.py` 从当前源码生成；只统计精确字符串引用，不是完整调用图。动态拼接仍须结合 INVENTORY.md 人工追踪。

| 动作 ID | 当前归属 | 精确引用文件 |
| --- | --- | --- |
| `files_with_app` | core | [DropActions53.cs](../../DropActions53.cs) |
| `notes_open` | core | [QuickRouter.cs](../../QuickRouter.cs) |
| `note_add` | core | [QuickRouter.cs](../../QuickRouter.cs) |
| `path_reveal` | core | [DropActions53.cs](../../DropActions53.cs) |
| `media_file` | media | [DropActions53.cs](../../DropActions53.cs) |
| `local_app_open` | core | [LocalApplications.cs](../../LocalApplications.cs) |
| `path_open` | core | [ActionDispatcher.cs](../../ActionDispatcher.cs), [QuickRouter.cs](../../QuickRouter.cs) |
| `module_list` | core | 动态组合/无精确引用，需人工追踪 |
| `module_run` | core | [ActionDispatcher.cs](../../ActionDispatcher.cs) |
| `command_text` | core | [ActionDispatcher.cs](../../ActionDispatcher.cs), [ChatForms.cs](../../ChatForms.cs) |
| `media_open` | media | [QuickRouter.cs](../../QuickRouter.cs) |
| `media_status` | media | 动态组合/无精确引用，需人工追踪 |
| `media_toggle` | media | [ComboEditor.cs](../../ComboEditor.cs), [CustomCombos.cs](../../CustomCombos.cs), [QuickRouter.cs](../../QuickRouter.cs) |
| `media_play` | media | [QuickRouter.cs](../../QuickRouter.cs) |
| `media_pause` | media | [ComboEditor.cs](../../ComboEditor.cs), [CustomCombos.cs](../../CustomCombos.cs), [QuickRouter.cs](../../QuickRouter.cs) |
| `media_next` | media | [ComboEditor.cs](../../ComboEditor.cs), [CustomCombos.cs](../../CustomCombos.cs), [QuickRouter.cs](../../QuickRouter.cs) |
| `media_previous` | media | [CustomCombos.cs](../../CustomCombos.cs), [QuickRouter.cs](../../QuickRouter.cs) |
| `media_stop` | media | [QuickRouter.cs](../../QuickRouter.cs) |
| `chat_open` | core | [CustomCombos.cs](../../CustomCombos.cs) |
| `app_search` | core | [ActionDispatcher.cs](../../ActionDispatcher.cs), [AppTools.cs](../../AppTools.cs) |
| `app_open` | core | [AppTools.cs](../../AppTools.cs), [CustomCombos.cs](../../CustomCombos.cs), [Launcher.cs](../../Launcher.cs) |
| `app_open_name` | core | [ActionDispatcher.cs](../../ActionDispatcher.cs) |
| `todo_natural` | core | [ActionDispatcher.cs](../../ActionDispatcher.cs), [ChatForms.cs](../../ChatForms.cs) |
| `todo_complete_title` | core | [ActionDispatcher.cs](../../ActionDispatcher.cs) |
| `web_open` | edge | [ActionDispatcher.cs](../../ActionDispatcher.cs), [CustomCombos.cs](../../CustomCombos.cs), [DropActions53.cs](../../DropActions53.cs) |
| `todo_skip_repeat` | core | 动态组合/无精确引用，需人工追踪 |
| `todo_undo` | core | [QuickRouter.cs](../../QuickRouter.cs) |
| `music_client` | music | [CustomCombos.cs](../../CustomCombos.cs), [QuickRouter.cs](../../QuickRouter.cs) |
| `music_pause` | music | [QuickRouter.cs](../../QuickRouter.cs) |
| `combo_name` | core | [ActionDispatcher.cs](../../ActionDispatcher.cs), [QuickRouter.cs](../../QuickRouter.cs) |
| `scene_watch` | core | [ActionDispatcher.cs](../../ActionDispatcher.cs), [QuickRouter.cs](../../QuickRouter.cs) |
| `launcher_open` | core | [EntryDisplay.cs](../../EntryDisplay.cs), [QuickRouter.cs](../../QuickRouter.cs) |
| `clipboard_open` | core | [ActionDispatcher.cs](../../ActionDispatcher.cs), [Launcher.cs](../../Launcher.cs) |
| `clipboard_pin_text` | core | 动态组合/无精确引用，需人工追踪 |
| `football_open` | football | [ActionDispatcher.cs](../../ActionDispatcher.cs), [CustomCombos.cs](../../CustomCombos.cs), [EntryDisplay.cs](../../EntryDisplay.cs), [QuickRouter.cs](../../QuickRouter.cs) |
| `football_facts` | football | [ActionDispatcher.cs](../../ActionDispatcher.cs), [QuickActions.cs](../../QuickActions.cs) |
| `todos_open` | core | [ActionDispatcher.cs](../../ActionDispatcher.cs), [CustomCombos.cs](../../CustomCombos.cs), [ExtensionManager.cs](../../ExtensionManager.cs), [QuickRouter.cs](../../QuickRouter.cs) |
| `todo_list` | core | 动态组合/无精确引用，需人工追踪 |
| `todo_add` | core | [ActionDispatcher.cs](../../ActionDispatcher.cs) |
| `todo_complete` | core | 动态组合/无精确引用，需人工追踪 |
| `todo_remind` | core | 动态组合/无精确引用，需人工追踪 |
| `todo_repeat` | core | 动态组合/无精确引用，需人工追踪 |
| `todo_edit` | core | 动态组合/无精确引用，需人工追踪 |
| `todo_move` | core | 动态组合/无精确引用，需人工追踪 |
| `todo_snooze` | core | 动态组合/无精确引用，需人工追踪 |
| `music_open` | music | [QuickRouter.cs](../../QuickRouter.cs) |
| `music_search` | music | 动态组合/无精确引用，需人工追踪 |
| `music_play` | music | 动态组合/无精确引用，需人工追踪 |
| `music_step` | music | [QuickRouter.cs](../../QuickRouter.cs) |
| `todo_postpone` | core | 动态组合/无精确引用，需人工追踪 |
| `todo_stop_repeat` | core | 动态组合/无精确引用，需人工追踪 |
| `match_add` | football | [ActionDispatcher.cs](../../ActionDispatcher.cs) |
| `web_search` | edge | [ActionDispatcher.cs](../../ActionDispatcher.cs), [QuickActions.cs](../../QuickActions.cs) |
| `watch_open` | edge | [ActionDispatcher.cs](../../ActionDispatcher.cs), [CustomCombos.cs](../../CustomCombos.cs) |
| `proxy_status` | clash | [EntryDisplay.cs](../../EntryDisplay.cs), [QuickRouter.cs](../../QuickRouter.cs) |
| `proxy_on` | clash | [ComboEditor.cs](../../ComboEditor.cs), [CustomCombos.cs](../../CustomCombos.cs), [EntryDisplay.cs](../../EntryDisplay.cs), [QuickRouter.cs](../../QuickRouter.cs) |
| `proxy_off` | clash | [ComboEditor.cs](../../ComboEditor.cs), [CustomCombos.cs](../../CustomCombos.cs), [EntryDisplay.cs](../../EntryDisplay.cs), [QuickRouter.cs](../../QuickRouter.cs) |
| `terminal_open` | terminal | [ActionDispatcher.cs](../../ActionDispatcher.cs), [EntryDisplay.cs](../../EntryDisplay.cs), [QuickRouter.cs](../../QuickRouter.cs) |
| `pet_size` | core | 动态组合/无精确引用，需人工追踪 |
| `fullscreen_hide` | core | 动态组合/无精确引用，需人工追踪 |
| `favorite_app` | core | 动态组合/无精确引用，需人工追踪 |
| `alias_app` | core | 动态组合/无精确引用，需人工追踪 |
| `files_open` | core | 动态组合/无精确引用，需人工追踪 |

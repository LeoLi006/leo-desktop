# UI localization — English

[简体中文](README.md) | **English**

Main provides Simplified Chinese (`zh-CN`, default) and English (`en`). Select Settings → General → Interface language and save. Existing/new windows use the selected locale. Missing old fields and unknown startup values fall back to Chinese; imports and manual saves validate supported values.

## Implementation rules

- Preferences.InterfaceLanguage controls presentation only. Do not mutate global/thread Culture, action IDs, model prompts, speech recognition language or command phrases.
- UiLanguage reads the embedded en.json; Chinese source strings are keys. Weak bindings retain canonical source text for controls/menus and handle TextChanged/new children. Chinese restoration uses source text, not reverse translation.
- Use UiLanguage.Source only for legacy identity checks; actions should use IDs and values rather than displayed labels. Custom drawing uses T/DrawText; calendars use explicit UiLanguage.Culture.
- Translate before measuring feedback/menu text. Preserve input, conversation bodies, task/app/song names and custom quotes. ComboChoice translates only with Localize=true; Value stays unchanged. UiLanguage.Preserve or Tag=user-content protects data controls/items. Never call T on user bodies.
- Tooltips/confirmations use host adapters. Unknown extension strings, raw third-party errors and unmapped technical text fall back to source; no network/model translation and no rewriting persisted logs.
- No new polling timer. Closed controls ignore changes, weak bindings avoid retaining windows, disposed tooltips leave the weak directory. Template regexes are nonbacktracking with bounded recursive composition.

## Adding text

1. Add an accurate English value to en.json while retaining the Chinese source key. Numeric/date templates may use {0}/{1}; avoid overly broad patterns that capture arbitrary user text.
2. Ordinary controls bind through the host; custom paint explicitly translates. Check actual English width; use short labels and tooltips for detail.
3. Separate labels from user content and mark the latter preserved. A user item literally named “保存” must not become “Save”.
4. RefreshOnChange may update presentation only: never save business data, launch clients or request network/model work from its callback.
5. Keep protocol identities and permissions stable. Missing translation must not block capability execution. Additional locales require real resources and layout checks.

## Targeted evidence

`LeoLauncher.exe --ui-language-test <absolute-report-path>` covers locale fields/fallback, dynamic controls/menus, data preservation, enum values, long templates, isolated window previews and failure isolation. Reports belong in ignored .build, not personal data directories.

`--chat-resize-test` separately checks resize/restore, reopen, native child repaint, queued refresh after close and absence of idle redraw loops. Localization does not replace this check.

Historical implementation checks: language28, chat resize9, conversation41, desktop tools58, entry display109, usage profiles36 and themes65:346 related checks passed, build0 warnings/errors. This is scoped evidence at the implementation commit, not a full-suite claim for every later commit. Chinese team names, routine/media titles and user quotes are intentionally preserved data. The entry fixture compares the complete ID→enabled map before/after display changes, including default-off modules.

The implementation was locally installed; this documentation/source upload creates no new release package. Chat repaint fix is commit6e4bca5, language integration42fea41.

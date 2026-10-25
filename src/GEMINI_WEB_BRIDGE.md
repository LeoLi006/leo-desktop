# Web AI browser bridge

User instructions: [中文](QUICK_START.md#网页-ai-快捷输入gemini--deepseek--chatgpt) / [English](QUICK_START.en.md#web-ai-quick-input-gemini--deepseek--chatgpt).

## Integration and compatibility

The existing `GeminiWeb` class name and `assets/gemini-bridge` installation path are preserved so the user's unpacked extension does not need reinstalling. `GeminiWeb.Home/Name/Url/Open` accept only gemini, deepseek or chatgpt. `FootTools` keeps one search disclosure button; its expandable textbox is followed by four mutually exclusive Edge/Gemini/DeepSeek/ChatGPT selectors. Selection preserves the draft, stays expanded and restores input focus; only Enter dispatches. Repeated disclosure hides textbox and all selectors. The last selection is session-only, with Edge as startup default. All four targets share draft text, layout/theme scaling and Edge module enable/shortcut rules. No model-registry tool or workflow permission is added; only explicit footer input launches a web hand-off.

Manifest V3 extension **Leo Web AI 1.2.0** runs only on `https://gemini.google.com/*`, `https://chat.deepseek.com/*` and `https://chatgpt.com/*`, with no background script, network fetch, telemetry, cookies API or extra permissions. Existing installations must reload in Edge and approve any added site access. Users sign in separately. No account, CAPTCHA, consent or browser security-policy bypass exists.

## Protocol and adapters

`<allowed-home>#leo-site=<provider>&leo-id=<32-hex>&leo-at=<unix-seconds>&leo-prompt=<encoded-text>`. Legacy Gemini links without leo-site remain accepted. Provider mismatch, unsupported origins/entry paths, invalid ID, empty/oversized text and timestamps outside 120 seconds are rejected. A request is consumed immediately; only the last 64 IDs are kept in per-origin per-tab session storage. Prompt text is not persisted. This is replay protection, not cryptographic proof of provenance: another URL using this protocol can request a send. URL fragments may still be observed/retained by browser/site components.

- Gemini: existing rich-textarea/Quill selectors; semantic Send labels, send marker or material icon.
- DeepSeek: chat-input/visible placeholder textarea or accessible contenteditable; semantic Send controls, markers or the known DS send SVG (`viewBox="0 0 14 16"`) within a button/role-button. No generic submit/last-button fallback. If no known send control is available after 1.5 seconds, DeepSeek may receive one composer-local Enter keydown instead; known disabled/ambiguous send controls or Stop block this fallback. A click and Enter are never both attempted.
- ChatGPT: visible prompt-textarea contenteditable/textarea, named textarea or mobile-composer-prompt; semantic Send, data-testid or composer-submit-button.
- Textareas use the native HTMLTextAreaElement value setter plus a composed input event, supporting React controlled inputs. Rich text preserves the working execCommand insertion plus explicit input notification. Text verification uses value for textareas and textContent otherwise.

One visible editor and one enabled Send control are required. Disabled, Stop/Voice and ambiguous controls are rejected. An existing draft or trusted manual edit stops automation. Only a foreground, focused page can insert/send. Waiting is bounded to 60 seconds and offers Copy/Cancel; after one click the bridge observes composer clearing/removal for up to five seconds and never retries uncertain sends. It reports send triggering, not a completed model response. Page exit/new hand-off cancels pending work. No global SendKeys, clipboard replacement, conversation-history reading or request queue exists. ChatGPT also resolves ProseMirror/Lexical editors without assuming a form wrapper or literal contenteditable=true. Focus-wait notices identify when a composer was found but the page needs foreground focus.

Webpage selectors are compatibility assumptions based on public implementations and must be maintained when sites change. A matching DOM fixture is not evidence of live-account success. Gemini was confirmed working by the user before this extension; the new sites still require live user acceptance after reloading.

## Targeted checks

`--gemini-web-test <absolute-report-path>` checks all provider URLs, allow-list validation, manifest scope, WinForms switching/toggling at multiple sizes, drafts and Edge-module hiding. `node src/Tests/GeminiBridge.test.cjs` exercises the original Gemini safety cases plus DeepSeek/ChatGPT rich/textarea inputs, native setter notification, existing drafts, timeout, uncertain sends, replay and the DS role-button icon path. No live accounts or API requests are used.

Manual acceptance: reload extension 1.1.1, approve site access, log in, then submit one disposable prompt from each new button. Verify one submission and a site response. Check that an existing draft is preserved. Browser-origin changes or renamed/disabled send controls must fail closed, not click an arbitrary action.

## Change notes

- 1.0.1: tolerate document_start before documentElement exists.
- 1.0.2: recognize semantic Gemini send controls and explicitly notify input state.
- 1.1.1: shared site adapters, native textarea support, DeepSeek/ChatGPT footer entries and site binding; preserve Gemini behavior and extension path.

### 1.1.1 compatibility follow-up

User reports: Gemini sends successfully; DeepSeek fills then waits for Send; ChatGPT times out before filling. Added nested-role-button deduplication and the bounded, single-shot DeepSeek composer key fallback described above; expanded ChatGPT editable variants and fallback selectors. Isolated regressions cover native textarea Enter submission, uncertain Enter without retry, disabled control suppression, nested role controls and alternate ChatGPT composer resolution. These are compatibility changes, not a live-browser diagnosis or guarantee; no real-account request was made by the tests.

### 1.2.0 unified entry and notices

Foot tools now contribute one search button instead of four separate utility entries. Target selectors are visible only while the input is expanded and have a persistent selection highlight. Layout/toggle checks cover all targets, preserved drafts, repeated selection, disclosure close and multiple scales. Shared page notices omit provider-specific wording and version strings; leo-lang carries the host UI choice (en/zh-CN), with Chinese as the legacy default. DOM insertion, send controls, DeepSeek Enter fallback and one-shot verification are unchanged. Fixture checks compare identical Chinese notices across all sites and verify explicit English selection despite Chinese page language.

# Gemini web bridge

User instructions: [中文](QUICK_START.md#gemini-网页快捷输入) / [English](QUICK_START.en.md#gemini-web-quick-input).

## Scope and integration

`FootTools` adds a Gemini sparkle beside the existing Edge search button. Both use the same expandable textbox, existing scale/layout/theme handling and Edge module visibility rules. `GeminiWeb` validates an explicit user prompt (1–2,000 UTF-16 characters after trimming), generates a fresh request ID/timestamp and opens Edge using the existing configured browser path. This is a browser hand-off, not a Gemini API tool; it is not exposed to the model registry or workflows. No new background host activity is introduced.

`Assets/gemini-bridge` is a Manifest V3 content-script extension, copied by the existing asset build rule to `app/assets/gemini-bridge`. The user must load that directory once in Edge. There is no automatic browser-policy change, debugging port, separate browser profile, cookie access, external script or extension download. The right-click setup action opens the existing browser's extension page and this local directory. This adds an optional browser-side component, not an independently installable Leo module.

## Hand-off and execution

URL: `https://gemini.google.com/app#leo-id=<32-hex>&leo-at=<unix-seconds>&leo-prompt=<encoded-text>`.

- The content script accepts only the exact Gemini HTTPS origin and its app entry paths; hand-offs expire after 120 seconds. Prompt text is never interpreted as HTML or JavaScript.
- The fragment is consumed immediately. A bounded per-tab session list stores only up to 64 request IDs, not prompt text. Reload does not resubmit. This is replay protection, not cryptographic proof of provenance; another link with the same fragment format can request a send.
- Wait up to 60 seconds for a unique visible rich-text editor, a foreground document and a unique enabled Gemini send button. Current selectors are `rich-textarea .ql-editor[contenteditable="true"]` / `[contenteditable="true"][role="textbox"]`, and enabled buttons carrying the send class/test-id, an exact Chinese/English Send accessibility label, or a `send` material icon. Stop/voice actions are rejected; multiple actionable matches are not clicked.
- Existing nonempty drafts stop automation. A trusted user edit stops sending. Input is inserted into the verified editor; no global SendKeys or system clipboard replacement occurs.
- Click Send exactly once. Observe composer clearing/removal for up to five seconds and report only that sending was triggered, not that a model answered. Unconfirmed sends are never retried. Failure leaves a small page notice with Copy/Cancel; the user can inspect Gemini and act manually.
- Hidden/unfocused pages wait instead of taking over another window. Page exit or a newer hand-off cancels the old pending loop. No prompt queue or account-login automation exists.

Without the extension, the URL only opens Gemini; it does not guarantee prefilling/submission. Browser/site changes may require selector maintenance. Prompts travel in a URL fragment; immediate removal does not guarantee the browser/site never observes or stores them. Gemini's own account, connectivity, regional availability and usage limits still apply.

## Targeted verification

`--gemini-web-test <absolute-report-path>` checks URL/limit handling, request uniqueness, manifest scope, actual WinForms toggle/switch behavior, Edge compatibility, module hiding and small/large scaling. `node src/Tests/GeminiBridge.test.cjs` uses an isolated DOM fixture to check Unicode/special characters, expiry, drafts, login timeout, ambiguous editors, inactive pages, uncertain sends, replay and session-storage failure. Neither sends requests to Gemini or uses a real account/API key.

These checks cannot establish compatibility with the currently signed-in Gemini DOM. Manual acceptance after extension loading: enter one disposable prompt in Leo, verify it appears once in a new Gemini chat and receives a response; try a page with an existing draft and confirm it is preserved. Do not describe the feature as end-to-end verified until this browser check is complete.

References: [Edge local extension loading](https://learn.microsoft.com/en-us/microsoft-edge/extensions/getting-started/extension-sideloading), [Chrome content script model](https://developer.chrome.com/docs/extensions/develop/concepts/content-scripts). No third-party extension source is bundled.

### 1.0.1 startup correction

A regression fixture reproduces execution at document_start before documentElement exists: reading its language threw before the DOM waiting loop. Language selection now tolerates the missing root and falls back to the browser language. Existing safety/one-shot fixtures remain unchanged. After installing the updated assets, an already loaded unpacked extension must be reloaded in Edge before opening a fresh Leo request; no real-account send was performed by this check.

### 1.0.2 send readiness correction

The reported symptom was successful text insertion followed by a wait timeout. The old resolver only accepted two class/test-id markers. The bridge now also recognizes exact Chinese/English send labels, the alternate data-testid spelling and Material send icons, while excluding disabled, Stop/Voice and ambiguous controls. An explicit composed input notification follows insertion so page state can enable Send even if execCommand returns success without a usable notification. Notices now distinguish waiting for an editor from waiting for Send. The send is still one-shot; no keyboard fallback or uncertain resend is added.

Isolated fixtures cover label-only/icon-only buttons, input notification, delayed button readiness, disabled/ambiguous/unknown controls and a coexisting Stop control, in addition to earlier safety checks. This validates the compatibility logic, not the user's live account. Reload the unpacked extension in Edge (version 1.0.2), then start a new Leo request; existing tabs retain their previously injected script.

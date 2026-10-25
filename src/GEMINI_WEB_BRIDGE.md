# Gemini web bridge

User instructions: [中文](QUICK_START.md#gemini-网页快捷输入) / [English](QUICK_START.en.md#gemini-web-quick-input).

## Scope and integration

`FootTools` adds a Gemini sparkle beside the existing Edge search button. Both use the same expandable textbox, existing scale/layout/theme handling and Edge module visibility rules. `GeminiWeb` validates an explicit user prompt (1–2,000 UTF-16 characters after trimming), generates a fresh request ID/timestamp and opens Edge using the existing configured browser path. This is a browser hand-off, not a Gemini API tool; it is not exposed to the model registry or workflows. No new background host activity is introduced.

`Assets/gemini-bridge` is a Manifest V3 content-script extension, copied by the existing asset build rule to `app/assets/gemini-bridge`. The user must load that directory once in Edge. There is no automatic browser-policy change, debugging port, separate browser profile, cookie access, external script or extension download. The right-click setup action opens the existing browser's extension page and this local directory. This adds an optional browser-side component, not an independently installable Leo module.

## Hand-off and execution

URL: `https://gemini.google.com/app#leo-id=<32-hex>&leo-at=<unix-seconds>&leo-prompt=<encoded-text>`.

- The content script accepts only the exact Gemini HTTPS origin and its app entry paths; hand-offs expire after 120 seconds. Prompt text is never interpreted as HTML or JavaScript.
- The fragment is consumed immediately. A bounded per-tab session list stores only up to 64 request IDs, not prompt text. Reload does not resubmit. This is replay protection, not cryptographic proof of provenance; another link with the same fragment format can request a send.
- Wait up to 60 seconds for a unique visible rich-text editor, a foreground document and a unique enabled Gemini send button. Current selectors are `rich-textarea .ql-editor[contenteditable="true"]` / `[contenteditable="true"][role="textbox"]`, and `button.send-button` / `button[data-test-id="send-button"]`.
- Existing nonempty drafts stop automation. A trusted user edit stops sending. Input is inserted into the verified editor; no global SendKeys or system clipboard replacement occurs.
- Click Send exactly once. Observe composer clearing/removal for up to five seconds and report only that sending was triggered, not that a model answered. Unconfirmed sends are never retried. Failure leaves a small page notice with Copy/Cancel; the user can inspect Gemini and act manually.
- Hidden/unfocused pages wait instead of taking over another window. Page exit or a newer hand-off cancels the old pending loop. No prompt queue or account-login automation exists.

Without the extension, the URL only opens Gemini; it does not guarantee prefilling/submission. Browser/site changes may require selector maintenance. Prompts travel in a URL fragment; immediate removal does not guarantee the browser/site never observes or stores them. Gemini's own account, connectivity, regional availability and usage limits still apply.

## Targeted verification

`--gemini-web-test <absolute-report-path>` checks URL/limit handling, request uniqueness, manifest scope, actual WinForms toggle/switch behavior, Edge compatibility, module hiding and small/large scaling. `node src/Tests/GeminiBridge.test.cjs` uses an isolated DOM fixture to check Unicode/special characters, expiry, drafts, login timeout, ambiguous editors, inactive pages, uncertain sends, replay and session-storage failure. Neither sends requests to Gemini or uses a real account/API key.

These checks cannot establish compatibility with the currently signed-in Gemini DOM. Manual acceptance after extension loading: enter one disposable prompt in Leo, verify it appears once in a new Gemini chat and receives a response; try a page with an existing draft and confirm it is preserved. Do not describe the feature as end-to-end verified until this browser check is complete.

References: [Edge local extension loading](https://learn.microsoft.com/en-us/microsoft-edge/extensions/getting-started/extension-sideloading), [Chrome content script model](https://developer.chrome.com/docs/extensions/develop/concepts/content-scripts). No third-party extension source is bundled.

# Positioning-led research: what Leo should build and integrate

[简体中文](RESEARCH.md) | **English**

Date:2026-10-22. Leo source baseline:75f0e95; installed release:v1.05.0. Research/documentation only: no installations, account access, model keys, runtime changes or GitHub upload.

## 1. Product boundary first

Accepted positioning: **a configurable desktop companion that helps users invoke capabilities on their computer faster and combine them.**

Value means fewer searches, window switches and repeated steps. Leo owns entry, context selection, bounded combinations and clear outcomes; established tools own their specialized engines. Pet-only use must not inherit agent/cloud-flow/listener overhead.

Hermes is no longer the organizing goal, just one candidate. Lack of alternatives does not automatically justify building something. High stars do not establish monopoly. Choose by concrete user scenarios, integration cost and operational benefit.

## 2. Method and limits

This survey covers20 GitHub projects across launchers, Windows utilities, input automation, capture, file movement, records, monitoring, pets, workflows and agents.

- Public GitHub API metadata succeeded for19 projects: stars, default branch, archive flag and last push. Dify API failed; its project-page estimate is marked separately. Figures are a2026-10-22 snapshot, not rankings/market share.
- Reviewed project READMEs/official pages and official interfaces in eight integration groups. Some README API failures were followed by page review; missing data was not invented.
- All19 successful metadata records were unarchived. Recent pushes do not guarantee stable releases or long-term support. Development-branch behavior may be absent from installed binaries.
- No tools were installed/run; no resource, latency, compatibility or reliability tests, and no complete source audit. Candidate integration means documented surface, not existing Leo support.
- Stars discover projects. Product completeness, system compatibility, terms, data scope and reduced operations decide suitability. This is not a market census or proof an unmet need has no competitors.

Raw public snapshots stay in ignored local.build, not the production capability inventory. Pin versions/interfaces/terms before implementation.

## 3. What high-attention projects already do

Integration/boundary columns are Leo-specific judgments, not a shipping checklist. Repository links are primary sources for feature summaries.

| Project | Star snapshot | Existing capability | Candidate use in Leo | Boundary |
| --- | ---: | --- | --- | --- |
| [microsoft/PowerToys](https://github.com/microsoft/PowerToys) | 139,561 | Windows utilities, layout and command entry | Use existing tools; invoke a user-created Workspaces shortcut first | Do not build another layout engine/tool suite |
| [Flow-Launcher/Flow.Launcher](https://github.com/Flow-Launcher/Flow.Launcher) | 15,846 | App/file search and plugin entry | Study UX; avoid opening a second launcher | Keep lightweight search; add an index only for a gap |
| [AutoHotkey/AutoHotkey](https://github.com/AutoHotkey/AutoHotkey) | 13,332 | Hotkeys, windows and desktop scripting | Explicit user-selected trusted scripts; verify versions/arguments | No general macro recorder/scripting engine |
| [espanso/espanso](https://github.com/espanso/espanso) | 14,670 | Text expansion and templates | Direct use, optionally a management shortcut | No duplicate text-expansion engine |
| [ShareX/ShareX](https://github.com/ShareX/ShareX) | 40,298 | Capture, annotate, pin and processing workflows | Documented CLI for image editing, pinning and named workflows | Prioritize a concrete-action experiment |
| [localsend/localsend](https://github.com/localsend/localsend) | 95,184 | Local-network file transfer | Current README has a separate sending CLI; validate released Windows availability | Do not rebuild discovery/encrypted transfer |
| [syncthing/syncthing](https://github.com/syncthing/syncthing) | 89,608 | Continuous folder synchronization | Authenticated REST for existing instance status/entry | No sync engine |
| [laurent22/joplin](https://github.com/laurent22/joplin) | 56,830 | Notes, organization and sync | Documented local Data API for selected note handoff | Keep quick notes; no knowledge-platform rewrite |
| [sabrogden/Ditto](https://github.com/sabrogden/Ditto) | 7,350 | Clipboard history and paste | Direct use; suitable structured bridge unverified | No private database copying/default duplicate listener |
| [ActivityWatch/activitywatch](https://github.com/ActivityWatch/activitywatch) | 19,323 | Activity/context tracking | API candidate only for requested statistics | No default complete window/web history |
| [LibreHardwareMonitor/LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) | 9,251 | Temperatures, fans and sensors | Reusable library; privileges/hardware compatibility untested | Keep basic monitoring; no custom driver |
| [rainmeter/rainmeter](https://github.com/rainmeter/rainmeter) | 6,100 | Desktop widgets and skins | Bangs commands as a reference; direct user use | No complete widget platform |
| [LorisYounger/VPet](https://github.com/LorisYounger/VPet) | 7,011 | Desktop pet, animations and ecosystem | Study experience and asset boundaries | Pet interaction is not an empty market |
| [n8n-io/n8n](https://github.com/n8n-io/n8n) | 208,278 | Cross-service automation and visual flows | Configured webhook on an existing instance | No large flow canvas/connector library |
| [node-red/node-red](https://github.com/node-red/node-red) | 23,774 | Event-driven low-code flows | HTTP In/Response for user-owned flows | Keep simple routines; delegate complex flows |
| [open-webui/open-webui](https://github.com/open-webui/open-webui) | 156,403 | Full chat and knowledge workspace | Direct use; specialized integration only for a real need | No complete chat/RAG workbench |
| [NousResearch/hermes-agent](https://github.com/NousResearch/hermes-agent) | 256,049 | Agent tools, memory and execution | Optional backend candidate, neither selected nor connected | No general-agent platform from scratch |
| [fathah/hermes-desktop](https://github.com/fathah/hermes-desktop) | 14,448 | Hermes management/chat client | Study task feedback; no nested full workspace | Leo is not a Hermes-only skin |
| [restic/restic](https://github.com/restic/restic) | 36,721 | Incremental backup and restoration | Invoke an existing backup task only when needed | No complete backup engine |
| [langgenius/dify](https://github.com/langgenius/dify) | ~159k (page, not API) | AI apps, RAG and workflows | Evaluate an existing published app endpoint only | No enterprise AI-app builder |

## 4. Eight integration groups worth checking

### 4.1 ShareX: concrete actions, not another capture app

Official CLI documents image editor, pinning and named workflows. Evaluate dropping an image onto Leo and handing it to ShareX. [CLI](https://getsharex.com/docs/command-line-arguments).

A bare file-path argument uploads it. Do not treat generic file opening as a safe capture action; verify explicit action/workflow settings and automatic uploads. Invocation/editor opening is not evidence the output was saved. Dependent steps need confirmed results.

### 4.2 PowerToys Workspaces: reuse layouts

Official docs support saved workspaces and shortcut launching. Invoke a user-created shortcut instead of copying private workspace formats. [Workspaces](https://learn.microsoft.com/en-us/windows/powertoys/workspaces).

A Leo button alone may be no better than that shortcut. Add an entry only when it helps a useful focus/app/routine combination. Submission does not prove every window was positioned.

### 4.3 LocalSend versus Syncthing: send versus sync

Current LocalSend README documents a separate send CLI and target selection, not proof that the Windows GUI package includes it. [CLI](https://github.com/localsend/localsend#command-line-interface).

Syncthing's authenticated REST interface suits existing sync status/entry, not one-off sending. [REST](https://docs.syncthing.net/dev/rest.html).

Consider selected-file→chosen device/task. Do not rebuild discovery, transport or conflict resolution. Validate receiver confirmation, CLI version, directory scope and completion evidence. Never silently select a recipient; sync is not backup.

### 4.4 Joplin: capture versus knowledge workspace

The local Data API requires an enabled clipper service and token. [API](https://joplinapp.org/help/api/references/rest_api/).

Keep zero-configuration quick notes in Leo. For existing Joplin users, consider handing selected notes/images to a chosen notebook, not rebuilding or scanning the full knowledge base. Validate credentials, saved-result evidence, duplicate retries and missing targets. Do not log full URLs containing tokens.

### 4.5 AutoHotkey: invoke trusted scripts

Official docs describe one-shot or persistent scripts and command-line use. The website returned403; the official docs repository was reviewed instead. [Docs source](https://github.com/AutoHotkey/AutoHotkeyDocs/blob/v2/docs/Program.htm).

No general macro recorder or default model-generated script execution. Only consider scripts explicitly chosen/understood by the user. Validate v1/v2, arguments, working directory, lifecycle and effects. Process exit alone may not prove target-app success.

### 4.6 Node-RED/n8n: trigger existing complex flows

Node-RED documents HTTP In/Response; n8n documents webhooks with different response timings. [Node-RED](https://cookbook.nodered.org/http/create-an-http-endpoint), [n8n](https://docs.n8n.io/integrations/builtin/core-nodes/n8n-nodes-base.webhook/).

Do not duplicate canvases, connectors or credential centers. Connect a configured endpoint only for an existing user flow. No default Node.js/Docker/service installation. Accepted HTTP is not whole-flow completion; timeout is not grounds for blind retry. Validate authentication, results and cancellation. Editor and HTTP-node access controls differ. [Node-RED security](https://nodered.org/docs/user-guide/runtime/securing-node-red).

### 4.7 ActivityWatch/LibreHardwareMonitor: summarize rather than rebuild

ActivityWatch provides activity APIs; LibreHardwareMonitor documents a library and extra privileges for some sensors. [Activity API](https://docs.activitywatch.net/en/latest/api.html), [sensor library](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor#integrate-the-library-in-own-application).

Keep Leo's existing Windows-based CPU/memory summaries. Evaluate sensors only for a real temperature/fan need; do not write drivers. No default full activity-history collection. Recent-app ranking does not require comprehensive behavior monitoring.

### 4.8 Rainmeter, Flow and agents: respect existing boundaries

Rainmeter's Bangs do not justify a replacement widget platform. [Commands](https://docs.rainmeter.net/manual/bangs/).

Study Flow's search/keyboard experience. Opening its search box behind Leo violates the existing preference against an intermediate launcher. No embedded Flow search service has been validated. For a demonstrated full-disk search gap, separately assess the non-GitHub Everything SDK rather than rebuild an index. [Flow](https://github.com/Flow-Launcher/Flow.Launcher), [Everything](https://www.voidtools.com/support/everything/sdk/).

Hermes/Open WebUI/Dify show substantial existing work in agents/chat/knowledge platforms. Do not copy their workspaces. Compare backends only for a concrete task bounded local routines cannot handle; stars do not select Hermes. [Hermes integration](https://hermes-agent.nousresearch.com/docs/developer-guide/programmatic-integration).

## 5. Use, integrate, study or build

| Choice | Scope | Leo's role |
| --- | --- | --- |
| Direct use | Mail, players, security clients, full notes/chat workspaces | Optional clear launch; omit it when there is no benefit |
| Thin integration | Validated CLI/URL/shortcut/system/local API | Target selection, limited parameters, authorization, wait and evidence |
| Study designs | Search, task feedback, animation, plugin descriptions | Local improvements matched to Leo, not wholesale migration |
| Build core handoff | Drop actions, cross-capability context, bounded routines, quiet feedback and optional presentation | A continuous intent-to-result experience |

Calling an application differs from copying code/bundling binaries. GitHub license labels are screening metadata, not legal clearance. Check exact GPL/MPL/custom terms, branding/assets and dependencies. n8n lists Sustainable Use/enterprise terms; Open WebUI has branding-related terms and Dify its own license. Do not label all as freely transplantable MIT code. [n8n](https://github.com/n8n-io/n8n#license), [Open WebUI](https://github.com/open-webui/open-webui#license-), [Dify](https://github.com/langgenius/dify/blob/main/LICENSE).

## 6. Testable differentiation, not an empty-market claim

**A. Content to action:** dropped images/files/text/links receive a few relevant choices based on enabled tools. Compare against Explorer context menus and original-app entry points. Do not add an action that is less convenient.

**B. Personal scenes:** evaluate existing “prepare to watch” routines rather than redevelop them. Users choose layout/media/browser/proxy/reminder combinations. Keep simple routines local; delegate complex cloud flows. Launching several apps is not unique technology.

**C. Quiet companion control:** stay companion-like when idle; briefly show relevant status and interrupt for confirmation/errors. Adapters remain optional; offline/missing clients must not break the pet. Explain external-process ownership and cancellation limits.

These are hypotheses. Existing products already overlap. Prove differentiation through actual operation comparison and repeated use.

## 7. Three experiments, not immediate implementation tasks

| Candidate | Minimum scope | Baseline | Reject when |
| --- | --- | --- | --- |
| Image drop→ShareX editor/pin | One validated action, no full capture window | Current context menu/ShareX hotkey | More clicks, unintended upload risk, unclear result |
| Restore a familiar scene | Reuse routines and, if useful, an existing Workspaces shortcut | Existing shortcut/routine | Merely another button without combined benefit |
| Quick note→existing Joplin | Selected content/target, one-way, no automatic sync | Direct Joplin capture | Setup/service burden exceeds convenience |

Retain LocalSend as another candidate, but establish Windows CLI availability first. Prioritize software the user already uses, action frequency and a genuinely shorter path, not star count. Do not install all candidates. No candidate enters production without approval.

## 8. Reuse Leo's foundation

[LocalApplications.cs](LocalApplications.cs) already handles programs/arguments/workdir/elevation. [FileDropActions.cs](FileDropActions.cs) appends validated paths. [CustomCombos.cs](CustomCombos.cs)/[ActionExecution.cs](ActionExecution.cs) already implement combinations/execution.

First test whether existing configuration suffices. New adapters should add real application-specific behavior/results, not duplicate settings, retries or a brain. Request interfaces only for demonstrated CLI/API gaps. Do not begin with a universal connector marketplace.

## 9. Admission card for each candidate

1. How does the user do it today, and which step can Leo remove? Observe instead of inventing a baseline.
2. Is the tool already installed? Explain absence; do not install automatically.
3. Which verified release provides the interface, authentication and terms?
4. What data is read and how is output confirmed? Separate launched/accepted/completed/unknown.
5. What are effects, duplicate-trigger, timeout, cancellation and failure rules? Original tools retain their maintenance owner.
6. What processes/polls/listeners remain after disable? Keep paths/keys out of public shares.
7. Does the gain justify complexity compared with existing hotkeys/context menus/Leo routines?

Explicit deterministic actions remain local. Add model understanding only for demonstrated value; it must not be a mandatory layer for opening a tool.

## 10. Roadmap implications and next step

Previous R1–R5 was an external-agent-centered proposal. Automatic progression is paused; it was not approved and is no longer the default priority. See [roadmap status](ROADMAP.en.md).

Select one real frequent scenario, then check existing configuration, run a minimal isolated trial and compare the path. If none is needed, improve current entry/reliability instead of finding features to justify research.

No monopoly conclusion, resource advantage, complete source or license audit is established. Integrations, installation, paid API trials, releases and uploads still need explicit scope and approval.

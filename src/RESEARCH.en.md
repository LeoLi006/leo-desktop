# Renewed research: Leo and mature agent ecosystems

[简体中文](RESEARCH.md) | **English**

Research date:2026-10-22. Leo baseline:21fcaf4; published app:v1.05.0. This review checks primary documentation, selected source and local interfaces. No third-party agent was installed, real key used, comparative benchmark run or full feature set validated. Conclusions are engineering judgments, not performance/security guarantees.

[Proposed roadmap](ROADMAP.en.md) · [Existing architecture](Architecture/README.en.md)

## 1. Question and recommendation

Replace “what else can we add?” with: can Leo reuse an external agent's planning, tools and memory at reasonable cost while retaining convenient, low-dependency, controlled local operation?

Retain the companion, windows and local execution foundation. Evaluate Hermes as the first independent backend candidate. Establish task lifecycle first, then constrained connectivity; expose Leo tools only after proving value. Do not transplant Hermes Desktop or continue window-by-window interface splitting.

These are proposals, not selected dependencies. Defer/reject a backend that merely adds another chat window, complex installation or unacceptable idle cost.

## 2. Evidence and method

| Level | Evidence reviewed | What it establishes |
| --- | --- | --- |
| E1 | Primary docs, specifications and licenses | Intended behavior/public interfaces, not local compatibility |
| E2 | Selected source files | Relevant entry points/fields exist, not complete correctness |
| E3 | Current Leo source | Reusable boundaries, not newly rerun tests |
| Missing | Pinned real integration, personal relay compatibility, resource/use comparison | Requires an approved isolated experiment |

Most external links point to changing main branches/live documentation; no reproducible dependency revision has been selected. Before implementation record version/commit, capabilities, package locks and license. Stars and marketing speed claims are not Leo quality/performance evidence.

## 3. Candidate review

### 3.1 Hermes Desktop / Hermes One: client design reference

It is a community client for local/remote Hermes. Selected entry/composition files separate window, IPC and shutdown ownership. Use it to study task presentation and frontend/backend responsibilities. Sources: [project](https://github.com/fathah/hermes-desktop), [entry](https://github.com/fathah/hermes-desktop/blob/main/src/main/index.ts), [composition](https://github.com/fathah/hermes-desktop/blob/main/src/main/app/start.ts).

Judgment: its Electron/React/TypeScript UI is not a reusable WinForms control. Prefer design lessons or backend integration over replacing the host. The reviewed client license is MIT; source reuse still requires exact-file review and notices. [Dependencies](https://github.com/fathah/hermes-desktop/blob/main/package.json), [license](https://github.com/fathah/hermes-desktop/blob/main/LICENSE).

Not validated: resource use, installation reliability, all tools, or whether a client fix solves Leo resizing.

### 3.2 Hermes Agent: independent run API first

Official integration docs distinguish HTTP, JSON-RPC, ACP and Python embedding. Reviewed API source includes runs/events/approval/stop/capabilities and explicitly marks some administration/memory-write features unavailable. [Integration](https://hermes-agent.nousresearch.com/docs/developer-guide/programmatic-integration), [API source](https://github.com/NousResearch/hermes-agent/blob/main/gateway/platforms/api_server.py).

Judgment: test an HTTP run lifecycle before richer transports. Streaming text alone is insufficient. Discover actual capabilities and validate authentication, ownership and terminal outcomes rather than infer everything from a port/OpenAI compatibility. [Architecture](https://hermes-agent.nousresearch.com/docs/developer-guide/architecture).

Documented approval/execution boundaries do not prove default local isolation. Core license reviewed:MIT; no code/dependency imported. [Security](https://hermes-agent.nousresearch.com/docs/user-guide/security), [license](https://github.com/NousResearch/hermes-agent/blob/main/LICENSE).

Unknowns: relay tool/stream compatibility, stopping, credential isolation, installation burden and demand beyond current four-step plans.

### 3.3 MCP C# SDK: tool bridge candidate

The official .NET SDK offers client/server components with core, hosting and HTTP packages. Hermes documents external MCP tools. [SDK](https://github.com/modelcontextprotocol/csharp-sdk), [Hermes MCP](https://hermes-agent.nousresearch.com/docs/user-guide/features/mcp).

Judgment: potentially expose limited Leo tools to Hermes later; do not first build an arbitrary MCP installer. Start read-only, allowlisted and explicitly scoped. Run protocol still owns progress/approvals. No SDK/package/framework compatibility has been selected or tested.

The reviewed LICENSE describes transition from MIT to Apache-2.0, with separate documentation licensing. Do not assume every file remains MIT; inspect exact package/files at selection. [License](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/LICENSE).

### 3.4 ACP: record, defer implementation

The specification primarily connects editors/IDEs to coding agents, including sessions, tool interactions and cancellation. [Introduction](https://agentclientprotocol.com/get-started/introduction).

Judgment: Leo is not currently an IDE and the first scenario is not code-diff editing. Avoid a third transport unless necessary backend behavior requires it. ACP and MCP serve different boundaries.

### 3.5 PowerToys Command Palette: extension responsibility reference

Official docs describe out-of-process .NET extensions, WinRT and manifest discovery. [Extension model](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extensibility-overview).

Judgment: borrow responsibility separation without converting Leo's existing protocol into COM/WinRT or claiming plugin compatibility.

### 3.6 Flow Launcher and Everything: search-only candidates

Flow provides Windows search/plugin entry points; reviewed license:MIT. Everything offers an SDK. [Flow](https://github.com/Flow-Launcher/Flow.Launcher), [license](https://github.com/Flow-Launcher/Flow.Launcher/blob/dev/LICENSE), [Everything SDK](https://www.voidtools.com/support/everything/sdk/).

Two guessed Flow raw-interface paths returned404, so no plugin signatures were inferred and source-level adaptation remains incomplete. Everything installation, latency and redistribution terms were not validated.

Judgment: evaluate only for a demonstrated Leo search problem, outside the agent pilot. SDK availability does not grant permission to redistribute the application.

## 4. Local foundation versus gaps

| Source | Confirmed foundation | Does not establish |
| --- | --- | --- |
| [ActionContracts.cs](ActionContracts.cs), [ActionExecution.cs](ActionExecution.cs) | Structured results, evidence, cancellation and existing policies | General remote-run protocol or global exactly-once effects |
| [BrainControl.cs](BrainControl.cs), [BrainInterface.cs](BrainInterface.cs) | Explicit catalog, four-step limit, preview/confirmation and existing executor | General agent loop or Hermes session integration |
| [ChatApi.cs](ChatApi.cs), [QuickRouter.cs](QuickRouter.cs) | Relay streaming, local routing and locally encrypted key | Full Hermes approvals/run-state compatibility |
| [ModuleActivities.cs](ModuleActivities.cs), [ModulePresentation.cs](ModulePresentation.cs) | Activity ownership, stale callbacks and fixed window service | OS confinement of arbitrary external processes |
| [ModuleWorker.cs](ModuleWorker.cs) | One-shot exchange, timeout and owned process cleanup | Persistent events, MCP/ACP compatibility or a sandbox |
| [UsageProfiles.cs](UsageProfiles.cs) | Existing capability/profile selection | Need for another parallel mode system |

Verify whether current chat can express runs/approvals, how stop is acknowledged, how external calls reach scoped Leo capabilities, and who owns runs across close/restart. Proposed remote bridges are not completed infrastructure.

## 5. Reuse decisions

| Option | Current decision | Reconsider with |
| --- | --- | --- |
| Retain local commands/stores/UI | Continue | A demonstrated nonlocalizable defect |
| Borrow task UX | Minimal event review first | Reuse current semantics when sufficient |
| Connect independent Hermes | Isolated candidate | Version/capability, benefit and resource evidence |
| Official MCP SDK for Leo tools | Later read-only pilot | A working backend and explicit scope/authorization |
| Copy full desktop client/agent loop | Defer | Clear maintenance/product benefit |
| Workflow engine/memory platform/multi-agent fleet | Defer | A bounded-routine acceptance scenario that cannot work today |

## 6. Experiment and stopping rules

Compare the same fake schedules, module state and local commands through current and candidate brains. Record success evidence, manual steps, readiness time, model requests, cancellation/disconnect handling and host/additional-process resources. Disabled backend must add no agent process/network activity.

Start with trusted fake services for missing capabilities, wrong authentication, duplicate events, stream interruption, approval denial, cancellation and late completion. This is not real Hermes compatibility. A real trial needs separate approval, a pinned version/workspace and no default access to personal files/keys or privileged shells.

An agent's own file/shell tools can bypass a Leo bridge allowlist. Strong isolation depends on its execution environment, not a separate process or “read-only” tool name.

Stop when there is no reduction in steps/task benefit, substantial default overhead, unreliable approval/stop/unknown-state handling, dependence on continuously changing private APIs, or need for unjustified private data access. Existing local capabilities remain usable.

## 7. Scope and maintenance

This research adds no code/dependency, changes no provider settings/application and uploads nothing. Follow the [roadmap](ROADMAP.en.md) with separate approvals. First improve existing task semantics/presentation, not business feature count.

Record exact versions, source links, compatibility and focused evidence during implementation. Recheck moving upstream references. Keep documentation claims, inspected source and measured results distinct.

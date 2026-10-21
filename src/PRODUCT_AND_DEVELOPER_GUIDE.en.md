# Product and developer guide — English

[简体中文](PRODUCT_AND_DEVELOPER_GUIDE.md) | **English**

This reference describes main through source commit `42fea41`. The release version is `1.05.0`, consolidating implementation through the stated commit and subsequent packaging revisions. Use documentation and source from the same commit.

## 1. Product and requirements

Leo is a standalone Windows x64 desktop companion, launcher and local action hub. Optional modules provide capabilities; the host validates requests, owns resources and reports evidence. A separately configured model handles open-ended conversation and bounded planning. Local functionality does not require an API key.

The UI uses .NET 9 WinForms with selected Windows/WPF integrations. Development requires Windows, PowerShell, Git and the SDK specified in `global.json`. Framework-dependent builds need the matching Windows desktop runtime. External applications, network sources and local speech models are configured separately.

Internal interfaces are not a stable external SDK. Built-in modules are shipped with the host; disabling a module does not uninstall or close its external application.

## 2. Features and dependencies

| Capability | Existing behavior and boundary |
| --- | --- |
| Companion and appearance | Editable name/size, multi-monitor docking, hover reveal and fullscreen hiding. Barcelona default, Argentina, Spain, Inter Miami, Manchester City and custom skins; windows follow the theme. Some auxiliary windows have fixed sizes. |
| Launcher and files | Name/pinyin/alias matching, recent apps, pinned ordering, keyboard navigation, path completion, drag/drop choices, custom EXE/LNK arguments, working directory and administrator launch. An EXE entry grants launch behavior only. |
| Planner and notes | Daily/weekly/overdue views, calendar, local natural input, recurrence/exceptions, rescheduling/undo, reminders/snooze/quiet hours and backups. Quick notes support search, pins and conversion to tasks. Reminders require Leo to be running. |
| Clipboard | History, pins, expiration and sensitive-content protection. Listening can be disabled; detection is not a guarantee that every secret is recognized. Clipboard text is not sent to a model by default. |
| Football | Configurable club/national-team following, schedules/results, team fixtures and standings, cached fallback, watch links and match-to-task reminders. Dedicated network sources can fail or change; cached data is not necessarily current. |
| Music and media | QQ Music single-song search/request, playback controls and lyrics shortcut; PotPlayer and Windows media sessions. Client state, permissions and media availability still apply. Playlist synchronization is not provided. |
| Application adapters | Optional Clash system proxy, terminal, Edge and custom programs. Launch/wait/execute/verify follows each adapter's existing policy. Accepted requests are not automatically confirmed effects. |
| Computer status and mail | Optional read-only CPU/memory/network/disk and process view, Task Manager shortcut and optional resource alerts. Huorong personal is a launch shortcut, not a security-state integration. Mail opens an existing client or QQ/Gmail website: no mailbox credentials, email retrieval or unread monitoring. |
| Chat and speech | Streaming chat, local commands, controlled tool plans and cancellation; local SenseVoice recognition requires its model and microphone. UI language does not change recognition or model reply language. |
| Management | Sequential custom routines, usage profiles, module status, runtime checks, quiet logs, focus and update checks. Passive dependency readiness does not prove a client is operational. |

Local commands, application launches, explicit task/note operations and fixed routines avoid model calls. Football and song queries use their own network sources. Free conversation, ambiguous intent and temporary planning use the user's configured model endpoint.

## 3. Host structure

| Layer | Source and responsibility |
| --- | --- |
| Composition | `Program.cs`, `Settings.cs`: create services/modules, bind entries, apply settings, dispose resources. Explicit composition remains necessary. |
| Registry and execution | `CapabilityRegistry.cs`, `ActionExecution.cs`, `ActionContracts.cs`: identity, validation, deadlines, cancellation, action-specific merging/serialization and results. |
| Contracts | `ModuleContracts.cs`, `ModuleContracts.Additional.cs`: descriptions aligned with real adapters, policies, dependencies and result evidence. Descriptions are not full JSON Schema. |
| UI contributions | `EntryContributions.cs`, `ModuleEntryContributions.cs`, `ModuleEntryRendering.cs`: data-only search/menu/button descriptors and identity-checked clicks. Hiding an entry does not disable its capability. |
| Scoped services | `ApplicationHostServices.cs`, `ModuleHostServices.cs`, `ModulePresentation.cs`, `ModuleThemeService.cs`: limited configuration/dependencies, fixed window requests, structured feedback and read-only theme subscriptions. |
| Ownership and storage | `ModuleActivities.cs`, `IPeriodicTask.cs`, `AtomicJson.cs` and existing stores: cancellation, generations, owned periodic work, atomic file replacement and recovery. No global multi-store transaction is claimed. |
| Delivery | `ApplicationPackages.cs`, `ExtensionCapability.cs`, `ModuleWorker.cs`, `ModuleStatus.Contracts.cs`: existing package formats, compatibility, rollback, worker lifecycle and passive status. |

UI, speech and routine entries reuse the same execution path. Modules should not duplicate persistence, client startup or retry logic. See the [architecture reference](Architecture/README.en.md) for composition and data boundaries.

## 4. Requests, results and interfaces

Three version spaces are distinct:

| Identifier | Meaning |
| --- | --- |
| `LeoModule/1` | Existing external manifest and one-shot stdio worker protocol. |
| `leo-action/1` | Internal structured action envelope, not an HTTP endpoint or unrestricted worker RPC. |
| `leo-host-contract/1` | Internal module/action/dependency metadata, not a stable third-party SDK. |

`ActionRequest` retains a legacy string `Value`. `StructuredActionRequest` contains `Protocol`, `ActionId`, object `Parameters`, `Source` and optional `RequestId`, `TraceId`, `Deadline`. Each adapter validates allowed fields and real targets. A local application request uses the configured application identity, not caller-supplied path or privilege overrides:

```json
{"Protocol":"leo-action/1","ActionId":"local:0123456789abcdef0123456789abcdef","Parameters":{"applicationId":"0123456789abcdef0123456789abcdef"},"Source":"ui"}
```

Submit through `ActionDispatcher.ExecuteStructuredAsync`. Legacy `local_app_open` maps to the same executor.

`ActionResult` carries request/trace/source/action/module/execution identities, `Status`, `Message`, `ErrorCode`, `Data`, `Evidence` and `Retryable`. Common statuses are succeeded/failed/cancelled; routines additionally track steps and unknown outcomes. `snapshot`/`cached` means a query result, `accepted` means a request was accepted, `committed` means the corresponding local write completed, `window-shown`/`state-observed` records an observed effect. `unknown` must not be presented as success or blindly replayed. Cancellation after commit cannot undo the write. In-flight merging is not a permanent deduplication ledger.

`ActionPayload.PrivateData` and diagnostic summaries separate private results from shareable diagnostics. Never log result bodies by default.

| Internal interface | Purpose |
| --- | --- |
| `ILeoModule` | Stable identity, version, enablement, actions and legacy execution. |
| `IStructuredLeoModule` | Execution policy, configuration generation, limits, structured execution and failure mapping. |
| `IActionRequestAdapter` | Strict conversion of permitted structured parameters to the existing request. |
| `IModuleContractSource` | Module description and passive dependency states: ready/missing/unknown/degraded. |
| `IModuleEntryContributor` | Data-only entry descriptions; no arbitrary controls/delegates. |
| `IBrainContributor` | Explicit model exposure. Basic registration or an installed extension does not automatically grant model access. |
| Cancellation/disposal | The host identifies ownership; modules release their subscriptions, streams, timers and helper processes. |

## 5. Choose an extension path

1. **Local application:** configure an EXE/LNK, icon, arguments, working directory and optional administrator launch; no source changes. Share only a data-only application package, with recipient-local path binding.
2. **Declarative extension:** a `LeoModule/1` JSON manifest wraps existing permitted actions with titles and phrases.
3. **Worker extension:** a `LeoModule/1` ZIP contains `module.json` and a root worker EXE, invoked only on user action via a one-request stdio exchange.
4. **Trusted internal module:** modify/rebuild the host for deep service, structured data or window integration. Explicit composition and lifecycle wiring are required.
5. **Appearance/profile:** `.leoskin` shares clothing/theme; usage profiles share selected behavior/layout. Neither format implements a capability.

For complete external field examples, limits, compatibility and rollback rules, read [MODULES.md](MODULES.en.md). No arbitrary DLL drop-in loading or automatic dependency downloads are provided. Worker responses are data, not host commands.

## 6. Internal module integration

Start with [MinimalModule.cs](Architecture/Contracts/MinimalModule.cs) and [ScopedNotesExample.cs](Architecture/C04/ScopedNotesExample.cs). Samples are opt-in test examples, not production registrations.

1. Define stable module/action IDs, input/output, effects, dependencies, timeout, serialization/merging and data ownership.
2. Inject only scoped services. Do not pass all `Preferences`, an arbitrary dispatcher, a raw data path or host `Form` when unnecessary.
3. Implement strict adaptation, structured execution and contract metadata; reject unknown fields and invalid real targets. Keep configuration reads inside the execution error boundary.
4. Register the instance through the existing registry; descriptions must match its real actions and policies.
5. For search/menu/buttons, implement data-only contributions with owned IDs and current parameters. The host binds `EntryContributions.Bind(prefs, registry, dispatcher)` and explicitly refreshes the launcher. Settings drafts use a read-only binding.
6. On disable, update configuration, cancel disabled work and refresh entries. On removal, unregister the same instance, cancel, dispose host-owned services and refresh. Saved data is not implicitly deleted.
7. Add model exposure only through explicit brain contribution and permissions. New complex windows need fixed presentation ports and host construction; an entry descriptor cannot create arbitrary controls.
8. Verify valid/invalid input, duplicates, disable/remove/stale clicks, cancel/timeout, save failure, service failure, disposal and old data compatibility using isolated fixtures.

Read-only snapshots must not mutate backing data. Edit drafts are not formal commits. Save first, publish the committed snapshot second. Closing a window is distinct from disabling a module: reminders/listeners that should continue remain host-owned.

## 7. Routines and controlled brain

Existing routines execute sequential steps with timeouts, cancellation, result references and recovery checks. Preserve completed steps, and verify unknown external effects before retrying. Parallel graphs, arbitrary scripts and general compensation are not delivered.

The brain derives permitted tools from explicit contributions and current state. Temporary plans contain at most four steps; host validation checks parameters, real IDs, configuration fingerprints and budgets, with preview/confirmation for effects. Ambiguous input asks for clarification. Actual query results supply match/song/task IDs. Reading notes/tasks requires explicit user intent; clipboard text and private session handles are not supplied by default. External content must not become trusted execution instructions.

## 8. Data, sharing and recovery

Runtime directories contain private settings, keys, tasks, notes, clipboard, logs, models and extension state. Do not upload them. Ignore rules do not sanitize already-tracked files.

| Format | Shareable content | Excluded by default |
| --- | --- | --- |
| `LeoApplicationPackage`, protocol 1 | Eight fixed metadata fields: Format/Protocol/Id/Version/Name/HostMin/HostMax/Dependencies. | Local paths, arguments, working directory, admin authorization, executable and business data. |
| `LeoSkin/1` | Public name, palette/motto/options, OriginalKit/HasBadge, jersey and optional badge. | Local identity/paths, pet name, planner quotes, accounts and business data. |
| `leo-profile-1` | Profile identity/name, module/entry choices, order, supported background settings and shortcuts. | Program paths/arguments/icons, API config, chat, clipboard, tasks and notes. |

Review user-supplied public names, slogans and image content yourself. Exports are not private backups. Application import produces a draft until settings save; profile import adds to the library and switching is separately previewed. Successful skin import adds a library item; cancelling the later editor does not undo that completed import.

Atomic replacement and backups protect individual files. There is no cross-file/process/power-loss transaction guarantee. Update promotion attempts rollback; if rollback also fails, preserve recovery material and report failure. If the new version is active but old-directory cleanup failed, report a warning instead of blindly replaying installation. Extension removal retains its data directory.

## 9. Build, validation and contribution

From the repository root:

```powershell
./scripts/Build.ps1 -Locked
./.build/Release/LeoLauncher.exe
```

The build checks pinned dependencies and produces `.build/Release`. Keep development data isolated from a real installation; do not mix framework-dependent output with a self-contained release. Packaging and publishing are separate operations.

Use only directly relevant checks, such as `src/Test-ExtensionContracts.ps1`, `src/Test-ExtensionPackages.ps1`, `src/Test-ModuleStatusContracts.ps1`, `src/Test-SharingBoundaries.ps1` and `src/Test-DeliveryRecovery.ps1`; inspect scripts before invoking unfamiliar flags. Reports belong in ignored `.build`. See [verification notes](PERFORMANCE.en.md) and [localization requirements](Localization/README.en.md).

A contribution should explain capability/data/lifecycle impact, preserve old IDs and formats, validate failures and cancellation, attach relevant evidence and disclose limitations. Do not commit credentials, recordings, personal records, models, binaries or temporary reports. Check current repository licensing and third-party notices before redistribution; public source does not automatically license team imagery, trademarks or models.

## 10. Troubleshooting and current limits

Import failures: check exact manifest casing, IDs, numeric versions/ranges, dependencies, archive layout and size/type limits. Missing entries: check enablement, display mode, isolation diagnostics and explicit refresh. Unknown dependency status is not success. Worker failures: test runtime requirements, single-line stdout, exit code, output limits and timeout in isolation. Cancellation after an external submission can leave an actual effect; inspect evidence before retrying.

The delivered subset includes unified execution, internal entries/services and the existing package recovery path. It does not include independent physical delivery of every built-in module, arbitrary-code sandbox/signature trust, full protocol negotiation, all complex windows/settings as descriptors, a global request ledger, general parallel compensation or completed long-term real-world qualification. Short tests cannot prove multi-day leak freedom.

Issue reports should include commit/version, Windows environment, reproduction, expected/actual behavior and sanitized error types. Never attach a whole personal runtime directory.

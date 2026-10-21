# Modules and extension protocol

[简体中文](MODULES.md) | **English**

Applies to main 1.04.0. The public package protocol is `LeoModule/1`; internal `leo-action/1` and `leo-host-contract/1` are not unrestricted worker APIs.

[Product and developer guide](PRODUCT_AND_DEVELOPER_GUIDE.en.md) · [Architecture](Architecture/README.en.md)

## Choose a format

| Type | Purpose and limits |
| --- | --- |
| Local app | Configure EXE/LNK, name, built-in/custom icon, arguments, working directory and elevation; launch only |
| Data-only app package | Share metadata; recipients supply local paths |
| Declarative JSON | Wrap existing permitted actions and fixed values |
| Worker ZIP | Your program via a one-shot request/response exchange |
| Internal C# | Modify/rebuild the host with scoped services; not a stable external SDK |

Adding/disabling/removing does not uninstall or close user applications. Hidden entries do not disable capabilities; stale clicks revalidate identity. Installation does not automatically grant model access.

## Declarative manifest

Use UTF-8 JSON with exact field casing; import through the existing extension manager in Settings.

```json
{
  "Format": "LeoModule/1",
  "Id": "daily-plan",
  "Name": "Daily plan",
  "Version": "1.0.0",
  "Runtime": "actions",
  "HostMin": "1.4.0",
  "Dependencies": ["windows", "leo-module/1"],
  "Commands": [
    {"Id":"today","Title":"Open today's plan","Phrases":["open daily plan"],"Action":"todos_open","Value":""}
  ]
}
```

| Field | Rule |
| --- | --- |
| Id | Module 2–40, command 1–40 characters; initial lowercase letter, then lowercase letters/digits/hyphens |
| Name / Commands | Name up to 40 characters,1–16 commands |
| Phrases / Value | Each phrase up to 60 characters; Value string up to 4000; complex values follow the action's JSON-string adapter |
| HostMin / HostMax | Optional inclusive numeric bounds; omission adds no range; not SemVer expressions |
| Dependencies | Passive checks for the listed tokens and module:<extension-ID> |

Choose Action from actual [LeoActions.cs](LeoActions.cs), respecting private handles and confirmation requirements. Recursive routines, text redispatch and extension recursion are forbidden. Fixed declarative values cannot be arbitrarily overridden with nonempty input. Unknown/missing/cyclic/disabled dependencies are not downloaded automatically. A fixed local_app_open also checks the real application's state, path and working directory.

The action identity is `ext:daily-plan:today`; the contribution identity is `ext:daily-plan/today`. They are distinct. Default visibility is search-only; existing entry settings can add menu/shortcut visibility.

## Worker process

Place module.json and worker.exe at the ZIP root; dependencies may use supported relative paths.

```json
{
  "Format":"LeoModule/1","Id":"hello-worker","Name":"Hello worker","Version":"1.0.0",
  "Runtime":"stdio","Executable":"worker.exe","TimeoutSeconds":15,
  "Commands":[{"Id":"ping","Title":"Ping worker","Phrases":["ping worker"],"Action":"worker:ping","Value":""}]
}
```

On user action, Leo starts the program with current-user privileges and `--leo-module`, writes one UTF-8 JSON line to stdin and closes input. Request shape (path is illustrative):

```json
{"protocol":"LeoModule/1","requestId":"generated-id","command":"ping","value":"","dataDirectory":"<host-assigned-local-directory>","origin":"search","theme":{"skin":"83","name":"Leo","accent":"#E7C277","background":"#0B1221","text":"#EDF2FA","radius":14,"opacity":100}}
```

command excludes the worker: prefix. Read, validate, perform bounded work, write exactly one response line to stdout and exit normally. Send logs to stderr only.

```json
{"ok":true,"message":"Operation completed"}
```

Use ok:false for failure. Nonzero exit, no response and invalid JSON fail. Timeout defaults to 15 seconds, range 1–120; response line up to 64KB, message up to 4000 characters. Cancellation/timeout reclaims owned worker processes, but does not undo external effects. Store private state in dataDirectory, not the package. This directory is not a sandbox.

[WorkerFixture.cs.txt](Architecture/Extensions/WorkerFixture.cs.txt) is a test fixture, not a production template. Each call is a one-shot process exchange, not a persistent session. Responses are data, never host commands.

## Archive, update and recovery

- Maximum64MB archive,256MB/2000 extracted files; only exe/dll/json/png/txt/md/dat/bin, root EXE. Reject traversal, absolute/duplicate paths, links and scripts.
- Framework-dependent workers need an installed runtime; no automatic downloads. Workers have user privileges; third-party DLLs are not loaded into Leo's process.
- Same-ID updates require a higher numeric version; running/queued modules cannot be replaced or removed. Validate staging before promotion; attempt rollback on failure and preserve material if recovery also fails.
- Cleanup failure after promotion is a warning, not grounds for blind replay. Removal retains `module-data/<id>`; programs live in `modules/<id>`.
- Data-only app exports contain exactly these eight fields; no executable, local paths/arguments, elevation grant or business records.

## Calling and acceptance

Search `:ext:hello-worker:ping` or a registered title/phrase; chat/speech follow explicit-command routing and routines can use the existing unified-command step. Compatibility module_list/module_run calls remain constrained by current enablement, validation and confirmation; they do not grant automatic model permission.

Relevant scripts in src cover extension contracts/packages, module status, sharing and joint delivery recovery. Use isolated data and trusted fixture workers rather than personal keys or third-party executables.

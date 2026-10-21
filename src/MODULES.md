# 模块与扩展协议 / Modules and extension protocol

适用主分支1.04.0。公开包协议为`LeoModule/1`；内部`leo-action/1`与`leo-host-contract/1`不是worker可自由调用的服务。
Applies to main1.04.0. The public package protocol is `LeoModule/1`; internal `leo-action/1` and `leo-host-contract/1` are not unrestricted worker APIs.

[产品与开发指南 / Product and developer guide](PRODUCT_AND_DEVELOPER_GUIDE.md) · [架构 / Architecture](Architecture/README.md)

## 选择方式 / Choose a format

| 方式 / Type | 用途与限制 / Purpose and limits |
| --- | --- |
| 本机应用 / Local app | EXE/LNK、名称、18种内置或自定义图标、参数、工作目录、管理员启动；只获得启动能力 / Configure EXE/LNK, name, built-in/custom icon, arguments, working directory and elevation; launch only |
| 数据型应用包 / Data-only app package | 分享元数据，接收者补本机路径 / Share metadata; recipients supply local paths |
| 声明式JSON / Declarative JSON | 包装已有公开动作及固定值 / Wrap existing permitted actions and fixed values |
| 独立进程ZIP / Worker ZIP | 一次请求、一次响应的自己的程序 / Your program via a one-shot request/response exchange |
| 内部C# / Internal C# | 修改宿主、限定服务与重新编译；不是稳定外部SDK / Modify/rebuild the host with scoped services; not a stable external SDK |

添加/停用/移除不会自动卸载或退出用户软件。入口隐藏不等于停用能力；旧入口点击仍须验证当前身份。扩展默认不因安装就获得模型权限。
Adding/disabling/removing does not uninstall or close user applications. Hidden entries do not disable capabilities; stale clicks revalidate identity. Installation does not automatically grant model access.

## 声明式清单 / Declarative manifest

UTF-8 JSON，字段大小写如下。在设置的扩展管理窗口导入。
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

| 字段 / Field | 规则 / Rule |
| --- | --- |
| Id | 模块2–40字符、命令1–40字符；小写字母开头，随后字母/数字/连字符 / Module2–40, command1–40 characters; initial lowercase letter, then lowercase letters/digits/hyphens |
| Name / Commands | 名称最多40字符，每包1–16命令 / Name up to40 characters,1–16 commands |
| Phrases / Value | 每条口令最多60字符；Value字符串最多4000，复杂参数使用对应动作的JSON字符串 / Each phrase up to60 characters; Value string up to4000; complex values follow the action's JSON-string adapter |
| HostMin / HostMax | 可选数字版本闭区间，省略无额外限制；不是SemVer表达式 / Optional inclusive numeric bounds; omission adds no range; not SemVer expressions |
| Dependencies | windows、leo-module/1、leo-host-contract/1、stdio/v1、module:<扩展ID>；只被动核对 / Passive checks for the listed tokens and module:<extension-ID> |

Action取自实际[LeoActions.cs](LeoActions.cs)，但内部私有句柄或需要额外确认的动作不因名字存在就可用。禁止递归组合、再分派文本或扩展递归。固定声明值不可用非空参数任意覆盖。未知、缺失、循环或停用依赖阻止相应调用，不自动下载。固定local_app_open同时核对真实应用、路径与工作目录。
Choose Action from actual [LeoActions.cs](LeoActions.cs), respecting private handles and confirmation requirements. Recursive routines, text redispatch and extension recursion are forbidden. Fixed declarative values cannot be arbitrarily overridden with nonempty input. Unknown/missing/cyclic/disabled dependencies are not downloaded automatically. A fixed local_app_open also checks the real application's state, path and working directory.

动作身份为`ext:daily-plan:today`，贡献身份为`ext:daily-plan/today`，不可混用。默认仅搜索，可在原入口设置选择菜单/快捷栏。
The action identity is `ext:daily-plan:today`; the contribution identity is `ext:daily-plan/today`. They are distinct. Default visibility is search-only; existing entry settings can add menu/shortcut visibility.

## 独立进程 / Worker process

ZIP根目录包含module.json和worker.exe；依赖保留相对目录。清单如下：
Place module.json and worker.exe at the ZIP root; dependencies may use supported relative paths.

```json
{
  "Format":"LeoModule/1","Id":"hello-worker","Name":"Hello worker","Version":"1.0.0",
  "Runtime":"stdio","Executable":"worker.exe","TimeoutSeconds":15,
  "Commands":[{"Id":"ping","Title":"Ping worker","Phrases":["ping worker"],"Action":"worker:ping","Value":""}]
}
```

用户触发后，Leo以当前用户权限启动程序，附加`--leo-module`；stdin写一行UTF-8 JSON并关闭。请求形状（路径仅为占位说明）：
On user action, Leo starts the program with current-user privileges and `--leo-module`, writes one UTF-8 JSON line to stdin and closes input. Request shape (path is illustrative):

```json
{"protocol":"LeoModule/1","requestId":"generated-id","command":"ping","value":"","dataDirectory":"<host-assigned-local-directory>","origin":"search","theme":{"skin":"83","name":"Leo","accent":"#E7C277","background":"#0B1221","text":"#EDF2FA","radius":14,"opacity":100}}
```

command不含worker:前缀。读取→校验协议/命令/参数→执行有界操作→stdout仅输出一行响应→正常退出；日志只能写stderr。最小响应：
command excludes the worker: prefix. Read, validate, perform bounded work, write exactly one response line to stdout and exit normally. Send logs to stderr only.

```json
{"ok":true,"message":"Operation completed"}
```

失败用ok:false；非零退出、无响应、坏格式均失败。默认15秒，可设1–120秒；响应行最多64KB，message最多4000字符。取消/超时结束宿主拥有的worker及子进程，不撤销已经发生的外部效果。私有状态使用dataDirectory，不放包目录；该目录不是权限沙箱。
Use ok:false for failure. Nonzero exit, no response and invalid JSON fail. Timeout defaults to15 seconds, range1–120; response line up to64KB, message up to4000 characters. Cancellation/timeout reclaims owned worker processes, but does not undo external effects. Store private state in dataDirectory, not the package. This directory is not a sandbox.

[WorkerFixture.cs.txt](Architecture/Extensions/WorkerFixture.cs.txt)为仓库测试worker，不是生产模板。每次调用是独立进程协议，不是常驻会话；回复只当数据，不执行为宿主命令。
[WorkerFixture.cs.txt](Architecture/Extensions/WorkerFixture.cs.txt) is a test fixture, not a production template. Each call is a one-shot process exchange, not a persistent session. Responses are data, never host commands.

## ZIP、更新与恢复 / Archive, update and recovery

- 压缩最多64MB，展开最多256MB/2000文件；只允许exe/dll/json/png/txt/md/dat/bin；根EXE。拒绝越界、绝对/重复路径、链接及脚本。Maximum64MB archive,256MB/2000 extracted files; only exe/dll/json/png/txt/md/dat/bin, root EXE. Reject traversal, absolute/duplicate paths, links and scripts.
- 框架依赖worker需要用户已有运行时；不自动下载。程序权限等同用户，不加载第三方DLL到Leo主进程。Framework-dependent workers need an installed runtime; no automatic downloads. Workers have user privileges; third-party DLLs are not loaded into Leo's process.
- 同Id更新必须提高数字版本；运行/排队时拒绝更新和移除。暂存校验后提升，失败尝试恢复旧版；恢复也失败保留材料。Same-ID updates require a higher numeric version; running/queued modules cannot be replaced or removed. Validate staging before promotion; attempt rollback on failure and preserve material if recovery also fails.
- 新版已生效但旧目录清理失败属于警告，不盲目重放。移除保留`module-data/<id>`；程序位于`modules/<id>`。Cleanup failure after promotion is a warning, not grounds for blind replay. Removal retains `module-data/<id>`; programs live in `modules/<id>`.
- 共享应用包严格八字段：Format/Protocol/Id/Version/Name/HostMin/HostMax/Dependencies。不夹带程序、路径、参数、管理员许可或业务记录。Data-only app exports contain exactly these eight fields; no executable, local paths/arguments, elevation grant or business records.

## 调用与验收 / Calling and acceptance

搜索`:ext:hello-worker:ping`或登记的标题/口令；聊天/语音沿明确指令路由，自定义组合可用统一指令。旧module_list/module_run兼容动作仍受当前启用、参数和确认限制，不是自动模型授权。
Search `:ext:hello-worker:ping` or a registered title/phrase; chat/speech follow explicit-command routing and routines can use the existing unified-command step. Compatibility module_list/module_run calls remain constrained by current enablement, validation and confirmation; they do not grant automatic model permission.

相关脚本：Test-ExtensionContracts.ps1、Test-ExtensionPackages.ps1、Test-ModuleStatusContracts.ps1、Test-SharingBoundaries.ps1、Test-DeliveryRecovery.ps1（均在src）。只用隔离数据和可信测试worker验证，不用个人Key和第三方程序做默认测试。
Relevant scripts in src cover extension contracts/packages, module status, sharing and joint delivery recovery. Use isolated data and trusted fixture workers rather than personal keys or third-party executables.

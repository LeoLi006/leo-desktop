# Q1 能力契约草案 0：先描述，再迁移

**草案，不是已发布 API。** 当前运行仍使用 ILeoModule、ActionRequest 和 LeoModule/1。本目录 JSON 只被离线验证器读取，不会被模块管理器安装或执行。Q2 开工前还需审批最小子集。

## 1. 一个模块的统一形状

`examples.json` 给出应用、清单、足球、媒体四个完整样例。字段约定：

| 字段 | 含义与约束 |
| --- | --- |
| format / moduleVersion | 草案格式固定 leo-capability-draft/0；模块数字版本独立于宿主版本。正式发布格式待定。 |
| id / name | 模块唯一稳定 ID、显示名；显示名/Leo 改名不得改变 ID。示例的 app/planner 是拟议责任域，不改当前 core 归属。 |
| hostRange / protocol | 支持范围显式描述，Q1 用 draft-only，不能冒充支持任意历史宿主。正式版语义与协商算法待 Q2 固定。 |
| runtime / dependencies | builtin-adapter 或 worker；声明 required/optional 的服务或外部应用。依赖检测必须无副作用，缺失时禁用相关动作而非整个宿主。 |
| enablement / configVersion / dataOwner | host-controlled；配置版本、状态归属独立于程序包。服务凭证只由宿主保存，不放在描述文件。 |
| actions | 一个动作一个稳定 ID；必须声明 input、output、timeoutMs、effect、idempotency、concurrency、cancel、retry、compensation、evidence。 |
| contributions | 只描述 kind/actionId/title/when，宿主决定快捷栏、菜单、搜索与设置呈现。可完全为空，因此无 UI 的后台模块也成立。 |

作用域中 `idempotent`、`share-read` 是**拟议执行要求**，不能直接套用到当前 ActionDispatcher；旧系统尚未按这些字段调度。

## 2. 输入、结果与证据

参数改为 JSON 对象，不再把 JSON 包进 value 字符串。最小 schema 子集为 object/string/integer/boolean/array、properties、required、additionalProperties、enum、items、format；日期/日期时间分别校验。草案验证器只实现此子集，未知 schema 关键字拒绝，不声称实现完整 JSON Schema。

请求信封拟议字段：

```json
{"protocol":"leo-action-draft/0","requestId":"fixture-request-1","traceId":"fixture-trace-1","parentRequestId":null,"source":"search","actionId":"app_open","parameters":{"app_id":"fixture-app"},"deadline":"2030-01-01T12:00:10Z"}
```

- requestId 标记一次请求；traceId 串联组合/模型工具链；parentRequestId 可空。source 来自宿主登记入口，不能作为身份授权证明。
- deadline 为带时区绝对时间，宿主将其与动作默认超时取更早者。CancellationToken 是宿主内部实现，进程外要用协议取消/终止，不序列化 token。
- 数据引用只传明确 ID/字段；外部输入不可获得任意宿主服务。请求上下文不得默认携带剪贴板、窗口标题、全文历史、密钥。

结果信封拟议字段：

```json
{"requestId":"fixture-request-1","status":"succeeded","data":{"app_id":"fixture-app","started":true},"error":null,"evidence":{"level":"accepted","kind":"process-start-returned"},"retryable":false}
```

status 必须为 succeeded/failed/cancelled/skipped/partial/needs-input。失败有稳定 error.code 和用户可读 message；needs-input 包含精确候选 ID，不让模型猜。partial 包含已完成/未完成步骤，不能靠一段文本让组合猜后续是否执行。

证据级别：confirmed（从目标状态核对）、accepted（系统/客户端已接收）、unknown（旧实现没有可核对结果）。**Process.Start 返回不证明界面已就绪，发歌不证明已播放。** 对失败/取消不写成功证据。取消只停止后续动作，不自动补偿已产生副作用。

## 3. 调度语义要分开

- effect：none / ui / local-write / external。只读足球查询可以持缓存，但必须显式描述；app_search 可能打开窗口，不能只据旧 ReadOnly 判断没有 UI 副作用。
- idempotency：idempotent / non-idempotent。设置代理目标值与 toggle 是两种动作；todo_add、打开应用、下一首不得因网络重试或连续点击被重复执行。
- concurrency：resource 为逻辑资源名；policy 为 share-read / serial / independent。共享只读任务的单个调用者取消，只解除该订阅；全部订阅取消或模块停用才取消共享工作。写动作默认串行，是否合并须明确。
- timeout/cancel：窗口任务、模块任务、宿主任务分别归属；过期回调只清理，不改新窗口或新配置。停用模块先拒绝新请求，再有界等待/取消任务；反复启停必须安全。
- retry：none / transient-read-only；有副作用的动作默认不自动重试。用户重试作为新 requestId，保留 trace 关联。补偿是显式动作或 none，不能假称系统级撤销。

## 4. 宿主服务边界

| 服务 | 最小职责 | 不允许混入 |
| --- | --- | --- |
| Configuration / Data | 每模块命名空间、配置版本、原子读写、验证和恢复；共享数据经对应领域动作访问 | 任意访问其他模块目录、自动共享 API Key |
| Diagnostics / Feedback | requestId/traceId、阶段、耗时、错误码；正常成功安静记日志，必要错误/用户选择才提示 | 默认保存完整输入、网址、路径、聊天内容或凭证 |
| Theme / Windows | 主题 token、窗口定位/焦点/Esc、订阅释放；UI 在宿主 UI 线程 | 模块直接全局修改 TopMost、热键、所有控件；跨进程传控件实例 |
| Dependencies | 被动路径与版本检测、用户确认修复 | 检查状态时悄悄启动软件/切代理/播放音乐 |
| Actions / Scheduler | 按能力目录校验、归属、取消、排队和回执；模块受控调用其他动作 | 绕过启停与确认、无限递归、模块自建第二套执行队列 |

宿主内部接口先保证最小闭环；worker 不自动获得进程内接口。独立模块仍有当前用户权限，不能把服务列表当安全沙箱。是否接入现有生态协议，沿用 RESEARCH.md 的已有调研，Q1 未引入新依赖或复制第三方代码。

## 5. 界面贡献与配置

快捷按钮、搜索候选、菜单项、设置入口均指向 actionId；启停由能力状态决定，入口位置由 EntryDisplayModes 决定。主题、名称、图标使用宿主 token，缺失图标可降级。复杂窗口只登记本地 view-provider，独立进程自己提供窗口；复杂设置不强制变成通用字段表。

贡献必须可去重、可撤销；模块停用同步移除/禁用贡献并取消拥有的活动，不能留下“点得到但无响应”的残项。没有 contributions 的模块仍能被组合或大脑调用。

## 6. 版本、未知字段与旧协议

- 顶层描述未知字段、未知运行类型、未知动作 schema 类型直接拒绝，并返回可定位错误；不悄悄安装部分内容。未来可选扩展只进入显式 extensions 命名空间并经协商。
- 动作输入默认 additionalProperties=false；unknown 参数与错误类型明确失败。结果按照调用方声明的版本校验，兼容性改变升级协议主版本。
- 本批不改 LeoModule/1。未来 v1 adapter 保留其字段和值限制，v1 string message 只能提供 legacy 证据；新信封要由支持新版本的 worker 协商后使用。
- 现有 ModuleCatalog.Owner、ReadOnly、合并键和 ClientOperationHub 不能直接作为新契约实现。适配应复用旧服务，只替换一个分派入口，避免两套执行策略。

## 7. Q2 前必须拍板

1. 最小契约先支持哪些字段，哪些只作为记录而暂不参与调度。
2. 宿主/协议版本范围算法、校验错误码、结果大小与取消期限。
3. 窗口打开这种 UI effect 是否允许无 UI 调用方，以及 needs-input 选择如何续接请求。
4. legacy 返回文本的成功含义，和真实副作用确认的边界。

离线验证见 `q1_audit.py`：检查四类描述、输入/结果示例、未知字段/版本、错误类型、无 UI 模块、重复 ID、错误/取消与证据一致性；报告不代表已经实现新运行时。

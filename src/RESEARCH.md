# Leo 桌面控制中枢：相关工作与复用调研

调研日期：2026-10-19。代码基线：v1.02.0（9990b4a）。状态：第一轮公开资料与源码审阅，尚未进行第三方组件接入或性能对比实验。

## 1. 结论与研究问题

Leo 应保留自己的桌宠、轻量窗口、主题和本地动作入口，优先复用搜索、扩展通信、可靠执行和安装更新等成熟基础能力。下一轮不宜继续按功能清单堆叠，也不宜整体换框架。

本轮围绕四个问题选择资料：

1. 如何让应用、文件、便签和动作共用搜索，却保留直接打开应用的速度？
2. 如何让可选模块独立演进，并把故障限制在模块内？
3. 如何把多步组合与模型工具调用建立在同一个可取消、可观察的动作系统上？
4. 如何让普通下载者可靠安装、升级并保留个人配置？

阅读范围包括官方仓库、具体接口源码、官方文档、许可证和两篇人机交互论文。未依据星数评判适用性，也未把项目宣传中的性能描述当作 Leo 的实验结果。以下“建议、成本、优先级”均是针对当前 Leo 的工程判断。

## 2. 已有实现与需要补齐的部分

Leo 已有 `ActionDispatcher`、`ActionRequest`、`ILeoModule`，具备相同请求合并、调用递归限制、取消、模块状态和操作回执；另有 `ModuleWorker`、应用模块、扩展清单、自定义组合和本地语句识别。因此不能再造第二套平行动作系统。

v1.02.0 新增的 `FeatureFaults` 能隔离普通托管异常和故障计时器，但它不等于进程隔离，不能保证从内存破坏、进程被终止等故障中恢复。研究重点应从“到处加异常捕获”转向明确模块边界及生命周期。

| 当前入口 | 需要研究的增量 | 对应候选 |
| --- | --- | --- |
| `AppSearch.cs`、`Launcher.cs` | 搜索提供者、统一排序、旧查询取消 | Flow Launcher、Everything |
| `ActionDispatcher.cs`、`ModuleWorker.cs` | 稳定协议、模块退出与重启、版本协商 | PowerToys Command Palette、MCP C# SDK |
| `Scenes.cs`、`LeoActions.cs` | 执行记录、明确重试边界、长任务恢复 | Polly、Workflow Core、Elsa |
| `ChatApi.cs` | 模型供应商适配与工具调用约束 | Microsoft.Extensions.AI |
| `ManualUpdates.cs`、`UpdateNotifications.cs` | 安装和更新事务、数据迁移 | Velopack |
| 吸附与皮肤系统 | 动作状态、过渡与打断规则 | VPet |

## 3. 候选总表

“可引入”表示适合做小规模接入验证，不表示本轮已安装或兼容性已经确认。

| 候选 | 复用方式 | 主要价值 | 成本判断 | 本轮决定 |
| --- | --- | --- | --- | --- |
| Flow Launcher | 参考设计，选择性适配源码 | 查询、结果、排序和搜索提供者 | 中：WPF 类型耦合 | 优先研究搜索，不搬宿主 |
| PowerToys Command Palette | 参考架构 | 进程外扩展、能力发现 | 高：WinRT、COM 与包清单 | 借鉴隔离，不追求插件兼容 |
| Everything SDK | 可选适配器 | 本机文件名检索 | 中：依赖后台 Everything | 用户自选，不作为必装项 |
| Polly | 可引入库 | 超时、熔断、有限重试 | 低至中：动作语义需自己定义 | 小范围验证，先核对版本政策 |
| Velopack | 可引入发布工具链 | 安装和增量更新 | 中至高：现有数据目录迁移 | 优先独立实验，不直接替换 |
| Workflow Core | 可引入库 | 持久化多步流程 | 中：流程版本和存储生命周期 | 出现跨重启需求再引入 |
| Elsa Workflows | 参考设计或后期引入 | 可视化流程及管理界面 | 高：完整设计器体系 | 当前暂缓 |
| MCP C# SDK | 可选协议适配器 | 接入外部工具生态 | 中：能力、权限和进程治理 | 保留扩展方向，非所有模块必用 |
| Microsoft.Extensions.AI | 可引入抽象库 | 供应商接口与流式调用抽象 | 中：中转站差异需验证 | 多供应商需求明确后试点 |
| VPet | 参考动画与模组设计 | 动作状态和动画组织 | 中至高：WPF 和素材授权区别 | 看逻辑，保留 Leo 自有素材 |

## 4. 搜索：先学 Flow，再决定是否接文件索引

### 4.1 Flow Launcher

官方项目提供应用和文件搜索、拼音、插件及多个文件索引后端。[官方仓库](https://github.com/Flow-Launcher/Flow.Launcher)

本轮进一步读了两个接口：

- [`Query.cs`](https://raw.githubusercontent.com/Flow-Launcher/Flow.Launcher/dev/Flow.Launcher.Plugin/Query.cs)：区分原始查询、搜索词、动作关键字及重新查询状态，适合作为 Leo 搜索请求模型的参考。
- [`Result.cs`](https://raw.githubusercontent.com/Flow-Launcher/Flow.Launcher/dev/Flow.Launcher.Plugin/Result.cs)：结果包含分数、标题、复制文字、补全及动作，但也依赖 WPF 的界面类型和委托。它不是可以原封不动搬进 WinForms 的纯数据协议。

对 Leo 的建议：抽取提供者思路，令应用、路径、便签、模块动作分别返回中性结果；由 Leo 统一排序和渲染。结果只携带动作 ID 与参数，最终交给现有 `ActionDispatcher` 执行。不要为了复用插件把 Leo 改造成第二个 Flow 宿主。

源码是 [MIT](https://raw.githubusercontent.com/Flow-Launcher/Flow.Launcher/dev/LICENSE)。真正复制时须固定提交、保留版权与许可证，并记录本地修改。此轮仅阅读，没有复制代码。

### 4.2 Everything SDK

官方 SDK 是通过 IPC 与 Everything 通信的 DLL/Lib 接口，要求 Everything 客户端在后台运行，并提供 C# 示例与非阻塞查询。[官方 SDK 文档](https://www.voidtools.com/support/everything/sdk/)

这适合增强“搜文件”，不能替代当前应用索引、路径补全或文件内容语义搜索。建议作为可选提供者：未安装或未就绪时保留原搜索能力；限制返回数量，取消过期查询，避免同步阻塞界面。不得把“SDK 可调用”理解为可以任意打包分发 Everything 主程序；正式接入前分别核对 SDK 与程序的分发条款。

## 5. 扩展：参考隔离方式，不重新造一套入口

### 5.1 PowerToys Command Palette

官方扩展通过 WinRT API 通信，并以进程外 COM 服务运行；使用包清单声明扩展，由 `IExtension` 提供 `ICommandProvider`。它也区分顶层、兜底和上下文命令。[官方扩展说明](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extensibility-overview)

对 Leo 最有用的是“核心负责入口，扩展负责能力，扩展有独立生命周期”。但 WinRT、COM 注册和包目录发现并非现有便携模块协议的直接替代。建议先在 Leo 现有 worker 上补充协议版本、请求 ID、取消、握手、退出清理和故障计数，再考虑单个第三方能力在独立进程运行。

进程分离有助于崩溃隔离，不自动构成权限沙箱。不能据此宣称陌生插件安全。PowerToys 主项目为 [MIT](https://raw.githubusercontent.com/microsoft/PowerToys/main/LICENSE)，实际选择源码时仍需检查所在目录及第三方声明。

### 5.2 MCP C# SDK

这是由协议项目维护、与 Microsoft 合作的官方 C# 客户端和服务端 SDK，许可证为 Apache-2.0。[官方仓库](https://github.com/modelcontextprotocol/csharp-sdk)、[许可证](https://raw.githubusercontent.com/modelcontextprotocol/csharp-sdk/main/LICENSE)

适用场景是未来接入已经提供 MCP 的工具，而不是把 QQ 音乐、Edge 和本地路径打开全部改造成 MCP 服务。建议由单个适配模块把外部工具映射到现有动作目录，明确输入范围、超时和用户可见名称；模型不得绕过本地执行策略。SDK 不负责替 Leo 判断哪些系统动作可以执行，也不使外部工具天然可信。

## 6. 可靠执行与组合：先明确语义，再选引擎

### 6.1 Polly

Polly 的策略管线可组合超时、重试、熔断等机制。超时通过取消令牌协作实现，官方明确指出忽略令牌的回调仍可能继续执行。[仓库](https://github.com/App-vNext/Polly)、[超时文档](https://www.pollydocs.org/strategies/timeout.html)

对 Leo 的建议：只对足球只读查询等明确可重试请求试点。播放下一首、切换代理和新增待办不能直接套自动重试；优先改成“设为开启”等明确目标状态，执行后再核验。超时不代表外部动作没有发生，取消也不等于撤销。

许可证是 [BSD-3-Clause](https://raw.githubusercontent.com/App-vNext/Polly/main/LICENSE)。另查到官方维护费公告：2026-11-16 之后发布的下一主版本、符合营收条件的直接引用存在维护费安排，既有版本不受该安排影响；这与当前代码许可证需分别核对。个人研究不因此需要付费，但未来商业化选型应重新确认。[官方公告及 2026-10-12 澄清](https://thepollyproject.org/2026/07/14/polly-osmf-announcement.html)

### 6.2 Workflow Core 与 Elsa

Workflow Core 是面向 .NET 的嵌入式流程引擎，支持可插拔持久化，适用于需要保存状态的长流程，主项目为 MIT。[仓库与说明](https://github.com/danielgerlag/workflow-core)、[许可证](https://github.com/danielgerlag/workflow-core/blob/master/LICENSE.md)

Elsa 支持代码、JSON 和可视化定义；其生态含 Server 与 Blazor Studio，主项目为 MIT。它能嵌入 .NET，并非一定要求部署完整服务器；但把设计器一起带入 Leo 将扩大维护范围。[官方仓库](https://github.com/elsa-workflows/elsa-core)

目前“准备看球”这类几个短步骤应继续使用现有组合。先补每步状态、取消和失败处理；只有用户确实需要“重启后继续”“等待事件几小时”“条件分支”时，才对比 Workflow Core。暂不引入 Elsa 设计器。停止外部音乐或恢复代理需要特定补偿动作，流程引擎也不能自动实现通用撤销。

## 7. 聊天：借统一接口，不扩大模型职责

Microsoft.Extensions.AI 提供 `IChatClient` 等抽象，以及流式消息、函数调用和中间件能力；抽象可由不同供应商客户端实现。[官方说明](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai)

对 Leo 的建议：若要支持多个供应商，可在 `ChatApi.cs` 外增加供应商适配层，先验证中转站的流式响应、工具参数、取消和错误格式。统一接口不会自动消除中转站差异，也不会凭空获得联网结果。

API 定位保持清晰：本地明确指令无需模型；查赛程由足球数据能力获取；需要理解含糊意图、总结或规划时才调用用户配置的模型。联网和模型推理是两条独立能力。不要为了接库要求用户换官方 Key，也不默认上传便签、剪贴板或所有应用记录。

## 8. 发布更新：Velopack 值得单独验证

Velopack 提供 .NET 启动钩子、更新检查、下载及应用更新接口，可使用 GitHub Releases 等更新源。官方指出更新会替换程序目录，配置不能放在被替换的位置。[集成文档](https://docs.velopack.io/integrating/overview)、[MIT 许可证](https://raw.githubusercontent.com/velopack/velopack/develop/LICENSE)

建议先制作独立的干净试验包，不替换用户当前安装。重点验证 Leo 固定 app 目录与既有启动项、配置位置能否迁移。需要明确“检查更新、下载、安装、数据迁移、恢复”的边界；不能仅凭支持更新就宣称断电回滚和个人配置恢复已经可靠。

## 9. 动画与人机交互相关工作

### 9.1 VPet：参考状态组织，避免继续用补帧掩盖状态问题

VPet 有独立的动画显示组件、核心逻辑与模组支持；其代码与内置动画的授权不是一回事。[官方架构说明](https://github.com/LorisYounger/VPet/blob/main/README.md?plain=1)、[二次开发文档](https://github.com/LorisYounger/VPet/blob/main/Secondary%20Development%20Support%20Documentation.md)

对 Leo 的研究重点应是待机、过渡、吸附、展开、拖动之间的状态、锚点和打断规则，而非继续迁移整个渲染系统。建议记录每帧脸部中心、缩放、透明度和屏幕坐标，区分图片变化与窗口位移。保留 Leo 皮肤素材；VPet 代码为 [Apache-2.0](https://raw.githubusercontent.com/LorisYounger/VPet/main/LICENSE)，其自带动画、图片另有声明及商业使用条件，不能一并当作 Apache 素材搬走。

### 9.2 两篇论文如何指导 Leo

**Amershi 等，CHI 2019，Guidelines for Human-AI Interaction。**提出并评估了 18 条人机交互指南，覆盖初次接触、常规使用、出错和持续使用。[作者机构提供的论文](https://www.microsoft.com/en-us/research/wp-content/uploads/2019/01/Guidelines-for-Human-AI-Interaction-camera-ready.pdf)

对 Leo 的具体转化：展示当前能力和模块状态；错误可纠正；耗时动作能取消；让用户知道究竟执行了什么。验收不能只看“模型回复正确”，还要看用户能否判断执行结果、修正错误并继续工作。这是应用论文原则，不是声称其研究已验证 Leo。

**Horvitz，CHI 1999，Principles of Mixed-Initiative User Interfaces。**讨论直接操作与自动化结合、意图不确定性、打扰成本及用户接管。[作者提供的论文](https://erichorvitz.com/chi99horvitz.pdf)

对 Leo 的具体转化：纯桌宠、极客、看球等方案控制主动程度；明确口令直接执行；含糊表达才澄清；重操作展示计划后执行；执行中保留停止入口。无需为了“智能”让每次点击都经过模型。

## 10. 建议的下一阶段实验顺序

以下是待审批的实验建议，本次没有开始实现。阈值是拟定验收目标，不是已经测得的数据。

| 顺序 | 小实验与对照 | 验收方式 | 不采用的条件 |
| --- | --- | --- | --- |
| 1 | 借 Flow 的提供者思路统一现有搜索，对照当前应用搜索 | 固定 50 条中文、拼音、别名、路径、便签查询；记录 Top-1/Top-5 和 P50/P95；应用命中率不得下降；旧查询不得覆盖新结果 | 增加明显启动延迟、普通应用搜索退化 |
| 2 | 参照 CmdPal 给现有 worker 增加完整生命周期 | 故障模块主动退出、卡住、输出损坏；Leo 搜索和便签继续可用；取消后无遗留 worker；恢复有明确入口 | 只能捕获异常，无法终止挂起模块 |
| 3 | Velopack 独立安装与升级验证 | 虚拟或隔离目录测试旧版升级、新装、下载中断、配置迁移；只用假 Key；备份恢复后配置一致 | 丢配置、启动项失效或无法保持现有使用方式 |
| 4 | 有需求时再验证 Everything、AI 抽象或流程引擎 | 分别对照现有路径搜索、中转站客户端、短组合；测增量依赖、内存、取消与失败路径 | 收益不足以覆盖依赖与迁移成本 |

实验记录至少包含：候选固定版本/提交、运行环境、样例、基线结果、候选结果、失败案例、依赖增量和是否采用。性能比较使用同一机器同一数据集，区分冷启动与热查询，避免只选成功示例。

## 11. 采用前的工程核对

1. 固定版本与许可证：本轮链接多为开发分支和在线文档，只用于调研。真正采用时记录准确提交、包版本、许可证和第三方声明。
2. 本仓库根目录未找到项目自身的 LICENSE；已存在的第三方许可证不能代替 Leo 自身的授权决定。公开分发前需明确项目代码、皮肤素材和球队标识各自适用条件，本轮不擅自更改授权。
3. 优先以依赖或适配层复用，只有很小且边界清晰的实现才考虑带来源复制。避免整块搬 UI、更新器或宿主后长期无法跟随上游修复。
4. 新能力继续使用现有动作入口、模块状态与用户方案，不额外建立一套快捷键、配置或后台常驻服务。
5. 本轮未引入第三方包、未上传数据、未改运行配置；研究不等于批准开发。完成调研后，再按候选收益和实验结果调整路线图。

## 12. 本轮交付与局限

- 已完成：10 个候选的适用性筛选、两篇相关论文阅读、关键接口及许可证核对、与 Leo 源码的映射、实验建议。
- 尚未完成：接入原型、运行时兼容性、内存和性能对比、最终依赖选型。不能据此承诺某个库接入后更快或更稳定。
- 结论可信度：接口耦合和文档描述有直接来源；接入成本、顺序和收益是当前工程判断，需要以上小实验验证。

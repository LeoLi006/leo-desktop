# 重新调研：Leo与成熟Agent生态的接入选择

**简体中文** | [English](RESEARCH.en.md)

调研日期：2026-10-22。Leo源码基线：21fcaf4，运行发布版v1.05.0。本轮为官方资料、选定源码和本地接口核对，没有安装第三方Agent、调用真实Key、运行性能对比或验证全部功能。结论是工程判断，不是性能或安全承诺。

[下一阶段路线图](ROADMAP.md) · [现有架构](Architecture/README.md)

## 1. 本轮问题与结论

问题从“还能增加什么功能”改为：现有Leo能否以合理成本复用外部Agent的规划、工具和记忆，同时保留本地操作的便捷、低依赖和可控性？

建议保留Leo的桌宠、窗口和本地执行底座；将Hermes列为第一个独立智能后端候选。先验证任务生命周期，再做受限连接；只有证明价值后，才考虑向外部Agent暴露Leo工具。暂不整体移植Hermes Desktop，不再按窗口零散拆内部服务。

这些是本轮建议，不表示已决定引入依赖。若原型只带来另一个聊天入口、复杂安装或无法接受的常驻开销，应暂缓或放弃。

## 2. 证据等级与方法

| 等级 | 本轮证据 | 能推出什么 |
| --- | --- | --- |
| E1 | 官方说明、规范和许可证 | 项目设计目标和公开接口，不代表在本机通过 |
| E2 | 选定源码文件 | 实际存在对应入口/字段，不能证明完整业务正确 |
| E3 | 本地Leo源码核对 | 可复用接口及已知边界，不是重新运行测试 |
| 尚缺 | 指定版本真实接入、个人中转兼容、资源和日常使用对比 | 必须在后续经批准的隔离试点补证据 |

外部链接多指向可变化的main或在线文档，本轮未将它们固定为可复现依赖。实施前必须记录所选版本/提交、API能力、锁定包及许可。不依据Star数判断质量，不采用网页宣传中的速度作为Leo实验结果。

## 3. 候选核对与取舍

### 3.1 Hermes Desktop / Hermes One：参考客户端设计

确认它是社区维护的Hermes客户端，可连接本地或远程后端；选定入口源码将窗口、IPC和退出资源分别管理。它适合作为任务进度与前后端分工的参考。依据：[项目说明](https://github.com/fathah/hermes-desktop)、[主进程入口](https://github.com/fathah/hermes-desktop/blob/main/src/main/index.ts)、[启动组装](https://github.com/fathah/hermes-desktop/blob/main/src/main/app/start.ts)。

工程判断：其Electron/React/TypeScript界面不能直接当作WinForms控件复用。Leo优先借鉴交互/边界，或连接同一后端，不整体搬宿主。所读客户端许可证为MIT；复制实质源码时仍需核对具体文件和保留声明。[依赖清单](https://github.com/fathah/hermes-desktop/blob/main/package.json)、[许可证](https://github.com/fathah/hermes-desktop/blob/main/LICENSE)。

未验证：当前客户端的资源占用、安装成功率、全部工具可用性；也没有证明其任何修复可以直接解决Leo的缩放问题。

### 3.2 Hermes Agent：优先验证独立运行API

官方集成说明区分HTTP API、JSON-RPC、ACP和Python嵌入。所读API源码确有运行、事件、审批、停止与能力发现入口，同时标记部分管理/记忆写入能力未开放。[集成说明](https://hermes-agent.nousresearch.com/docs/developer-guide/programmatic-integration)、[API源码](https://github.com/NousResearch/hermes-agent/blob/main/gateway/platforms/api_server.py)。

工程判断：先评估HTTP运行闭环；仅“能流式聊天”不足以证明任务可管理。不从固定端口或OpenAI兼容推断支持所有功能；实际连接先读能力，并验证认证、运行归属和终态。[架构](https://hermes-agent.nousresearch.com/docs/developer-guide/architecture)。

官方安全说明有审批与执行环境边界；不能因此认定默认本机后端已被隔离。所读核心许可证为MIT，尚未引入其代码或依赖。[安全说明](https://hermes-agent.nousresearch.com/docs/user-guide/security)、[许可证](https://github.com/NousResearch/hermes-agent/blob/main/LICENSE)。

未验证：用户中转的工具调用/流式事件兼容、停止语义、凭据隔离、后端安装负担，以及用户是否实际需要超出现有四步计划的任务。

### 3.3 MCP C# SDK：工具桥接候选，不替代Agent

官方SDK支持.NET客户端和服务端，并区分核心包、宿主和HTTP扩展。Hermes文档描述连接MCP工具服务。[官方SDK](https://github.com/modelcontextprotocol/csharp-sdk)、[Hermes MCP](https://hermes-agent.nousresearch.com/docs/user-guide/features/mcp)。

工程判断：未来可能用它将Leo有限能力提供给Hermes，不先建设任意MCP服务安装器。先只读、白名单、显式范围；任务审批/进度仍由任务协议负责。没有接入SDK，也没有确定包版本和.NET目标兼容。

所读LICENSE处于MIT向Apache-2.0过渡，并区分文档许可；不能沿用旧印象宣称所有文件都是MIT。选版时核对准确文件及包声明。[当前许可证](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/LICENSE)。

### 3.4 ACP：记录，不先实施

官方介绍主要面向编辑器/IDE与编码Agent的通信，包含会话、工具交互和取消。[规范入口](https://agentclientprotocol.com/get-started/introduction)。

工程判断：Leo当前不是IDE，首个目标也不是代码差异编辑。除非所选后端的必要功能只能通过ACP获取，否则不同时实现第三种协议。ACP与MCP解决不同边界，不能当成可互换名称。

### 3.5 PowerToys Command Palette：参考独立扩展职责

官方文档描述进程外.NET扩展、WinRT接口与清单发现。[扩展说明](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extensibility-overview)。

工程判断：借鉴能力、入口、安装责任的分离；不为复用而将Leo现有扩展改成COM/WinRT协议，不宣称兼容其插件。

### 3.6 Flow Launcher与Everything：保留为搜索专项候选

Flow提供Windows搜索和插件入口；所读仓库许可证为MIT。Everything公开提供SDK。[Flow仓库](https://github.com/Flow-Launcher/Flow.Launcher)、[许可](https://github.com/Flow-Launcher/Flow.Launcher/blob/dev/LICENSE)、[Everything SDK](https://www.voidtools.com/support/everything/sdk/)。

本轮Flow两个推测的接口原始文件地址返回404，未据此推断具体插件签名；没有完成其源码级适配审查。Everything未测安装状态、查询延迟或分发许可。

工程判断：先有Leo搜索问题再评估，不将它们塞进Agent试点。Everything SDK存在不等于允许把其程序直接重新打包。

## 4. 对照Leo：已有基础和真实缺口

| 本地源码 | 已确认基础 | 不能据此声称 |
| --- | --- | --- |
| [ActionContracts.cs](ActionContracts.cs)、[ActionExecution.cs](ActionExecution.cs) | 结构化结果、效果证据、取消与既有策略 | 通用远程运行协议、全局恰好一次 |
| [BrainControl.cs](BrainControl.cs)、[BrainInterface.cs](BrainInterface.cs) | 显式工具目录、最多四步、预览确认和原执行器 | 完整通用Agent、独立Hermes会话 |
| [ChatApi.cs](ChatApi.cs)、[QuickRouter.cs](QuickRouter.cs) | 中转流式调用、本地路由、Key本机加密 | Hermes全部审批/运行状态可直接复用 |
| [ModuleActivities.cs](ModuleActivities.cs)、[ModulePresentation.cs](ModulePresentation.cs) | 活动归属、过期回调和固定窗口服务 | 任意外部进程受宿主权限限制 |
| [ModuleWorker.cs](ModuleWorker.cs) | 一次请求/响应、超时和自有进程回收 | 常驻任务事件流、MCP/ACP兼容或权限沙箱 |
| [UsageProfiles.cs](UsageProfiles.cs) | 方案与模块选择已有实现 | 需要另造模式/偏好配置体系 |

实际缺口应优先核实：现有聊天是否能统一展示运行与审批、外部停止是否可确认、后端如何获知并受限调用Leo能力，以及关闭/重启后的运行归属。不能把尚未实现的远程桥接写成已完成底座。

## 5. 复用方式决策表

| 做法 | 本轮决定 | 改变决定需要什么 |
| --- | --- | --- |
| 保留Leo本地命令、数据Store和界面 | 继续 | 有明确无法局部解决的问题 |
| 借鉴Hermes任务交互 | 优先做最小事件核对 | 原接口已够用时不再加层 |
| 连接独立Hermes后端 | 隔离试点候选 | 版本/能力、真实价值和资源证据 |
| 使用官方MCP SDK提供Leo工具 | 后续只读试点 | 后端连接先成立，范围与授权已明确 |
| 复制Hermes完整客户端或Agent循环 | 暂缓 | 实际需求及维护收益足够明显 |
| 引入工作流引擎、记忆平台或多Agent调度 | 暂缓 | 现有有限组合确实无法完成场景 |

## 6. 试验设计与停止条件

选相同的模拟赛程、模块状态和本地命令，比较现有大脑与候选后端。记录任务成功依据、人工步骤、启动等待、模型请求数量、取消/断连结果，以及宿主和新增进程的资源。未启用后端时应没有新Agent进程或联网。

第一轮用可信假服务，检查能力缺失、错误认证、重复事件、事件流中断、审批拒绝、取消与迟到完成；这些不能冒充真实Hermes兼容。真实试验需另批批准，锁定版本和工作目录，禁止默认读取个人文件/Key或启动高权限shell。

若后端工具可直接读写本机，Leo工具桥接的白名单无法限制其其他工具。强隔离须依赖明确执行环境，而非仅靠“独立进程”或“只读工具名”。

停止条件：没有比现有功能减少操作或提升任务完成能力；默认负担显著增加；无法可靠识别等待审批/停止/未知结果；升级需要持续追随私有内部接口；或必须扩大私人数据权限才能成立。暂停不影响现有本地能力。

## 7. 适用范围与后续维护

本轮没有移植代码、增加SDK、修改API配置或替换本地程序，也没有上传GitHub。按[路线图](ROADMAP.md)逐批审批。下一批先验证已有任务体验与状态，不先扩张业务功能。

后续接入时保存具体版本、链接、兼容矩阵和定向报告；资料随main变化须重新核对。本报告不把文档声明、所读源码和实测结果混为一谈。

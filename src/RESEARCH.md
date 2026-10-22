# 定位驱动调研：Leo应该自研什么、集成什么

**简体中文** | [English](RESEARCH.en.md)

日期：2026-10-22。Leo源码基线：75f0e95；运行发布版：v1.05.0。当前只调研和更新文档，不安装软件、不接账号、不调用模型Key、不修改应用功能、不上传GitHub。

## 1. 先确定产品边界

已认可的定位：**一个可按需配置的桌面伙伴，负责让用户更快地调用电脑上的能力，并把这些能力组合起来。**

用户价值是少找入口、少切窗口、少做重复步骤。Leo负责入口、上下文选择、有限组合和清楚的结果；成熟软件负责其擅长的底层能力。纯桌宠用户不承担Agent、云端流程或额外监听的默认负担。

本轮不再以“接入Hermes”为中心。Hermes只是候选之一。没有成熟替代品也不自动意味着值得自研；有高星产品也不代表市场被绝对占领。应以具体用户场景、接入成本和操作收益决定。

## 2. 调研方法与证据边界

本轮筛选20个GitHub项目，覆盖启动器、桌面工具、输入自动化、截图、文件流转、记录、监测、桌宠、流程平台和Agent。

- GitHub公开API取得19个项目的Star、默认分支、归档状态和更新时间；Dify的API读取失败，采用项目页面约数。数据是2026-10-22快照，不是排名或市场份额。
- 阅读项目README/官方页面，并对8类接入入口补读官方资料。部分README API失败后改读网页；未取得的信息不补造。
- 19份成功元数据均显示未归档，但最近推送不等于稳定发布或长期维护承诺。dev、alpha、main上的功能不能直接推定存在于用户安装包。
- 未安装或运行这些工具，未测资源、延迟、兼容、成功率，也没有审阅全部源码。报告中的“可以集成”表示文档存在候选入口，尚不是Leo已支持。
- Star只用来发现有关注度的候选；产品完成度、系统兼容、许可、数据范围和实际减少的操作更重要。不是完整竞品普查，也不能据此证明某个需求无人解决。

原始抓取和阅读材料保留在本地忽略的.build目录，不作为当前生产能力清册。实施前再固定版本、接口和许可。

## 3. 高关注项目做了什么

表中“接法”和“判断”为针对Leo的工程建议，不是功能上线清单。项目链接也是功能概况的主要出处。

| 项目 | Star快照 | 已有能力 | 对Leo的候选接法 | 边界判断 |
| --- | ---: | --- | --- | --- |
| [microsoft/PowerToys](https://github.com/microsoft/PowerToys) | 139,561 | Windows工具、窗口布局、命令入口 | 直接使用其工具；布局先调用用户已有Workspaces快捷方式 | 成熟布局与工具箱不自研 |
| [Flow-Launcher/Flow.Launcher](https://github.com/Flow-Launcher/Flow.Launcher) | 15,846 | 应用/文件搜索与插件入口 | 参考搜索体验；不先唤醒第二个启动器 | 保留Leo轻量搜索，缺口再接索引 |
| [AutoHotkey/AutoHotkey](https://github.com/AutoHotkey/AutoHotkey) | 13,332 | 快捷键、窗口和桌面脚本 | 只调用用户选择的可信脚本；版本和参数待实测 | 不开发通用宏录制/脚本引擎 |
| [espanso/espanso](https://github.com/espanso/espanso) | 14,670 | 文本展开与模板 | 用户直接使用；需要时只给管理入口 | 不重复做全文本替换引擎 |
| [ShareX/ShareX](https://github.com/ShareX/ShareX) | 40,298 | 截图、标注、贴图和处理流程 | 已读CLI：图片编辑、贴图、具名工作流 | 优先验证有具体动作的适配 |
| [localsend/localsend](https://github.com/localsend/localsend) | 95,184 | 局域网文件传输 | 当前README有独立CLI发送；需核对发布包和收件确认 | 不重写发现/加密传输协议 |
| [syncthing/syncthing](https://github.com/syncthing/syncthing) | 89,608 | 持续文件夹同步 | 已有实例的认证REST接口；可选状态/入口 | 不开发文件同步引擎 |
| [laurent22/joplin](https://github.com/laurent22/joplin) | 56,830 | 笔记、组织、同步 | 已读本地Data API；选择性送入笔记 | 保留Leo速记，不做知识库平台 |
| [sabrogden/Ditto](https://github.com/sabrogden/Ditto) | 7,350 | 剪贴板历史与粘贴 | 直接使用；未核实适合Leo的结构化接口 | 不复制其数据库或默认双重监听 |
| [ActivityWatch/activitywatch](https://github.com/ActivityWatch/activitywatch) | 19,323 | 活动时间与上下文记录 | 有API；只有用户主动要统计才评估 | 不默认记录全部窗口/网页历史 |
| [LibreHardwareMonitor/LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) | 9,251 | 温度、风扇等硬件传感器 | 有可集成库；额外权限/硬件兼容待验证 | 保留现有基础监测，不自写驱动 |
| [rainmeter/rainmeter](https://github.com/rainmeter/rainmeter) | 6,100 | 桌面小组件与皮肤 | 官方Bangs命令可参考；用户直接使用 | 不做完整桌面组件平台 |
| [LorisYounger/VPet](https://github.com/LorisYounger/VPet) | 7,011 | 桌宠、动作和交互生态 | 参考体验和素材协议边界 | 桌宠并非无人领域；Leo保持自身交互 |
| [n8n-io/n8n](https://github.com/n8n-io/n8n) | 208,278 | 跨服务自动化和可视化流程 | 已有实例通过配置好的Webhook触发 | 不复制大型流程画布/连接器库 |
| [node-red/node-red](https://github.com/node-red/node-red) | 23,774 | 事件流与低代码自动化 | HTTP In/Response端点，可按需接用户已有流程 | 保留简单组合，复杂流程交给外部 |
| [open-webui/open-webui](https://github.com/open-webui/open-webui) | 156,403 | 完整聊天与知识工作台 | 用户直接使用；有明确需要才研究专用接口 | 不复制完整聊天/RAG工作台 |
| [NousResearch/hermes-agent](https://github.com/NousResearch/hermes-agent) | 256,049 | Agent、工具、记忆和任务执行 | 可选后端候选，未选定也未接入 | 不从头开发通用Agent平台 |
| [fathah/hermes-desktop](https://github.com/fathah/hermes-desktop) | 14,448 | Hermes的桌面管理和聊天界面 | 参考任务反馈；不嵌套整个工作台 | Leo不是Hermes的专用皮肤 |
| [restic/restic](https://github.com/restic/restic) | 36,721 | 增量备份与恢复 | 只在需求明确时调用用户已有备份任务 | 不开发完整备份引擎 |
| [langgenius/dify](https://github.com/langgenius/dify) | 约159k（页面，非API） | AI应用、RAG与工作流平台 | 只评估用户已发布的应用接口，不整体嵌入 | 不做企业级AI应用搭建平台 |

## 4. 真正值得深入核对的8类入口

### 4.1 ShareX：优先研究具体动作，不复制截图软件

官方CLI支持图片编辑、贴图以及具名工作流。可先评估“把图片拖到Leo→交给ShareX标注/贴图”。[官方参数](https://getsharex.com/docs/command-line-arguments)

重要边界：官方文档同时说明，仅传文件路径会触发上传。不能把通用“打开文件”直接当安全截图动作；需明确动作及用户设置，核对工作流是否自动上传。CLI被调用或编辑器打开不等于截图已保存、标注已完成。仅在返回结果确证后才能接续依赖该文件的步骤。

### 4.2 PowerToys Workspaces：布局能力由原工具提供

官方提供保存工作区及通过快捷方式启动布局的方式。Leo可以先调用用户已有快捷方式，不读取或仿造私有工作区格式。[官方说明](https://learn.microsoft.com/en-us/windows/powertoys/workspaces)

但“点Leo启动布局”未必比点原快捷方式方便。只有它与已有专注、应用或其他动作形成有用组合时才值得增加入口。快捷方式启动只能证明提交；不能声称所有窗口已正确定位。

### 4.3 LocalSend与Syncthing：区分临时发送和持续同步

LocalSend当前README列出独立CLI的send及目标选择，说明有进一步适配线索；不能推定Windows GUI安装包自带该CLI。[CLI说明](https://github.com/localsend/localsend#command-line-interface)

Syncthing有认证REST接口，更适合已有同步任务的状态/入口，不代替临时发文件。[REST说明](https://docs.syncthing.net/dev/rest.html)

候选场景是“拖入选定文件→选择收件设备/已有任务”。不要重新实现设备发现、传输或同步冲突解决。接收确认、CLI版本、递归目录范围与发送成功证据仍需验证；不自动选设备发送，不把同步当备份。

### 4.4 Joplin：速记与知识库分工

Data API在Clipper服务启用后可用，并要求Token。[官方Data API](https://joplinapp.org/help/api/references/rest_api/)

保留Leo无需配置的一句话便签。只在用户已有Joplin且需要时，评估把选定便签/图片送入指定笔记本；不复制笔记知识库，也不后台扫描全部笔记。需处理Token保护、保存结果、重试重复和目标失效。日志不可直接记录带Token的完整请求URL。

### 4.5 AutoHotkey：可信脚本的调用者

官方说明脚本可一次执行后退出，也可定义持续快捷键；命令行调用是接入候选。官网访问返回403后，核对了官方文档仓库。[官方文档源码](https://github.com/AutoHotkey/AutoHotkeyDocs/blob/v2/docs/Program.htm)

Leo不自研通用宏录制器，不默认执行模型生成脚本。只考虑调用用户明确选择且理解的脚本；v1/v2、参数、工作目录、退出行为和副作用先验证。脚本进程退出不一定证明目标软件完成操作。

### 4.6 Node-RED与n8n：已有复杂流程的入口

Node-RED文档给出HTTP In/Response端点，n8n有Webhook触发与不同返回时机。[Node-RED端点](https://cookbook.nodered.org/http/create-an-http-endpoint)、[n8n Webhook](https://docs.n8n.io/integrations/builtin/core-nodes/n8n-nodes-base.webhook/)

不复制画布、连接器库和凭据中心。用户已有流程且需要桌面触发时，才连接指定端点；不默认安装Node.js、Docker或整个服务。HTTP已接收不等于整个流程成功，超时不能盲目重试；必须先核对认证、结果与取消能力。Node-RED编辑器和HTTP节点权限也不是同一配置。[Node-RED访问控制](https://nodered.org/docs/user-guide/runtime/securing-node-red)

### 4.7 ActivityWatch与LibreHardwareMonitor：摘要与底层分离

ActivityWatch提供活动数据API；LibreHardwareMonitor的README提供库用法，并说明某些传感器需要额外权限。[ActivityWatch API](https://docs.activitywatch.net/en/latest/api.html)、[传感器库](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor#integrate-the-library-in-own-application)

Leo现有CPU/内存摘要继续沿用Windows读取。温度/风扇有真实需求再评估库，不自己写驱动。完整使用历史不默认收集；Leo自己的最近使用排序不需要因此升级成全量行为监控。

### 4.8 Rainmeter、Flow与Agent：参考成熟系统的边界

Rainmeter有Bangs命令，但不因此值得复制其组件平台。[官方命令](https://docs.rainmeter.net/manual/bangs/)

Flow适合参考查询/键盘体验；若只是在Leo后面再打开另一个搜索框，就不满足用户此前“没有中间唤醒过程”的要求。不宣称已获得可嵌入的Flow搜索服务。需要全盘文件检索时可单独核对非GitHub的Everything SDK，而非重建索引。[Flow](https://github.com/Flow-Launcher/Flow.Launcher)、[Everything SDK](https://www.voidtools.com/support/everything/sdk/)

Hermes、Open WebUI和Dify说明完整聊天、记忆、Agent和AI应用平台已有成熟投入；Leo暂不复制这类工作台。若出现普通本地组合解决不了的场景，再比较外部后端；不因高Star就绑定Hermes。[Hermes集成说明](https://hermes-agent.nousresearch.com/docs/developer-guide/programmatic-integration)

## 5. 直接用、轻集成、借鉴、自研

| 选择 | 适用范围 | Leo应该负责 |
| --- | --- | --- |
| 直接用现成产品 | 邮箱、播放器、安全软件、完整笔记/聊天工作台 | 清晰可选的打开入口；没有收益时不加入口 |
| 轻集成已有能力 | 已验证CLI、URL、快捷方式、系统接口、本地API | 选择目标、少量参数、必要授权、等待与真实反馈 |
| 借鉴交互和边界 | 搜索、任务进度、桌宠动作、插件描述 | 根据Leo自己的使用方式局部改进，不整体迁移框架 |
| 自研核心衔接 | 内容拖放、跨能力上下文、有限组合、安静反馈、按需显示 | 对用户负责从意图到结果的连续体验 |

“调用外部程序”和“复制其代码/打包其二进制”是不同决定。GitHub许可标签只是初筛；GPL/MPL、定制许可、商标素材和依赖条款均需在选版时核对。n8n的README列出Sustainable Use/企业许可；Open WebUI明确有品牌保留等许可差异，Dify有单独许可，不将它们统称为可随意移植的MIT代码。[n8n说明](https://github.com/n8n-io/n8n#license)、[Open WebUI许可](https://github.com/open-webui/open-webui#license-)、[Dify许可](https://github.com/langgenius/dify/blob/main/LICENSE)

## 6. Leo可验证的差异化，而非宣称“空白市场”

### A. 内容到动作

用户拖入图片、文件、文字或网址，Leo根据内容和用户已经启用的工具给出少量适合的动作，而不是展示庞大工具目录。比较对象应包括资源管理器右键和原软件入口；若没有更方便，就不新增。

### B. 私人场景的一次完成

已有“准备看球”可作为现成场景核对，不作为新功能重复开发。布局、媒体、浏览器、代理或提醒是否组合，由用户选择；简单固定组合沿用现有执行器，复杂云流程交给已有服务。不能把一次启动多软件就宣称为独有技术。

### C. 与桌宠结合的安静控制

闲时保持伙伴交互，操作时短暂呈现有用状态，需要确认或真实出错才打扰。所有额外适配都可关闭；离线、缺软件时不拖垮桌宠。外部工具进程能否取消、谁负责关闭，要说清楚。

这些是定位假设，不是无人竞争的结论。差异应由真实操作对比与用户复用来证明。

## 7. 三个候选试验，不是三个立即开发任务

| 候选 | 最小范围 | 比较对象 | 不成立则停止 |
| --- | --- | --- | --- |
| 图片拖放→ShareX标注/贴图 | 单个已验证图片动作，不加完整截图窗口 | 用户现在的右键菜单/ShareX热键 | 需要更多点击、无法避免意外上传或无法说明结果 |
| 一次恢复常用场景 | 复用现有组合，必要时调用用户已有Workspaces快捷方式 | 原布局快捷方式与现有组合 | 只是换一个按钮，没有组合收益 |
| 速记→用户已有Joplin | 单向、选定内容、选定目标，不自动同步 | 直接打开Joplin记一条 | 配置和服务负担大于便利，或已有入口同样简单 |

LocalSend发送也值得保留为候选，但先确认当前Windows发布物的CLI可用性，不能根据main文档直接承诺。

优先排序看用户自己是否已经使用原工具、该动作频率，以及是否真正少一步；不是按Star排序，也不是要求把这些软件装一遍。候选未获批准前不进入生产代码。

## 8. 利用现有底座，避免再次造框架

[LocalApplications.cs](LocalApplications.cs)已有程序、参数、工作目录和权限配置；[FileDropActions.cs](FileDropActions.cs)能沿配置追加有效路径；[CustomCombos.cs](CustomCombos.cs)和[ActionExecution.cs](ActionExecution.cs)已有动作组合及执行规则。

多数“添加应用”应先证明现有配置是否足够。需要新适配器时，补真实软件特有的能力与结果，不为每个应用复制设置窗、重试队列或大脑。只有CLI/API无法表达的具体缺口才申请新增接口；不先建设万能连接器市场。

## 9. 每个候选的准入卡

实施前只需回答同一组具体问题：

1. 用户现在如何完成，Leo能少哪一步？拿不到使用路径时，先观察而非假定。
2. 用户是否已有此工具？缺软件时只解释，不自动安装。
3. 哪个已验证版本提供什么入口？正式发布物、认证和许可如何确认？
4. 输入要读取什么，输出怎样确认？启动、接收、完成与未知分开。
5. 副作用、重复触发、超时、取消和故障怎样处理？软件仍由其原负责人维护。
6. 关闭模块后还有哪些进程、轮询或监听？个人路径与Key能否避免进入分享包？
7. 相比原工具的快捷键、右键或现有Leo组合，新增复杂度是否值得？

无需默认调用模型的动作坚持本地执行。模型后端只有在明确理解收益时接入，不能成为打开一个工具的必经中间层。

## 10. 对现有路线图的影响与下一步

上一版R1–R5是围绕外部Agent写的候选路线，现暂停自动推进；它不代表已批准，更不再是默认优先级。见[路线图状态说明](ROADMAP.md)。

本轮建议先由用户从真实高频需求选一个候选，再做“现有配置能否完成→最小隔离验证→操作对比”的单场景试验。若都不需要，先优化现有入口和稳定性，不为调研结果找功能。

本轮未建立市场垄断结论、未证明任何资源优势、未完成全量源码或许可审计。所有集成、安装、收费API验证、发布与上传，仍需明确范围和批准。

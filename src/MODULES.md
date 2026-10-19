# Leo 模块接口与独立进程协议

适用 v0.51.0。入口：设置 → 功能 → 管理扩展模块。

## 架构

`ActionDispatcher` 接收带来源的 `ActionRequest`，通过 `ILeoModule` 查找能力、检查启用状态并执行；并发相同请求合并，递归调用拒绝。聊天模型工具、文字、语音、搜索和自定义组合复用此入口。

内置能力通过统一模块适配器提供；外部能力可以是声明式动作或独立进程。安装和启动时不会自动执行扩展命令。内置模块随主程序发布，当前支持停用；外部模块可以独立安装、更新和移除。

## 声明式模块

导入一个UTF-8 JSON文件。字段大小写如下：

```json
{
  "Format": "LeoModule/1",
  "Id": "daily-plan",
  "Name": "每日计划",
  "Version": "1.0.0",
  "Runtime": "actions",
  "Commands": [
    {
      "Id": "today",
      "Title": "查看今日计划",
      "Phrases": ["查看今日计划"],
      "Action": "todos_open",
      "Value": ""
    }
  ]
}
```

Id为2～40位小写字母、数字、连字符，首位必须是字母。每模块1～16个命令；命令Id为1～40位同类字符。名称不超过40字符，每条口令不超过60字符。Value为字符串，复杂参数以JSON字符串提供，最多4000字符。

声明式Action只能是当前公开的动作。为避免递归，不允许声明式模块直接调用组合、再次分派文本或运行其他扩展。可以导出示例后修改。

## 独立进程模块

将`module.json`与自己的Windows程序放在ZIP根目录，其他依赖可按原相对目录放入ZIP：

```text
module.json
worker.exe
worker.dll             # 可选依赖
worker.runtimeconfig.json
```

声明示例：

```json
{
  "Format": "LeoModule/1",
  "Id": "my-capability",
  "Name": "我的新能力",
  "Version": "1.0.0",
  "Runtime": "stdio",
  "Executable": "worker.exe",
  "TimeoutSeconds": 15,
  "Commands": [
    {
      "Id": "run",
      "Title": "运行我的能力",
      "Phrases": ["运行我的能力"],
      "Action": "worker:run",
      "Value": ""
    }
  ]
}
```

Leo以当前用户权限启动包内程序，附加固定参数`--leo-module`，通过标准输入写入一行UTF-8 JSON，随后关闭标准输入。程序不得依赖当前桌面前台或把日志写入标准输出。

请求示例（实际路径和requestId由Leo生成）：

```json
{"protocol":"LeoModule/1","requestId":"随机id","command":"run","value":"","dataDirectory":"模块专用本地数据目录","origin":"search","theme":{"skin":"83","name":"Leo","accent":"#E7C277","background":"#0B1221","text":"#EDF2FA","radius":14,"opacity":100}}
```

命令为`worker:`之后的名称。Value默认来自声明；`module_run`可以用JSON字符串`{"id":"ext:my-capability:run","value":"参数"}`覆盖本次参数。主题字段提供当前显示名、基础球队及配色，方便模块自行呈现相同外观。

程序返回一行UTF-8 JSON，并正常退出：

```json
{"ok":true,"message":"已完成具体操作"}
```

失败时使用`ok:false`和具体原因。响应行最多64KB，message最多4000字符；非零退出、无响应、格式错误和超时均视为失败。默认超时15秒，可设置1～120秒；取消或超时结束扩展进程及其子进程。标准错误可以输出诊断日志，Leo只排空流，不上传。退出Leo会取消未完成动作并结束其持有的辅助进程；不将QQ音乐或PotPlayer登记为辅助进程。

独立进程是协议边界，不是权限沙箱：程序具有当前用户权限。导入会展示运行类型和动作，执行只在用户触发后发生。不要把API Key等私人配置写进发布的模块包；模块自己的状态放在请求提供的数据目录，避免更新覆盖。

## 更新、移除与文件范围

- 同Id更新必须提高数字版本号，如1.0.0 → 1.1.0。
- 运行或排队时不更新、不移除。ZIP先解压到临时目录，成功后替换模块目录；替换失败保留旧模块。
- ZIP最多64MB，展开最多256MB、2000文件。拒绝越界路径、重复路径、链接及不支持的脚本文件。
- 允许的文件类型：exe、dll、json、png、txt、md、dat、bin；可执行文件必须在根目录。
- 程序目录：`modules/<id>`；状态目录：`module-data/<id>`。移除保留状态目录。
- 不在主进程内加载第三方DLL；模块可以使用自身进程需要的DLL或运行时。
- 模块自己的联网/API行为由其实现决定，Leo不自动把本机API Key交给扩展。

## 调用

- 搜索框：`:运行我的能力` 或 `:ext:my-capability:run`。
- 聊天和语音：说出名字或已登记的口令。
- 自定义组合：新增“统一指令”步骤。
- 模型工具：先`module_list`读取可用动作，再用`module_run`传入返回的精确ext动作Id。列表不包含声明中的固定参数值。

内置明确命令优先；扩展口令相互冲突时拒绝随机执行。当前协议只返回文本结果；需要复杂界面的模块可自行提供界面，并读取主题字段适配外观。


## 本地应用模块（v0.52.0）

设置中的“应用模块与图标”允许无需编写扩展包就添加本机EXE/LNK。每项拥有独立ID、名称、启用状态和可选图标路径；运行时通过`ILeoModule`暴露`local:<32位ID>`动作，停用和移除即时影响统一动作入口。`module_list`的`applications`返回不含程序路径的ID与名称；`local_app_open`接受该ID。快捷栏和聊天工具均调用同一动作分发器。

`path_open`接收用户提供的本机绝对文件/目录路径，通过Windows默认关联打开；支持引号和环境变量，拒绝不存在路径。此动作可能执行程序，调用方须遵守用户明确意图。

普通自定义应用只启动程序。需要专用控制或复杂逻辑时，仍使用独立ZIP进程扩展。内置应用适配器使用同一设置管理入口，停用不卸载外部程序。


## v0.53.0 补充

本地应用记录增加`Arguments`、`WorkingDirectory`、`Administrator`，均随本机设置保存。通过`ProcessStartInfo`直接交给所选EXE/LNK，参数不通过额外shell拼接。管理员启动使用Windows `runas`确认。失效程序路径由同名候选与用户确认修复，参数保持不变。

快捷顺序单独存为`ShortcutOrder`的模块ID列表，停用模块保留排序记录；新模块追加，拖放排序及溢出入口调用同一动作分发器。

新增动作：`notes_open`（可带便签草稿）、`note_add`（明确要求记下的文字）、`path_reveal`（本地路径）、`media_file`（PotPlayer媒体路径）、`files_with_app`（JSON字符串`{application,paths}`，应用必须为存在的EXE）。`files_open`的文件选择结果进入统一拖放动作面板。

便签、拖放内容及其它个人记录不包含在皮肤包或源码发布包中。普通应用模块仍只提供启动能力；复杂控制沿用独立进程扩展协议。


## S01：入口显示（v1.00.1）

`Preferences.EntryDisplayModes`独立保存六个内置可选模块及自定义应用的入口选择。键为原模块ID或自定义应用GUID，值为`shortcut`、`menu`、`search`；缺省为`shortcut`，不改变旧配置。未知但格式有效的键保留，便于模块重新加入后恢复。

显示策略只控制通用按钮与菜单，不参与`Modules.Enabled`和动作执行权限判定。隐藏不等于停用：指令仍走原分发器；真正停用仍由原模块开关和自定义应用`Enabled`判断。扩展指令入口沿用现有协议，本轮不增加扩展桌面按钮。


## S03：统一状态（v1.00.2）

`ModuleStatuses.Read` 对内置模块、自定义应用与扩展输出统一状态；只读检测使用 `ModulePaths.PassiveRead`，不会触发修复对话或启动软件。停用状态优先，其次校验指定路径，再检测依赖，最后呈现当前会话的执行状态。模型API只读配置完整度，不自动联网测试。

`ActionDispatcher` 与 `ClientOperationHub` 将等待、成功、失败和取消写入 `ModuleActivity`。递增操作票据阻止旧请求完成时覆盖新请求；取消恢复可用状态。已安装只表示依赖有效，不要求软件已运行。状态页提供对应设置入口；重新检测只确认失败记录，不重放动作。

设置字段 `AutoUpdateNotifications` 默认true，`LastNotifiedUpdate` 记录已提醒的版本。自动检查只访问公开正式发布信息，不消费模型API；失败静默，下载和安装仍由手动更新窗口完成。


## v1.01.0：使用方案与活动释放

`UsageProfile`使用`leo-profile-1`白名单格式；不复用`Preferences`作为导出载体。`UsageProfiles.Prepare`在当前本机配置副本上只变更能力、布局、快捷键和后台选项，不修改本机路径或个人记录。导入拒绝未知字段并分配新ID，切换经预览确认后调用统一设置路径。`ProfileTransition`保留切换前副本，失败时恢复并报告错误。

`Preferences.ApplicationIndexing`默认true以兼容旧配置。关闭后取消STA扫描、丢弃过期结果并停止上下文观察；`ClipboardHistoryService.ConfigureActivity`根据开关停止轮询；足球开关控制同步定时器和请求生命周期。

`ClientOperationHub.Configure`按资源取消排队与等待中的动作，并在停用时拒绝新请求。它不退出本机软件，也不回滚已完成的代理或播放操作。独立扩展使用`module:<id>`资源名。

`FileDropActions.ConfiguredStart`按模块ID解析当前已启用应用，沿用`LocalApplications.Start`的参数、工作目录和提权设置，通过Windows引号规则追加有效文件路径。拖放请求不接受外部传入的管理员标记或任意启动参数。


## v1.02.0：窗口交互与故障边界

`WindowInteraction`集中普通窗口的可见区域、父窗口附近放置、焦点和最小化恢复；伴随浮层与提醒保留各自层级。`SelectionKeys`只给明确的列表动作绑定Enter/双击，不截获正文方向键。

`Preferences.QuickNoteHotkey`添加第四组快捷键，使用统一的注册、冲突回滚及配置迁移校验。`QuickNoteCompatibility`在旧配置省略此字段且原快捷键已占用默认组合时停用新增绑定；不修改旧绑定。方案增加可选的`NoteKey`字段，旧方案默认未启用。

`StickyNote.Pinned`默认false，兼容旧便签文件。`NoteStore.Query`只按分类与关键字筛选，不删除已关联待办的原文；写入采用原子替换，通知订阅者失败不会反过来撤销已保存的数据。

`FeatureFaults`提供可恢复异常边界、定时器熔断、UI兜底和本次运行诊断；不存储异常消息或用户输入。UI连续异常按活动普通窗口隔离，核心附属窗口不销毁；进程级致命异常不伪装成功恢复。独立进程扩展仍是隔离原生崩溃的边界。

吸附绘图通过`HeadGeometry.Bounds`对齐脸部中心，`SpriteBlend`按预乘像素插值相邻姿态，避免透明度凹陷和旧轮廓残留。绘图与命中区域共用同一采样。

## v1.03.0 性能与生命周期约定

主题挂接不重复扫描已接管的控件树，新控件通过 ControlAdded 接管。透明桌宠的绘制缓冲由 PetForm 独占，尺寸变化重建、退出释放；预览截图仍返回独立位图，不允许调用者释放运行中的共享缓冲。

统一动作在模块停用时取消，取消之后的返回结果不记为成功。客户端操作队列在退出时终止后续工作；扩展进程清理有界等待，不把进程隔离误称权限沙箱。关闭或 Dispose 必须都能取消窗口任务，重复 Dispose 不得再次访问已释放的取消源。

AtomicJson 用于完整 JSON 的同目录替换及本地备份恢复；不是多文件事务。恢复只采用通过对应类型校验的副本，损坏主文件单独保存。原始数据和备份均是本地个人数据，不属于模块或程序分发资产。

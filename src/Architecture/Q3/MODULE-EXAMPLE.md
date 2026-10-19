# 宿主内数据模块实现样例

本例说明当前已实现的内部接口，不是可导入模块包，也不要求用户填写这些代码。完整可运行实现是 `NotesCapability.cs`、`ModuleHostServices.cs` 和 `Q3NotesTests.cs`；不另维护一份与实际实现脱节的示例模块。

## 1. 定义动作与注册

实现 `IStructuredLeoModule`，声明稳定 ID、动作列表、输入长度、当前配置有效性，以及每个动作的副作用、并发策略、超时和不自动重试。选择已经支持的 `coalesce-inflight` 或 `serialized-store`；后者要求模块使用自己的存储串行边界，不代表宿主替任意外部资源自动加锁。

宿主只做一次注册：

```csharp
var windows = new ModuleWindowService(this);
commands.Registry.Register(new NotesCapability(
    () => Notes,
    (action, value, token) => windows.ShowAsync(() => {
        if (action == "note_quick_open") ShowQuickNoteCore();
        else ShowNotesCore(value);
    }, token)));
```

被替换的旧动作从旧适配器的列表移除，其他旧动作继续由旧实现执行。不要在按钮和聊天入口再次直接写文件，也不要同时保留两个动作所有者。注册 API 是受信宿主代码入口，尚未对外开放动态卸载或不受信代码加载。

## 2. 接收请求

旧的 `note_add` 字符串值仍是正文。新调用者可以使用结构化输入：

```csharp
var result = await commands.ExecuteStructuredAsync(new(
    "leo-action/1", "note_save",
    JsonSerializer.SerializeToElement(new { id = (string?)null, text = "一个想法" }),
    Source: "example", RequestId: "one-user-intent"), token);
```

`IActionRequestAdapter` 验证协议、允许的字段和重复字段，再转为同一 `ActionRequest`。模块在执行时验证正文、ID 和数据类型。调用者要重试同一次操作时复用 RequestId；新操作必须使用新 ID。便签实现只支持本次进程的有界成功回放，不推广为跨模块持久幂等保证。

## 3. 使用宿主服务

- 数据：`ModuleJsonData<T>` 接收文件路径、初始值、旧格式验证器、快照复制器。便签元素是不可变记录，所以复制列表即可；带可变子对象的新模块必须深复制。`Change` 回调只编辑新快照，不执行外部副作用；写入成功才发布。`Read` 回调是受信实现，返回业务快照，不能暴露可变内部对象。
- 窗口：通过 `ModuleWindowService.ShowAsync` 切回 UI 线程。将具体创建、位置和显示放进回调，排队时保留请求取消令牌；不要让后台数据操作同步等待 UI 通知。
- 反馈：默认只报告失败，普通查询不弹消息。仅用户需要一次确认时启用 acknowledge。实际窗口现有的保存状态文字继续保留。
- 结果：返回 `ActionPayload`，读操作证据为 snapshot，写操作为 committed，打开窗口为 window-shown。包含业务正文时标记 PrivateData，历史中不留正文。

## 4. 退出与验证

宿主释放窗口前解除数据观察订阅；确需保留的未保存文本在分发器释放前提交。异步回调必须检查窗口是否已销毁。取消提交前可中止操作，提交后只能停止等待，不能向用户谎称已撤销。

使用独立临时目录和可注入 writer；至少覆盖旧格式读取、成功/失败写入、原文件保留、并发、排队取消、同请求重试、不同请求不合并、窗口销毁和原入口兼容。测试不得使用个人 JSON、真实聊天 API 或外部应用启动。

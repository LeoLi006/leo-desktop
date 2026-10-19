# 宿主内部能力接口：Q2 最小子集

本文件固定 Q2 已实现的部分；Q1 CONTRACT.md 中其余内容继续是草案。不是开放网络 API，也不向旧 worker 发送新格式。

## 1. 定义与注册

`CapabilityRegistry` 是唯一动作查找入口。`ILeoModule` 保留 ID、名称、版本、启用状态和动作列表，以承载旧模块。`CapabilityDefinition` 为本机应用提供 canonical action ID、module ID、运行类型 application-adapter、输入 application-id/v1、external 副作用、coalesce-inflight 并发策略、15000ms 默认超时和 Retry=false。

新执行器实现 `IStructuredLeoModule`：提供动作描述、当前配置检查、结构化执行与错误码映射；公共执行器按此接口工作，不为每个应用名称或具体模块类型添加分支。当前只支持已实现的 coalesce-inflight/有界超时/不自动重试策略；未支持的声明明确拒绝，而不是悄悄忽略。

本机应用 action/module ID 均为 `local:<原32位GUID>`。别名 `local_app_open` 的字符串 value 为此 GUID，旧 `local:<ID>` 的 value 沿用原来不影响固定启动参数的行为。重复模块/动作 ID 拒绝注册；配置快照完整建立后才替换，避免半更新。

注册表不在查询状态时启动程序或修复路径。可用性仍由当前启用状态及实际调用时的依赖校验共同决定；完整被动健康状态继续由已有 ModuleStatuses 提供，不新增第二套设置。

## 2. 结构化输入

宿主通过 `ExecuteStructuredAsync(StructuredActionRequest, token)` 调用，本批仅支持应用启动：

```json
{"Protocol":"leo-action/1","ActionId":"local_app_open","Parameters":{"applicationId":"11111111111111111111111111111111"},"Source":"search","RequestId":"fixture-request","TraceId":"fixture-trace","Deadline":"2030-01-01T12:00:00Z"}
```

以上是 C# 记录的字段表示，不是可直接导入的模块包。Parameters 必须恰好只有一个字符串 applicationId；GUID 必须存在于本机配置。也可使用 canonical ActionId，但必须与 Parameters 中的 ID 一致。不接受外来 path、arguments、administrator 等覆盖字段。

协议版本不符或参数多余/类型错误，返回 invalid_request 且不启动。来源非空且不含控制符，最多128字符；请求/调用链 ID 非空、不含控制符，最多96字符。ID 省略时自动生成；嵌套调用继承 trace ID。

RequestId 用于追踪，**不是跨重启或成功后重复请求的持久幂等键**。独立的后续点击仍可再启动一次。Deadline 为调用者的等待期限；单个调用者到期不取消其他订阅者。共享应用操作另有默认15秒期限；超远未来期限不会造成计时器参数溢出。

## 3. 结构化结果

`ActionResult` 包含 RequestId / TraceId / Source / ActionId / ModuleId / ExecutionId / Status / Message / ErrorCode / Data / Evidence / Retryable。

- succeeded：本机应用 Data 为 `{applicationId, accepted:true}`，Evidence=accepted。只表示系统调用未报告失败，既不证明新进程一定创建，也不证明界面已就绪；Shell 可将请求交给已有进程而不返回新句柄。
- failed：无成功证据，ErrorCode 区分 invalid_request、invalid_action、module_disabled、module_removed、invalid_configuration、launch_failed、dispatcher_closed 等。不会自动重放。
- cancelled：无成功证据，区分用户/调用者取消、timeout、module_disabled、configuration_changed、dispatcher_closed。停止等待不是撤销已提交的系统调用。
- 旧模块结果：文字保留，Evidence=unknown；不根据“已完成”等中文词猜测目标状态。内部异常只为旧调用者保留原异常类型，JSON 序列化忽略该异常对象。

同一次共享操作的 ExecutionId 相同，但每个订阅者有自己的 RequestId、TraceId、来源和结果。调用者可取消，系统原生调用可能仍在收尾；此时继续点击不会另起同 ID 的并行启动。真正返回后才释放占用。

## 4. 生命周期与历史

配置提交仍用 ApplySettings，随后 CancelDisabled 和现有快捷入口刷新。停用/移除拒绝新动作，取消在途等待；更换路径、参数、管理员设置或名称同样使旧配置任务失效。不会退出用户的软件，不会删除应用记录、图标或个人数据。

启动前、依赖修复后及完成后核对当前配置，避免迟到任务使用过期路径。路径修复经原对话框确认后更新本次配置快照。只有接受且未取消、配置仍有效的操作写一次历史；历史写入失败不会把已接受的启动变成可自动重试的启动失败。

关闭聊天会取消其拥有的订阅；如果其他入口仍在等待，共享工作继续。旧组合的统一口令保留来源和取消 token。未迁移业务的首次调用者取消与合并语义保持兼容，不宣称已全部采用新的订阅模型。

## 5. 计数与隐私

`ActionMetrics`：Requests 是收到的请求数（含拒绝），Executions 是创建的执行尝试数，Coalesced 是复用在途执行的请求数；Succeeded/Failed/Cancelled 为每个请求结果数，TimedOut 是取消中的超时子集。Executions **不是实际启动进程数或全局网络请求数**。

结果/旧回执各最多60条，只在内存中；计数不包含请求参数、路径、Key、聊天正文或剪贴板。界面仍使用原有安静反馈和错误处理，不为成功启动额外弹通知。未来诊断导出需另行定义字段白名单，本批不导出运行中的个人结果。

## 6. 保留项

LeoActions.Names 的63个旧 ID、ModuleCatalog 的旧归属、LeoModule/1、旧配置、热键、组合、使用方案和入口模式都保留。归一后的实际执行归属 local:<ID>，不借此改动旧工具枚举。其他模块通过 legacy-adapter 共用同一分发器，超时/幂等/副作用等完整策略仍按后续批次迁移，不伪装已经实现。

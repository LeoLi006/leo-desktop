# C04：宿主周期任务接口

## 契约

IPeriodicTask定义Enabled、Running、Configure(bool)、RunNowAsync、Retry、Dispose，限UI线程调用。原OwnedActivityTimer直接实现契约，原有用户保持兼容；本轮没有再包一层实现。IWindowPeriodicTask继承该契约，WindowPeriodicTask继续提供窗体销毁兜底，宿主任务不绑定业务窗口可见性。

Enabled表示目前允许接受新工作，故障暂停、停用和销毁时为false。Running表示仍有未结束的回调；任务忽略取消时，即使Enabled已为false，Running也可能为true。关闭不等于已经完成所有外部工作，不允许假报释放。状态只供观察，调用方仍通过RunNowAsync执行，不能依赖先检查状态来防重。

Configure重复同一值不增挂计时器；运行时重复触发合并，停用取消令牌，重新启用后等待旧工作结束再运行新代次。错误沿原FeatureFaults隔离及重试，停用/销毁后的旧重试不得恢复任务。所有数据写回仍由原业务检查取消和代次。

## 归属与接入

| 所有者字段 | 周期 | 关闭业务窗口 | 停用/退出 |
| --- | --- | --- | --- |
| PetForm.reminderActivity | 15000ms | 清单关闭后继续提醒 | 退出先释放；不新增停用开关 |
| PetForm.matchSyncActivity | 600000ms | 足球窗口关闭不停止宿主校时 | 足球停用取消，快速重启沿原代次规则；退出先释放 |
| ClipboardHistoryService.monitor | 450ms | 历史窗口关闭不停止采集 | 按历史开关启停；服务退出解除隐私事件并释放 |

字段类型使用IPeriodicTask，实例仍为OwnedActivityTimer。实际编译样例见MatchSync.cs和ClipboardTool.cs：

```csharp
IPeriodicTask task = new OwnedActivityTimer(
    timer, "module-host", "后台检查", "check", async token => {
        var result = await Query(token);
        token.ThrowIfCancellationRequested();
        Apply(result); // 原业务继续负责目标、模块和请求代次校验
    });
task.Configure(moduleEnabled);
await task.RunNowAsync();
task.Dispose(); // 宿主释放，不依赖业务窗口是否可见
```

原参数、任务类型、活动数量摘要、首次检查、缓存处理、敏感保护和数据格式不变。没有修改提醒、足球数据写回、实际剪贴板读取算法。其他OwnedActivityTimer使用者本轮不改字段，仍兼容同一实现；全部后台活动迁移不在本批范围。

## 验证

扩展已有 `--r02-host-test` 至44项，以IPeriodicTask执行模拟周期工作，验证Enabled/Running、取消收尾、重复启用/触发、失败与过期重试、三个生产字段类型及原周期。生产宿主夹具检查关闭清单保留提醒、足球停用/快速启用、旧比赛时间丢弃、独立故障、退出先取消及晚回调不写入；剪贴板使用模拟采集。

`--window-periodic-test` 19项验证窗口接口继承兼容及原生命周期，共63项通过。宿主30轮启停全部活动释放、句柄增量0，只记录必要资源变化，不追求极限指标。锁定构建零警告。

仅模拟任务、隔离数据，无真实Key、麦克风、剪贴板正文读取、联网赛程请求或外部应用启动。不新增业务、不打包、不上传GitHub。本轮核验无新增业务缺陷，因此不额外修改原执行算法。

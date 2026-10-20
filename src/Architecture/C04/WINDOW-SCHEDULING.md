# C04：窗口周期任务最小试点

## 接口与归属

IWindowPeriodicTask提供Configure(bool)、RunNowAsync、Retry和Dispose。WindowPeriodicTask将窗体生命周期绑定到已有OwnedActivityTimer，继续使用ActivityOwner/ActivityLease登记周期与在途工作；没有新增线程、后台全局调度器或模块代码执行机制。

必须在UI线程创建和调用；WinForms.Timer的Tick也在该线程执行。传入的计时器交给服务独占挂接与释放，不应另加Tick处理或自行启动。调用方决定何时启停，不自动根据Visible停用所有窗口任务。

回调接收取消令牌。异步读取后、写回UI前，业务必须检查令牌及自身代次；服务前后检查不能撤销已发生的UI修改或外部操作。媒体继续沿用原ActivityLease与refreshGeneration校验。RunNowAsync在同一任务运行时合并触发，不承诺为每次点击补跑。

## 接入样例

```csharp
IWindowPeriodicTask schedule = new WindowPeriodicTask(
    window, timer, "module-window", "窗口刷新", "window-refresh",
    async token => {
        var rows = await Query(token);
        token.ThrowIfCancellationRequested();
        Render(rows); // 业务仍需校验自己的请求代次
    });
schedule.Configure(true);
// 按窗口原有策略隐藏时可停用，也可继续；关闭时释放。
schedule.Dispose();
```

MediaForm与ClipboardTool.Open是实际可编译接入样例。媒体仍每5000毫秒轮询；显示时即时刷新，隐藏时停止周期并重置原请求活动。剪贴板历史在Shown启动900毫秒刷新，隐藏继续运行，关闭或销毁停止；宿主采集监听不属于历史窗口。

## 取消、错误与兼容

重复Configure(true)不重复挂接或增加轮询租约。Configure(false)停止计时并取消在途任务；重新启用建立新代次。忽略取消的工作返回前仍算取消收尾，不能假报已经释放。原OwnedActivityTimer负责最后的代次核验和恢复新代次工作。

可恢复异常只暂停出错的周期任务，并沿用FeatureFaults原重试入口；其他任务互不共用失败标记。媒体业务原有“刷新重试”提示和客户端执行策略保留。正常取消不报错。重复Dispose安全，窗体Disposed有兜底释放；窗口原清理入口仍主动先释放服务。

本轮只迁移这两个周期任务；宿主监听、提醒、索引、媒体队列及其他OwnedActivityTimer用户保留原路径。现有运行检查的活动摘要增加“剪贴板历史窗口”“轮询检查”“窗口刷新”类型名称，不记录业务内容。

## 定向验证

`--window-periodic-test <隔离报告绝对路径>`：18项，覆盖重复启用、重复Tick合并、取消/重启代次、销毁后旧结果、独立故障和显式重试、剪贴板隐藏继续刷新/关闭保留监听、媒体周期与隐藏停止，以及30轮生命周期释放。

活动生命周期42项、客户端46项、音乐剪贴板54项，共160项通过。专项30轮活动归零、句柄增量0；短测资源只写忽略的构建报告，不宣称长期无泄漏。测试使用模拟任务、隔离历史及模拟采集，不访问真实Key、麦克风或剪贴板正文，不启动外部客户端。不打包、不上传GitHub。


## 宿主接口收敛增量

后续批次将公共操作及Enabled/Running收敛到[IPeriodicTask](HOST-SCHEDULING.md)，IWindowPeriodicTask继承它；窗口绑定和各自显示策略仍保留。本文试点历史数量不替代后续专项报告。

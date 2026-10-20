# C04：当前周期任务清册

## 接口和资源

业务字段通过IPeriodicTask调用Configure、RunNowAsync、Retry及查询Enabled/Running，直接实例化原OwnedActivityTimer；只有窗口销毁适配器WindowPeriodicTask内部保留具体实现字段。IWindowPeriodicTask继承公共契约，不增加另一套执行算法。

各任务仍在UI线程创建和驱动，沿用原活动登记、取消、代次与故障策略。没有新全局容器、计时器、扫描线程或后台服务。Enabled为false不保证Running立即false；忽略取消的回调仍计入收尾，直到实际返回。异步写回仍由业务校验令牌、代次及目标状态。

## 已接入周期任务

| 活动/字段所有者 | 周期 | 开关及隐藏/关闭规则 | 释放与故障 |
| --- | --- | --- | --- |
| reminder-scheduler / PetForm.reminderActivity | 15秒 | 核心宿主提醒；清单窗口关闭后继续 | 宿主退出先释放，原提醒错误策略保留 |
| match-sync / PetForm.matchSyncActivity | 10分钟 | 足球模块控制；足球窗口关闭后继续 | 停用/退出取消；旧校时不能改写清单，独立重试 |
| clipboard-monitor / ClipboardHistoryService.monitor | 450毫秒 | 历史开关控制；历史窗口关闭后继续 | 服务销毁取消并解除隐私事件；保留敏感内容保护 |
| media-window / MediaForm.polling | 5秒 | 可见时启用并即时刷新；隐藏停止 | 窗体销毁或模块停用时释放，客户端队列另有归属 |
| clipboard-window / 历史窗口局部schedule | 900毫秒 | Shown启用，隐藏继续，关闭停止 | Release释放窗口刷新，不释放共享监听 |
| application-context / LauncherForm.observationActivity | 1500毫秒 | 索引和上下文开关均开启；搜索隐藏仍继续，Open共用立即执行入口 | 关任一开关取消；销毁解除；故障不由设置重复应用暗中重启 |
| update-check / UpdateNotifications.activity | 启用后30秒首次，之后60秒轮询每日截止时间 | 自动提醒开关控制；业务窗口关闭不影响；重复配置不重置截止时间 | 停用/退出取消旧请求；业务捕获接口失败并静默等待，不下载包 |
| focus-host / PetForm.companionActivity | 1秒 | 宿主计时；专注窗口关闭不停止会话 | 退出释放，故障只暂停自身，旧重试不得复活 |
| focus-window / FocusForm.displayActivity | 1秒 | 只在窗口可见时刷新；隐藏即停 | 窗口销毁释放；不调用共享FocusSession.Stop |

预览/测试开关沿原规则保留，不让测试模式暗中启动生产后台。自动更新“有结果”与“发现未提醒过的新版本”的判断仍由原业务分别负责；本轮没有改提醒去重或失败节奏。

## 保留的边界

应用索引扫描是受ActivityOwner管理的STA工作，不是周期任务，不迁入IPeriodicTask。动画、拖动、输入防抖、流式文本渲染及主题通知也不因名称含Timer就强行接入。本清册只描述上述9类，不代表全部宿主活动已经统一。

旧OwnedActivityTimer类型仍有构造与适配器调用；保留它作为实现，不复制算法、不删兼容路径。窗口是否隐藏、模块是否停用及宿主是否退出仍是不同事件，不能统一成全部停止。

## 本批验证与交付

只扩展并运行 `--r02-resident-test <隔离报告绝对路径>`：54项。新增四个生产字段的契约和原周期/状态检查；原夹具覆盖重复配置、开关、独立失败、旧重试、退出、取消后更新返回、首次延迟及每日截止时间、专注窗口关闭后宿主会话继续。索引的直接关联夹具继续使用模拟扫描。

20轮模拟自动更新启停后活动归零、句柄428→428；资源读数保存在忽略的.build/resident-periodic，不作长期性能保证。锁定构建零警告。本轮未确认新增业务缺陷，因此只做字段接口收敛，不改执行算法、不重启正在运行的Leo。

仅隔离数据及模拟提供者，无真实Key、麦克风、剪贴板正文、更新联网/下载或外部应用启动。只本地提交、开发说明，不打包、不上传GitHub。完整C04及通用资源容器仍有剩余范围。

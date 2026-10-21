# Leo v1.03.0 性能与鲁棒性记录

日期：2026-10-19。范围：用户批准的性能基线、按需主题挂接、绘制复用、任务生命周期、原子保存与定向验证。没有增加业务功能，没有引入第三方依赖。

## 测量方法与限制

在同一机器、同一 .NET 9.0.6 测试宿主中，先运行未优化的 v1.02.1 代码加测量入口，再运行优化代码。使用独立预览进程、假数据、关闭本机应用索引及联网任务；每场景预热后采样 6 秒。没有读取个人清单、API Key 或剪贴板。

- CPU 指进程消耗的 CPU 时间，不是整机 CPU 百分比。
- 分配量指采样期间累计托管分配，不等于常驻内存。
- “首次显示”测桌宠构造到首次 Show，不是 Windows 登录启动或磁盘冷启动。
- 吸附压力场景由测试循环约每 5 ms 手动推进吸附检测，主动制造重复更新；生产环境使用计时器。因此结果用于比较同一压力负载，不代表日常 CPU 占用比例。
- 安装包自带运行时 9.0.21；以下数字来自上述测试宿主，不能直接当作发布包在所有电脑上的表现。
- 短采样受系统负载和计时精度影响，不能证明长期稳定性或给出普遍性能保证。

原始报告在本地 `.build/robustness/baseline.json`、`optimized-final.json`；不包含个人数据，未随源码上传。

## 前后对比

| 场景（6 秒） | 优化前累计分配 | 优化后累计分配 | 优化前 CPU 时间 | 优化后 CPU 时间 |
| --- | ---: | ---: | ---: | ---: |
| 普通待机 | 9.65 MB | 0.055 MB | 203 ms | 219 ms |
| 静止吸附压力 | 99.95 MB | 1.44 MB | 1438 ms | 281 ms |
| 全屏隐藏 | 0.026 MB | 0.034 MB | 16 ms | 47 ms |
| 清单与通知窗口打开 | 9.88 MB | 0.050 MB | 203 ms | 203 ms |

普通待机保持约 42～43 帧/6秒；吸附完成后的重复绘制从压力样本中的 430 次降为 0 次。程序仍保留显示变化、展开与鼠标响应所需的更新。

普通待机样本的私有内存从约 53.0 MB 降至 41.5 MB，但这只是该样本结束时的进程值。GDI 资源从 40 个变为 42 个，这是保留一组绘制资源的预期代价，测试期间数量稳定。

首次显示样本为 354 ms → 316 ms。便签打开时间的中位数约 46 ms → 60 ms，存在波动，本轮**不宣称窗口打开更快**。隐藏场景原本没有实际绘制，CPU 数字很小且波动明显，也不宣称隐藏 CPU 已测得改善。

主要可信收益是减少逐帧大数组分配、消除静止吸附重复绘制；后续若要判断启动或窗口速度，需更多重复样本和真实使用环境测量。

## 实现变化

### 主题与绘制

- 滚动条首次挂接后由新控件加入、句柄创建/销毁事件维护，不再在每次空闲时重复遍历完整控件树。
- 保留主题切换时的主动刷新；单个滚动条绘制异常只停用该控件的自绘，避免持续触发整窗异常。
- 每个桌宠持有一组绘制位图、原生 DIB/DC 和像素缓冲；尺寸变化时重建，退出时释放。
- 位图每次先清空透明背景，透明边缘清理使用有归还路径的缓冲池；确保复用不残留上一帧。
- 吸附静止后不推进普通待机帧，静态吸附不再重复提交相同画面；吸附过渡保持高频，静态检测降低频率。
- 全屏隐藏时停止动画、吸附和悬停计时器；恢复后重新启动所需活动。

### 后台生命周期

- 停用模块取消其正在执行的统一动作；取消后即使旧工作返回，也不报告成功。
- 退出 Leo 时取消客户端操作队列，不再发送迟到反馈；只停止 Leo 自己拥有的短期辅助进程，不关闭 QQ 音乐等用户应用。
- 聊天直接 Dispose 与正常关闭均取消请求、停止录音和绘制计时器；迟到的本地结果不再修改已释放界面。
- API 设置、运行检查、更新、音乐窗口的释放流程可重复执行。
- 扩展退出清理使用有界等待，避免后代进程持有输出管道时无限等待。

### 数据保护

- 核心设置、API 设置、清单与便签采用同目录唯一临时文件、完整写入和刷盘，然后原子替换；保留前一个完整版本为 `.bak`。
- 清单保留现有最新完整副本与日常备份；读取时先验证主文件，再尝试可用备份。
- 恢复前保留损坏原件为带时间与唯一标识的 `.corrupt-*` 文件，不覆盖有效备份。
- 没有可用清单备份时禁止写入损坏文件；便签读取失败通过功能错误反馈保留原件；设置不能恢复时提示已临时使用默认配置。
- 成功落盘后，单个清单视图刷新失败不会把已保存操作当作失败。
- 备份保存在本机，API Key 继续使用原有本机加密格式；备份不进入发布包。

这不是跨多个文件的数据库事务，也不保证在硬件故障、文件系统损坏或所有备份均损坏时恢复。文件替换失败会保留原文件并反馈失败。

## 复现入口

`src/Test-Robustness.ps1` 只运行本轮及直接影响的检查；`-Measure` 额外进行性能采样，`-Soak` 额外运行持续操作检查。报告和生成的辅助进程夹具位于 `.build/robustness`。

性能与持续检查在隔离预览中运行，真实网络、代理切换、音频识别和外部应用的长时间行为不在本轮压力结论范围内。

## 本轮持续与故障检查结果

持续检查 180.12 秒，共 812 轮；从预热后第 30 秒到结束，GDI 始终 74，USER 句柄始终 24；托管堆从 1,393,440 字节到 1,394,552 字节，中间在约 1.32～1.41 MB 波动，未持续增长。私有内存在约 56.9～59.0 MB 波动。该检查不会调用实际网络和外部应用。

故障与直接影响检查覆盖写入中断、文件锁、无效 JSON、并发保存、损坏与缺失主文件、备份恢复、内存回滚、视图观察者失败、模块停用与退出取消、关闭后的迟到结果、缓冲复用像素一致、大小变化与隐藏恢复、透明边缘、四向吸附、通知点击、方案切换及扩展进程取消/超时。只运行相关检查，没有执行全量测试。

定向检查合计 **287 项通过**：37 项本轮故障/保存/生命周期，28 项通知交互，44 项吸附，39 项便签与窗口交互，36 项方案，59 项统一动作与扩展进程，6 项客户端队列，17 项清单，17 项透明边缘，4 项聊天和音乐失败恢复。持续检查另有两项资源增长约束通过。没有运行未受影响的全量测试。


---

# Verification and performance — English

The Chinese table above is historical evidence from the1.03.0 optimization on2026-10-19, not a benchmark of every current build. Both sides used the same .NET9.0.6 isolated host, fake data, disabled app indexing/network work, warmup and six-second samples. The release runtime differed. CPU is process CPU time; allocations are cumulative managed bytes, not resident memory. First-show time is not login/cold startup. The docking stress loop deliberately advanced work roughly every5ms and is not representative idle load.

| Six-second scenario | Allocations before→after | CPU time before→after |
| --- | --- | --- |
| Normal idle |9.65MB→0.055MB |203ms→219ms |
| Static docking stress |99.95MB→1.44MB |1438ms→281ms |
| Fullscreen hidden |0.026MB→0.034MB |16ms→47ms |
| Planner/log windows open |9.88MB→0.050MB |203ms→203ms |

The credible gains are lower repeated allocations and elimination of duplicate static-docking submissions, not a universal CPU/window-speed claim. Idle remained about42–43 frames/6s. Stress duplicate submissions fell430→0. One idle endpoint private-memory sample was53.0→41.5MB; GDI40→42 reflects retained reusable drawing resources. First show354→316ms is one sample; note opening46→60ms did not establish a speedup. Small hidden CPU values were noisy.

An isolated180.12-second/812-round soak had stable GDI74 and USER24 handles after warmup, managed heap roughly1.32–1.41MB and private memory56.9–59.0MB. Its287 directly related checks covered persistence/recovery, lifecycle, interaction, docking, profiles, execution/worker cancellation, queues, planner and rendering. Two soak bounds also passed. These are historical scoped results, not a newly rerun full suite or proof against long-term leaks.

## Implementation principles

Attach theme/scrollbar hooks on demand rather than traversing entire control trees at every idle event. Reuse bitmap/native DIB/DC/pixel buffers, clear transparent pixels each frame, rebuild on size changes and release on exit. Stop animation while fully hidden; retain interactive transitions. Own/cancel background work and discard stale generations without closing external user applications.

Core settings/API/tasks/notes use unique same-directory temporary files, full writes/flush and atomic replacement with prior complete backups. Recovery preserves corrupt originals. Failed saves do not publish an uncommitted snapshot; a failed view callback does not undo an already committed write. This is not a multi-file transaction or a guarantee against hardware failure/all backups being corrupt.

## Later stability and language evidence

A later approximately five-minute isolated stability check did not resolve a private-memory high-water observation. It is neither proof of leakage nor proof of absence. The agreed next step is everyday observation, not repeated tuning without evidence. No multi-day real client/network qualification is claimed.

At42fea41, related checks passed: language28, chat resize9, conversation41, desktop tools58, entry display109, profiles36, themes65(total346), build0 warnings/errors. These are different scoped runs and must not be added to historical counts as a new full-suite number. Chat checks simulate resizing/reopening/native-child repaint without idle redraw loops; real driver/DPI combinations still need feedback.

## Reproduction and reporting

Use `src/Test-Robustness.ps1`; `-Measure` adds sampling and `-Soak` adds sustained isolated operations. Inspect relevant test scripts before invoking them. Examples for newer work include `--ui-language-test` and `--chat-resize-test`; report paths must be absolute and point inside ignored .build. Do not read real personal data, call paid model APIs or start real clients for default fixture runs.

Run only checks directly affected by a change. Record scenario, commit, runtime, duration, warmup and metrics, and distinguish simulation from real use. Original raw reports stay local/ignored; do not upload them as personal diagnostics. This documentation-only consolidation does not rerun runtime tests or install a new build.

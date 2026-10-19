# Q2 与 Q1 的定向性能比较

相同隔离预览方法，各3个新进程；不代表真实软件启动速度，也不代表个人配置负载。调查门槛沿用 Q1：max(基线中位数20%，基线样本极差)。

| 指标 | Q1 中位数 | Q2 中位数 | 超过预定调查门槛 |
| --- | --- | --- | --- |
| construction_to_show_ms | 334.788 | 711.327 | 是 |
| quick_note_show_ms | 102.868 | 115.385 | 否 |
| module_disable_cancel_ms | 14.192 | 0.280 | 否 |
| idle/cpu_one_core_percent | 5.460 | 6.431 | 否 |
| idle/allocated_bytes | 70376.000 | 71096.000 | 否 |
| idle/private_bytes_after | 39079936.000 | 39157760.000 | 否 |
| idle/pump_gap_p95_ms | 30.896 | 27.077 | 否 |
| docked/cpu_one_core_percent | 0.195 | 0.390 | 否 |
| docked/allocated_bytes | 41000.000 | 41000.000 | 否 |
| docked/private_bytes_after | 70574080.000 | 71249920.000 | 否 |
| docked/pump_gap_p95_ms | 21.522 | 25.630 | 否 |
| hidden/cpu_one_core_percent | 0.390 | 0.000 | 否 |
| hidden/allocated_bytes | 41000.000 | 41000.000 | 否 |
| hidden/private_bytes_after | 70574080.000 | 71176192.000 | 否 |
| hidden/pump_gap_p95_ms | 28.881 | 27.242 | 否 |
| windows/cpu_one_core_percent | 5.662 | 6.831 | 否 |
| windows/allocated_bytes | 82120.000 | 81880.000 | 否 |
| windows/private_bytes_after | 71946240.000 | 73265152.000 | 否 |
| windows/pump_gap_p95_ms | 31.632 | 26.928 | 否 |

句柄、GDI/USER 及绘制帧数见 comparison.json；短时采样不证明长期无泄漏。CPU 为单核百分比，内存/分配单位为字节，其他耗时单位为毫秒。

## 首次显示的同场新旧控制

历史首次显示项仍保留越限标记。使用未改动的 --performance-test 对安装中的旧 DLL 与新 DLL 交替采样各3次，显式使用同一运行时/依赖，校验关键图集一致。旧版中位数 711.133 ms，新版 630.119 ms，预定同场调查门槛 152.015 ms。

同场新版本回归：未复现。历史绝对耗时变化仍需后续跟踪；不据此宣称已找到系统环境变化的具体原因，也不将这一组较短方法与 Q1 原始数值直接合并。

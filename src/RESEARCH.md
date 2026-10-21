# 相关工作与复用原则 / Related work and reuse principles

这是2026-10-19第一轮调研结论的归并，不是2026-10-21重新验证的依赖选型或性能比较。候选尚未因列在这里而引入项目。
This consolidates the initial2026-10-19 research, not a newly verified dependency selection or benchmark. Listing a candidate does not mean it is integrated.

| 方向 / Area | 调研对象 / Candidates | 当前取舍 / Current approach |
| --- | --- | --- |
| 搜索 / Search | Flow Launcher、Everything | 借鉴提供者/排序，保留Leo宿主和现有搜索 / Study provider/ranking ideas, retain Leo's host/search |
| 扩展 / Extensions | PowerToys Command Palette、MCP C# SDK | 借鉴发现/进程边界，不声称协议兼容 / Study discovery/process boundaries, no compatibility claim |
| 可靠组合 / Reliable routines | Polly、Workflow Core、Elsa | 保留小型顺序执行器，避免为简单组合引入完整引擎 / Keep the small sequential executor rather than a full engine for simple routines |
| 模型适配 / Model adaptation | Microsoft.Extensions.AI | 先保证现有中转兼容和受控调用 / Prioritize existing relay compatibility and controlled invocation |
| 安装更新 / Install/update | Velopack | 单独评估迁移与恢复成本，不自动替换 / Evaluate migration/recovery separately, no automatic replacement |
| 桌宠交互 / Companion interaction | VPet | 参考状态与打断设计，保留素材和宿主 / Study state/interrupt handling, retain Leo assets/host |

选型先证明具体缺口，再核对当前官方源码、许可证、平台、维护状态和可迁移成本，最后做隔离原型。不可把项目宣传或星数当Leo性能证据；不可整包照搬而忽略版权与依赖。
Start with a concrete gap, then check current official code, licenses, platform/support and migration cost, then build an isolated prototype. Popularity and project marketing are not Leo performance evidence. Reuse must respect licensing and dependency boundaries.

过去调研已转化为统一执行、限定服务、生命周期和受控扩展方向；实际交付与限制以[架构](Architecture/README.md)和[路线图](ROADMAP.md)为准。未来有需求再复查候选，不为列更多框架继续重构。
The research informed unified execution, scoped services, lifecycle and controlled extensions. Actual delivery/limits are documented in [architecture](Architecture/README.md) and [roadmap](ROADMAP.md). Reassess candidates for a real need, rather than refactor just to add frameworks.

# 路线图 / Roadmap

更新于2026-10-21，基于主分支42fea41。目标：稳定、高效、按需配置的桌面动作中枢；不以持续增加功能数量代替质量。
Updated2026-10-21, based on main42fea41. Goal: a stable, efficient, configurable desktop action hub. Feature count does not replace quality.

本页以当前结果归并历史批次，不把试点等同完整交付。旧任务与批准记录可查Git历史。后续实施前确认具体范围；文档更新不自动发布安装包。
This page consolidates historical batches by current outcome. A pilot is not full delivery. Previous tasks remain in Git history. Agree on scope before implementation; documentation changes do not automatically publish binaries.

## 已交付范围 / Delivered scope

- [x] 统一动作注册、参数适配、结构化结果、取消和旧编号兼容；主要既有能力迁移。Unified action registration/adaptation/results/cancellation and old-ID compatibility; major existing capabilities migrated.
- [x] 普通内部模块的限定服务、契约、搜索/菜单/快捷栏贡献及停用移除闭环。Scoped services, contracts, search/menu/button contributions and disable/remove lifecycle for ordinary internal modules.
- [x] 配置/依赖、窗口/反馈、主题快照订阅和周期任务归属的已接入服务。Integrated configuration/dependencies, presentation/feedback, theme subscriptions and owned periodic tasks.
- [x] 查询快照/草稿/提交边界核对及比赛→清单、便签→待办两条链路验收。Snapshot/draft/commit checks and match→task/note→task handoff verification.
- [x] 现有应用包和LeoModule/1扩展兼容、更新恢复、状态及分享隐私核验。Existing app/LeoModule/1 package compatibility, recovery, status and sharing-boundary checks.
- [x] 近期改动定向稳定性短测，保留限制，不追求极限指标。Targeted short stability checks with explicit limits, not extreme optimization.
- [x] 可选只读电脑状态、已有邮件入口、图标库、聊天缩放重绘与中英文界面。Optional read-only computer status, existing mail-client entry, icon library, chat resize repainting and Chinese–English UI.
- [x] 公开说明归并、中英双文、更新主分支说明。Consolidated bilingual public documentation aligned with main.

## 继续观察 / Ongoing observation

- [ ] 日常运行数天的窗口、后台任务、内存与句柄趋势；私有内存高位暂不能确认为泄漏，也不能宣称已解释。Observe windows/background activity/memory/handles over real multi-day use; private-memory high-water is not proven leakage or fully explained.
- [ ] 不同DPI、显示器、驱动和真实流式回复的聊天缩放表现。Chat resize behavior across real DPI/monitor/driver combinations and streaming content.
- [ ] 外部数据源变动、Clash/QQ/PotPlayer真实客户端时序与登录启动。Changing data sources, real client readiness and login startup behavior.
- [ ] 英文长文本、未知扩展与自定义内容边界的用户反馈。Feedback on long English labels, unknown extensions and user-content boundaries.

验收以可复现问题为准；没有证据不反复扩大测试或继续拆接口。针对已确认问题补检查，避免真实Key和个人数据默认进入夹具。
Use reproducible issues as evidence. Do not broaden tests or split interfaces without a concrete need. Add focused checks for confirmed defects; keep real keys and personal data out of fixtures.

## 未完成目标 / Remaining goals

| 优先条件 / Trigger | 工作 / Work | 完成标准 / Acceptance |
| --- | --- | --- |
| 实际故障 / Concrete defect | 修复跨入口、保存、停用/退出和迟到回调 / Fix entry, persistence, disable/exit and late-result failures | 先复现、后最小修复、直接相关检查通过 / Reproduce, minimal fix, relevant checks pass |
| 有真实新模块需求 / Real new-module need | 核对现有契约/服务能否接入 / Validate current contract/service reuse | 无新增业务分支或解释必要组装点 / Reuse generic entry paths or document necessary composition |
| 决定对外SDK / External SDK decision | 版本协商、权限/信任、独立交付策略 / Version negotiation, permissions/trust and independent delivery | 明确兼容、权限、失败隔离与恢复，不宣称当前已有 / Specify compatibility, permissions, isolation and recovery; not delivered today |
| 已有组合不足 / Existing routines insufficient | 按具体场景补有界组合能力 / Extend bounded routines for a concrete case | 结果证据、取消、重试和未知状态明确 / Explicit evidence, cancel/retry and unknown-state handling |
| 决定正式发布 / Explicit release decision | 干净包、升级恢复、版本与截图 / Clean package, upgrade recovery, version/screenshots | 无个人数据、可校验、文档和二进制对应 / No personal data, verifiable payload, matching docs/binaries |

没有承诺在线市场、任意代码热加载、完整沙箱、所有窗口声明化、通用并行补偿或全局事务。这些应单独评估，不是剩余小修。
No online marketplace, arbitrary-code hot loading, full sandbox, fully declarative UI, general parallel compensation or global transaction is promised. These require separate decisions rather than minor cleanup.

## 执行规则 / Working rules

每批先明确范围和批准，再实现、只验证直接影响、更新开发说明并本地提交。是否本地更新取决于运行变化；GitHub上传、安装包和Release按明确授权单独执行。
Define/approve each batch, implement, validate direct effects, update development notes and commit locally. Update the local program only for runtime changes; GitHub upload, packaging and releases require their own explicit scope.

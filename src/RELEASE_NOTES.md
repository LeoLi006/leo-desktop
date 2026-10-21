# 更新记录 / Change log

## v1.05.0 — 2026-10-22

本次将v1.00.0之后的主分支改进集中发布为Windows x64安装包，附SHA-256校验文件。
This Windows x64 release consolidates main changes since v1.00.0 and includes a SHA-256 checksum.

### 安装与兼容 / Installation and compatibility

完整解压，运行Install-Leo.cmd，更新时选择原安装目录；发布清单校验、更新前备份和回退沿用现有安装器。包内包含Windows桌面运行时，不含个人配置、Key、语音模型或第三方客户端。
Extract everything and run Install-Leo.cmd. For updates select the existing installation. The installer verifies the manifest, backs up and supports rollback. The bundled Windows desktop runtime needs no development environment; personal settings, keys, speech models and third-party clients are excluded.

### 最近变化 / Recent changes

- `48df4ea`：可选只读电脑状态和现有邮件客户端入口，默认关闭；火绒仅快速打开。Optional read-only computer status and existing mail-client launch, off by default; Huorong launch only.
- `874b78f`：比赛后台元数据按相关事项筛选、去重和分批，避免大缓存超过动作输入上限；聊天文字重排、18种图标及电脑状态布局改进。Filter/deduplicate/chunk relevant match metadata to avoid oversized actions; chat reflow,18 icons and status-window improvements.
- `6e4bca5`：聊天缩放/还原后合并布局和原生子窗口重绘，避免空闲循环刷新。Coalesced layout/native-child repaint after chat resize/restore without idle polling.
- `42fea41`：简体中文/英文界面、本地资源、设置入口、动态控件与用户内容保护。Chinese–English UI using local resources, settings, dynamic bindings and user-content protection.
- 文档归并为中英双文指南；安装包附本地说明，源码接口链接指向本版本标签。Consolidated bilingual guides are included; source-interface links target this release tag.

### 此前主分支底座 / Earlier main foundations

统一执行、契约、入口贡献、限定宿主服务和后台活动归属；清单/足球/便签/媒体等主要能力迁移；有界模型计划和组合恢复；旧扩展兼容检查、更新回退、状态及分享隐私核验。详细边界见[架构](Architecture/README.md)及[路线图](ROADMAP.md)。
Unified execution/contracts/entry contributions, scoped host services and owned background work; migrated core capabilities; bounded model plans and routine recovery; extension compatibility, rollback, status and sharing checks. See [architecture](Architecture/README.md) and [roadmap](ROADMAP.md) for limits.

通知成功记录使用安静日志；错误和提醒保留必要反馈。修复搜索/右键设置间切换，并提供自定义队徽和清单话语。旧逐轮记录改由Git提交保存，不在当前说明重复累加测试数。
Routine success uses quiet logs; errors/reminders retain necessary feedback. Earlier changes fixed search/context-settings interactions and added custom badges/planner quotes. Git preserves per-batch history; old check counts are not accumulated into a current full-suite claim.

## 已有发布v1.00.0 / Existing releasev1.00.0

[发布页 / Release page](https://github.com/LeoLi006/leo-desktop/releases/tag/v1.00.0)

当时集中提供统一入口、应用模块与进程扩展、快捷栏排序/溢出、路径补全、媒体控制、独立主题和皮肤分享、拖放动作及便签。以该标签对应源码与安装包为准，不包含所有后续主分支功能。
That release introduced unified entry routing, app modules/worker extensions, shortcut ordering/overflow, path completion, media controls, independent themes/skin sharing, drop actions and notes. Use its tagged source and binary as the authority; later main changes are not all included.

手动下载选择Leo-v1.00.0-win-x64.zip；v1.0.0同内容兼容附件供旧更新器识别。历史截图使用演示数据，不代表当前比赛。
For manual downloads choose Leo-v1.00.0-win-x64.zip; the equivalent v1.0.0-named asset supports older updaters. Historical screenshots use demo data, not current fixtures.

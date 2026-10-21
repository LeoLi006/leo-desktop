# Leo · 桌面伙伴与本地动作中枢 / Desktop companion and local action hub

把应用、清单、便签、足球、音乐和本地操作放进一个可自定义的 Windows 桌面伙伴，按需启用功能。
Bring apps, tasks, notes, football, music and local actions into a customizable Windows companion. Enable only what you need.

**最新发布 / Latest release：v1.05.0 · Windows x64。** 包含中英文界面、当前模块底座和后续修复，安装包附SHA-256校验。Includes Chinese–English UI, the current module foundation and later fixes, with a SHA-256 checksum.

[下载 / Download](https://github.com/LeoLi006/leo-desktop/releases/tag/v1.05.0) · [问题反馈 / Issues](https://github.com/LeoLi006/leo-desktop/issues)

## 文档 / Documentation

以下公开说明均提供中英文。历史批次记录已归并，旧版原文仍可通过 Git 历史查看。
All public guides below include Chinese and English. Repeated batch reports have been consolidated; previous revisions remain in Git history.

| 文档 / Guide | 内容 / Contents |
| --- | --- |
| [使用手册 / User guide](src/USER_GUIDE.md) | 安装、操作、设置、故障排查 / Setup, everyday use, settings, troubleshooting |
| [产品与开发指南 / Product and developer guide](src/PRODUCT_AND_DEVELOPER_GUIDE.md) | 功能、架构、接口、扩展要求 / Features, architecture, interfaces, extension requirements |
| [模块与包协议 / Modules and packages](src/MODULES.md) | 本机应用、JSON、ZIP、stdio及恢复 / Local apps, JSON, ZIP, stdio and recovery |
| [皮肤规范 / Skin format](src/SKIN_FORMAT.md) | 球衣、队徽、主题与分享 / Jerseys, badges, themes and sharing |
| [架构参考 / Architecture](src/Architecture/README.md) | 生命周期、数据边界、样例与接入 / Lifecycle, data boundaries, samples and integration |
| [路线图 / Roadmap](src/ROADMAP.md) | 已完成范围与剩余工作 / Delivered scope and remaining work |
| [更新记录 / Change log](src/RELEASE_NOTES.md) | 已发布与主分支改进 / Released version versus main changes |
| [验证与性能 / Verification and performance](src/PERFORMANCE.md) | 历史证据、方法和限制 / Historical evidence, methods and limitations |
| [界面语言 / Localization](src/Localization/README.md) | 中英文资源与新增文本要求 / Chinese–English resources and new UI text |

## 现有功能 / Features

- **桌宠与主题 / Companion and themes**：名字、大小、多屏吸附、悬停展开、全屏隐藏；五套球队皮肤、自定义球衣与全界面主题。Editable name/size, multi-monitor docking, hover reveal, fullscreen hiding, five team skins and custom clothing/UI themes.
- **启动器 / Launcher**：拼音/简称、收藏排序、最近应用、路径补全、拖放动作、自定义程序和图标。Pinyin/aliases, pins, recent apps, path completion, drop actions, custom programs and icons.
- **清单与记录 / Planning and records**：日/周/日历、重复与例外、提醒、便签、剪贴板保护及安静日志。Daily/weekly/calendar planning, recurrence/exceptions, reminders, notes, protected clipboard history and quiet logs.
- **可选能力 / Optional capabilities**：足球、QQ音乐单曲点播、PotPlayer/媒体、Clash、终端、Edge、只读电脑状态及已有邮件客户端入口。Football, QQ Music song requests, media/PotPlayer, Clash, terminal, Edge, read-only computer status and existing mail-client launch.
- **受控大脑 / Controlled brain**：本地命令优先，流式聊天、本地语音识别、预览确认的有限工具计划。Local commands first, streaming chat, local speech recognition and bounded tool plans with preview/confirmation.
- **模块底座 / Module foundation**：统一动作、依赖、结果、入口及生命周期；本机应用包与现有声明式/独立进程扩展。Unified actions, dependencies, results, entries and lifecycle; data-only app packages plus declarative/worker extensions.

本地功能不需要模型API；自由聊天与复杂理解需要自配服务。足球、歌曲和更新各自使用网络数据。邮件不读取账号或邮件；火绒仅快速打开，不代表已接入安全检测。
Local features need no model API. Free chat/complex understanding use your configured provider. Football, songs and updates have separate network sources. Mail does not retrieve accounts/messages; Huorong integration only opens the client.

## 开始使用 / Getting started

1. 在发布页下载 `Leo-v1.05.0-win-x64.zip`，完整解压，运行 `Install-Leo.cmd`。Download the ZIP from the release page, extract everything and run `Install-Leo.cmd`.
2. 右键伙伴进入设置，按需启用模块、配置本机程序路径。Right-click the companion to open settings and configure optional modules/local paths.
3. 设置 → 常规 → 界面语言可切换简体中文 / English。Switch between Chinese and English in Settings → General → Interface language.
4. 聊天需单独配置API；SenseVoice语音需本地模型。API chat and the local SenseVoice model are optional, separately configured dependencies.

单击打开/关闭应用搜索；拖动移动；Esc收起当前窗口。脚下按钮、右键菜单和快捷栏按已启用模块显示。
Click to toggle search, drag to move, and press Esc to dismiss the current window. Buttons, menus and shortcuts follow enabled modules.

## 开发与参与 / Development and contribution

Windows x64、[global.json](global.json)指定的 .NET SDK、Git、PowerShell。Windows x64, the SDK specified by [global.json](global.json), Git and PowerShell are required.

```powershell
./scripts/Build.ps1 -Locked
./.build/Release/LeoLauncher.exe
```

新增能力前阅读[开发指南](src/PRODUCT_AND_DEVELOPER_GUIDE.md)。不要把独立进程协议误认为权限沙箱；内部模块仍需宿主显式组装。提交相关定向检查，不上传个人配置、Key、模型、聊天或运行目录。检查第三方许可与素材权利后再分发。
Read the [developer guide](src/PRODUCT_AND_DEVELOPER_GUIDE.md) before extending Leo. Worker processes are not a permission sandbox; internal modules still require host composition. Include relevant checks and exclude personal settings, keys, models, conversations and runtime directories. Review licensing and asset rights before redistribution.

## 界面示意 / UI examples

以下为历史演示截图，不代表当前赛程或最新版全部布局。Historical demo screenshots; fixtures are not current and layouts may differ from main.

| 自定义皮肤 / Custom skin | 便签 / Notes |
| --- | --- |
| ![Custom skin](src/docs/screenshots/v0.53-new-skin.png) | ![Notes](src/docs/screenshots/v0.53-notes.png) |

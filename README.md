# Leo · 桌面伙伴与本地动作中枢

**简体中文** | [English](README.en.md)

把应用、清单、便签、足球、音乐和本地操作放进一个可自定义的Windows桌面伙伴，按需启用功能。

**最新发布：v1.05.0 · Windows x64。** 包含中英文界面、当前模块底座和后续修复，安装包附SHA-256校验。

[下载](https://github.com/LeoLi006/leo-desktop/releases/tag/v1.05.0) · [问题反馈](https://github.com/LeoLi006/leo-desktop/issues)

## 文档

本页及以下指南默认显示中文。点击每页顶部的English可阅读独立英文版，正文不再中英混排。

| 文档 | 内容 |
| --- | --- |
| [使用手册](src/USER_GUIDE.md) | 安装、操作、设置、故障排查 |
| [产品与开发指南](src/PRODUCT_AND_DEVELOPER_GUIDE.md) | 功能、架构、接口、扩展要求 |
| [模块与包协议](src/MODULES.md) | 本机应用、JSON、ZIP、stdio及恢复 |
| [皮肤规范](src/SKIN_FORMAT.md) | 球衣、队徽、主题与分享 |
| [架构参考](src/Architecture/README.md) | 生命周期、数据边界、样例与接入 |
| [路线图](src/ROADMAP.md) | 已完成范围与剩余工作 |
| [更新记录](src/RELEASE_NOTES.md) | 已发布版本和源码改进 |
| [验证与性能](src/PERFORMANCE.md) | 历史证据、方法和限制 |
| [界面语言](src/Localization/README.md) | 资源及新增文本要求 |
| [动画维护](src/ANIMATION-NOTES.md) | 吸附、贴图、绘制和检查 |
| [球衣素材](src/Assets/MatchDay/Kits/README.md) | 素材来源性质及使用限制 |
| [相关工作](src/RESEARCH.md) | 复用调研与选型原则 |

## 现有功能

- **桌宠与主题**：名字、大小、多屏吸附、悬停展开、全屏隐藏；五套球队皮肤、自定义球衣与全界面主题。
- **启动器**：拼音/简称、收藏排序、最近应用、路径补全、拖放动作、自定义程序和图标。
- **清单与记录**：日/周/日历、重复与例外、提醒、便签、剪贴板保护及安静日志。
- **可选能力**：足球、QQ音乐单曲点播、PotPlayer/媒体、Clash、终端、Edge、只读电脑状态及已有邮件客户端入口。
- **受控大脑**：本地命令优先，流式聊天、本地语音识别、预览确认的有限工具计划。
- **模块底座**：统一动作、依赖、结果、入口及生命周期；本机应用包与现有声明式/独立进程扩展。

本地功能不需要模型API；自由聊天与复杂理解需要自配服务。足球、歌曲和更新各自使用网络数据。邮件不读取账号或邮件；火绒仅快速打开，不代表已接入安全检测。

## 开始使用

1. 在发布页下载`Leo-v1.05.0-win-x64.zip`，完整解压，运行`Install-Leo.cmd`。
2. 右键伙伴进入设置，按需启用模块、配置本机程序路径。
3. 设置→常规→界面语言可切换简体中文和英文。
4. 聊天需单独配置API；SenseVoice语音需本地模型。

单击打开/关闭应用搜索；拖动移动；Esc收起当前窗口。脚下按钮、右键菜单和快捷栏按已启用模块显示。

## 开发与参与

需要Windows x64、[global.json](global.json)指定的.NET SDK、Git和PowerShell。

```powershell
./scripts/Build.ps1 -Locked
./.build/Release/LeoLauncher.exe
```

新增能力前阅读[开发指南](src/PRODUCT_AND_DEVELOPER_GUIDE.md)。独立进程协议不是权限沙箱；内部模块仍需宿主显式组装。提交相关定向检查，不上传个人配置、Key、模型、聊天或运行目录。检查第三方许可与素材权利后再分发。

## 界面示意

以下为历史演示截图，不代表当前赛程或最新版全部布局。

| 自定义皮肤 | 便签 |
| --- | --- |
| ![自定义皮肤](src/docs/screenshots/v0.53-new-skin.png) | ![便签](src/docs/screenshots/v0.53-notes.png) |

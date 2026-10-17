# Leo 桌面伙伴

Windows 桌宠、应用搜索、清单提醒、足球日程和 QQ 音乐快捷控制。巴萨主题。当前版本 **v0.38.0**，对应迁移前的稳定版 v38。

## 本机启动

运行 `app/LeoLauncher.exe`，或 `scripts/Start.ps1`。唯一运行目录固定为 app，更新时原位安装，个人数据保留。

## 从源码构建

需要 Windows x64、.NET SDK 9、PowerShell。固定版本依赖原包与 SHA-256 清单在 vendor，构建不需要从外网下载 NuGet 包。

```powershell
./scripts/Build.ps1 -Locked
./scripts/Test.ps1
./scripts/Install-Local.ps1
./scripts/Install-VoiceModel.ps1  # 首次语音安装需联网；已有模型仅校验
./scripts/Start.ps1
```

源码在 src；构建产物在 .build/Release；安装后程序与个人配置在 app。原位安装不会覆盖或删除个人 JSON 配置。API Key 在 Leo 设置中填写，DPAPI 本地加密保存。

## 常用功能

点击 Leo 打开应用搜索。支持拼音、自定义简称、收藏拖拽排序、剪贴板历史与收藏。右键音乐展开置顶歌单。每日/每周清单支持日历、重复事项、顺延、提醒、免打扰和自动备份。足球关注巴塞罗那、迈阿密国际、曼城。

- Ctrl+Alt+Space：应用搜索。
- Ctrl+Alt+V：SenseVoice 本地语音。
- Ctrl+Alt+L：QQ 原生桌面歌词。已验证 QQ 21.21，需要保留 QQ 默认 Ctrl+Alt+W 歌词绑定。
- “听赛前热身”“听路上随手听”“下一首”“上一首”：本地指令，不调用大模型。

QQ 音乐后台运行时静默点歌；公开歌单单向同步到本地。聊天和复杂指令可使用 OpenAI 兼容中转 API。更多当前版本说明见 docs/v38-features.md。

## 版本管理与隐私

唯一源码目录为 src。用 Git 分支、提交和 vX.Y.Z 标签管理版本，不再复制 LeoLauncher-vNN 文件夹。先构建、检查、提交，再原位安装。

app、backups、模型、编译缓存及个人 JSON 都被 Git 忽略。API Key、聊天、剪贴板、清单和个人设置不上传。不要 git add -f 这些内容。GitHub 仓库保持 private。

动画和自绘图标随仓库保存。球队标识来源见 assets/badges/SOURCES.md，权利归各自权利人。依赖原包和许可证保留在 vendor、licenses；不额外授予第三方素材或整个项目开源许可。

语音模型安装源和校验值见 scripts/model.json；超过200MB的模型不放进Git。来自 [sherpa-onnx 官方模型说明](https://k2-fsa.github.io/sherpa/onnx/sense-voice/pretrained.html)。

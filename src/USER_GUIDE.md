# 使用手册 / User guide

适用v1.05.0。旧v1.00.0安装包不包含全部后续变化。
Applies to v1.05.0. The older v1.00.0 binary does not contain every later change.

[首页 / Home](../README.md) · [开发指南 / Developer guide](PRODUCT_AND_DEVELOPER_GUIDE.md)

## 安装与设置 / Setup

从GitHub Release下载完整ZIP，解压后运行Install-Leo.cmd。升级选择原安装位置，先保存私有备份。不要将开发输出直接覆盖个人安装。
Download the complete ZIP from GitHub Releases, extract it and run Install-Leo.cmd. When upgrading, use the original installation location and retain a private backup. Do not directly overwrite a personal installation with development output.

右键伙伴 → 设置：名字、大小、快捷键、开机启动、模块、API、迁移与运行检查。常规顶部可切换简体中文/英文并保存。电脑状态和邮件默认关闭，按需启用。语言只改变界面，不翻译个人内容、不改变语音识别或模型回复语言。
Right-click the companion → Settings for name, size, shortcuts, startup, modules, API, migration and runtime checks. Select Chinese or English at the top of General and save. Computer status and mail are off by default. UI language does not translate personal content or change speech/model language.

## 常用操作 / Everyday controls

| 操作 / Action | 效果 / Behavior |
| --- | --- |
| 单击 / Click | 切换应用搜索，搜索打开时锁定伙伴位置 / Toggle app search; the companion stays fixed while search is open |
| 拖动 / Drag | 移动，接近显示器边缘松手吸附 / Move; release near a monitor edge to dock |
| 悬停头部 / Hover docked head | 展开并恢复功能按钮 / Reveal companion and buttons |
| Esc | 关闭当前窗口或收起面板 / Dismiss the current window or panel |
| 右键 / Right-click | 分组菜单：记录、应用媒体、组合、外观、设置 / Grouped records, apps/media, routines, appearance and settings |

隐藏入口只改变按钮/菜单显示；停用才拒绝能力并停止其后台活动，不自动退出外部应用。
Hiding an entry changes presentation; disabling rejects its actions and stops owned background work. It does not close an external application.

## 应用与文件 / Applications and files

按名称、拼音和简称搜索，例如wx、weixin、vsc；右键应用设置别名。收藏可拖拽排序。两排候选每排七个，最近使用和匹配按当前设置展示。输入完整路径可打开文件或文件夹，支持空格、引号和环境变量。
Search names, pinyin and aliases such as wx, weixin or vsc. Right-click an app to set aliases; drag pins to reorder. Candidates use two rows of seven. Enter a full file/folder path, including spaces, quotes or environment variables.

方向键选择，Enter打开，Tab补全路径，Ctrl+L回到输入；Shift+F10打开候选菜单。拖入程序可添加模块，其他文件、文字和网址显示适用的动作面板。
Use arrows to select, Enter to open, Tab for path completion, Ctrl+L for input and Shift+F10 for the candidate menu. Drop an executable to add a module; other files, text and links show relevant actions.

设置 → 功能中的应用管理可指定程序、图标、启动参数、工作目录和管理员选项；图标库提供18种图标，也可用自己的图片。快捷栏支持排序和溢出菜单。路径修复需要确认，不凭空改路径。
Application settings support path, icon, arguments, working directory and administrator launch. Choose from 18 built-in icons or a custom image. Shortcuts support ordering/overflow. Path repair requires confirmation.

## 清单、便签与剪贴板 / Tasks, notes and clipboard

清单提供日、周、日历及逾期视图；支持自然输入、重复/例外、右键操作、双击编辑、拖到日期改期、一键顺延和适用操作的撤销。添加前核对解析结果。提醒可稍后10分钟或1小时，设置免打扰时段；Leo关闭时不能承诺提醒。
Use daily, weekly, calendar and overdue views; local natural input, recurrence/exceptions, context actions, double-click editing, date-drop rescheduling, postponement and supported undo. Review parsed input before adding. Snooze reminders for 10 minutes or one hour and configure quiet hours. Reminders are not guaranteed while Leo is closed.

速记快捷键可配置，回车保存、Esc取消。便签可搜索、置顶及转待办。搜索“剪贴板”打开历史/收藏；可停用监听并设置敏感内容保护。真实秘密仍需自行保护。
Configure the quick-note shortcut; Enter saves and Esc cancels. Search/pin notes or convert them to tasks. Search for the clipboard action to open history/pins. Listening and sensitive-content protection are configurable, but cannot detect every secret.

## 足球、音乐与媒体 / Football, music and media

足球窗口管理俱乐部和国家队关注，查看近期结果、未来赛程、球队比赛和积分榜，设置观赛网址，并将真实比赛加入清单。联网失败保留缓存，留意时间和过期提示。后台校时受数据源可用性影响。
Manage club/national-team following, results, upcoming matches, team fixtures and standings; save watch links and add actual matches to the planner. On network failure cached content remains; check timestamps/staleness. Background schedule synchronization depends on source availability.

QQ音乐定位为快速点一首歌：搜索后选择真实歌曲，未启动时先启动并等待。暂停、控制和歌词依赖客户端状态/配置。歌单请到QQ音乐操作，Leo不提供歌单同步。PotPlayer及媒体窗口控制已配置客户端或Windows媒体会话。
QQ Music provides quick single-song requests: search and select a real result; Leo starts/waits for the client when needed. Controls and lyrics depend on client state/configuration. Manage playlists in QQ Music; playlist sync is not provided. PotPlayer/media controls target configured clients or Windows media sessions.

Clash未启动时静默启动并等待，再执行代理操作；以实际结果为准。停用模块不等于关闭代理。
Clash starts silently and waits before proxy operations when necessary. Follow observed results. Disabling the module does not turn off the proxy.

## 电脑状态、火绒与邮件 / Computer status, Huorong and mail

电脑状态只读显示CPU、内存、网络汇总、系统盘空间及进程排序，提供任务管理器入口；没有结束进程或清理操作。可选高占用提醒：CPU90%或内存92%持续30秒、15分钟冷却。高占用不等于病毒。
The read-only status view shows CPU, memory, aggregate network, system-disk space and process ranking, with a Task Manager shortcut. It does not kill processes or clean files. Optional alerts use CPU90% or memory92% sustained for30 seconds, with a15-minute cooldown. High usage does not imply malware.

火绒个人版只快速打开客户端，不扫描、不显示安全结论。邮件使用已有Foxmail/Thunderbird/经典Outlook或手动指定EXE/LNK；QQ/Gmail也可打开网页。校园邮箱在原客户端配置。Leo不接收邮箱密码、不拉邮件、不统计未读。
Huorong personal is a client shortcut, not scanning/security reporting. Mail opens existing Foxmail/Thunderbird/classic Outlook or a selected EXE/LNK; QQ/Gmail can open websites. Configure campus mail in the original client. Leo collects no mail passwords and does not retrieve messages or unread counts.

## 聊天、语音与组合 / Chat, speech and routines

API设置填写自己的兼容服务、模型和Key；不要公开配置。明确命令优先本地，事实查询交给对应模块，复杂理解才调用模型。模型计划需按现有规则预览确认，不能凭空生成目标编号。
Configure your compatible endpoint, model and key privately. Explicit commands run locally first; facts come from data modules and complex understanding uses the model. Tool plans require the existing validation/preview rules and real target IDs.

语音使用独立安装的SenseVoice本地模型。设置模型后测试麦克风权限；界面英文不代表英文识别自动配置。停止/关窗丢弃过期结果。组合是现有动作的顺序执行，可取消和核验失败步骤，不是任意脚本系统。
Speech uses a separately installed local SenseVoice model. Check microphone permissions and model setup; English UI does not automatically configure English recognition. Stop/close discards stale results. Routines are sequential existing actions with cancellation/recovery, not an arbitrary scripting system.

## 皮肤与配置 / Skins and configuration

外观中可选五套皮肤、新建独立主题、导入球衣和队徽，调整色彩/明暗/圆角/透明度。清单话语每行一句，可删空关闭。规范与限制见[皮肤格式](SKIN_FORMAT.md)。使用方案切换先看差异；公开分享包不同于私有备份。
Choose five built-in skins, create independent themes, import jerseys/badges and edit colors/brightness/radius/opacity. Planner quotes use one per line; empty disables them. See [skin format](SKIN_FORMAT.md). Preview profile changes; public share packages are not private backups.

## 排查与反馈 / Troubleshooting

先打开设置 → 运行检查，核对模块开关、路径、依赖和错误类型；“已安装”不等于“正在正常工作”。普通成功只进安静日志，报错和提醒才需要注意。错误重试前确认是否已经产生外部效果。
Start with Settings → Runtime checks: inspect enablement, paths, dependencies and error types. Installed does not mean operational. Routine success goes to quiet logs. Before retrying a failure, check whether an external effect already happened.

反馈提供版本/提交、Windows环境、操作步骤和脱敏截图。不要上传运行目录、Key、真实聊天/剪贴板或整份个人日志。
Report version/commit, Windows environment, reproduction and sanitized screenshots. Do not upload runtime directories, keys, private conversations/clipboard or complete personal logs.


## 语音模型、迁移与用量 / Speech model, migration and usage

源码使用者可在仓库根目录运行以下现有脚本安装本地模型；首次需要网络，模型不进入Git。发布包用户先核对包内是否提供对应脚本和说明，不要假设仓库脚本在安装目录存在。
Source users can run the existing repository script below to install the local model. The initial download requires network access; model files stay out of Git. Binary-package users should check the package's own instructions rather than assume repository scripts exist in the installation.

```powershell
./scripts/Install-VoiceModel.ps1
```

语音在本地转文字；若文字进入模型对话，文本会发送到自己配置的服务。Leo没有通用网页浏览工具；浏览器搜索与专门赛程来源不代表每次模型回复已联网核实。
Recognition runs locally; recognized text is sent to your configured provider if routed to model conversation. Leo has no general web-browsing tool. Browser search and dedicated fixture sources do not mean every model reply was verified online.

配置迁移在设置 → 常规 → 配置迁移与恢复。导入前看摘要，保留原备份。迁移导出不同于公开分享：可能包含待办、网址、简称与程序路径，应当作私有文件；不包含API Key、聊天、剪贴板或语音模型，不上传GitHub。
Use Settings → General → Configuration migration/recovery. Review the import summary and retain backups. Migration exports differ from public shares: they may contain tasks, URLs, aliases and program paths, so keep them private. API keys, chat, clipboard and speech models are excluded; do not upload migration files to GitHub.

用量页默认每日100次模型生成请求，0表示不限；失败/取消也可能已发请求并计数，模型列表读取不计入。Token只记录服务返回值，不猜费用；关闭不兼容的Token统计选项后仍可聊天。本地功能不受模型用量上限影响。
Usage defaults to100 model-generation requests daily;0 is unlimited. Failed/cancelled requests can still count if sent; listing models does not count. Tokens reflect provider-reported values, not estimated cost. Disable unsupported token reporting if necessary. Local functionality remains available after reaching the model limit.

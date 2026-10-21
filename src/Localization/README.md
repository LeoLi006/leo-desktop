# 界面语言 / UI localization

当前提供简体中文（zh-CN）和英文（en），默认中文。设置 → 常规顶部的“界面语言”，选择后保存；已有窗口和后续打开的窗口共同使用该语言。旧配置没有字段时沿用中文；无法识别的启动配置语言回退中文，导入和手动保存仍会校验合法值。

## 实现边界

- `Preferences.InterfaceLanguage`只管理界面；不修改线程/系统Culture，不更改动作编号、模型提示、语音识别语言或快捷口令。
- `UiLanguage`使用编译进程序的`en.json`。中文源文作为键。控件和菜单用弱键保存源文，动态TextChanged和新增子控件复用同一绑定；切回中文恢复源文，不靠反向机器翻译。
- `UiLanguage.Source`用于确有历史文字匹配的入口，点击行为依旧使用动作ID、Value和原始身份。不要用翻译后的Text做业务条件。
- 定制绘制用`UiLanguage.T`或`UiLanguage.DrawText`；日历使用显式`UiLanguage.Culture`。先翻译再测量气泡/反馈尺寸，菜单按显示文字测量。普通输入框、聊天正文、用户标题、应用/歌曲名称及自定义语句保留原文。
- `ComboChoice`只有Localize=true才翻译界面选项，Value不变；应用、路径和自定义名称保持false。`UiLanguage.Preserve(control/item)`或Tag="user-content"用于数据控件。不要对用户正文调用T。
- 提示气泡、确认对话框和工具提示通过现有宿主界面适配。第三方原始诊断、未知扩展文字和没有资源的技术细节回退原文，不联网翻译，不调用模型，不篡改落盘日志。
- 语言切换按现有窗口刷新，无新增计时器；窗口级刷新异常隔离，关闭后的控件不再更新。模板匹配使用非回溯正则，组合片段有递归深度上限。弱键绑定不持有已关闭窗口；工具提示释放后移出弱引用目录。

## 新增或调整文本

1. 在en.json加入准确英文，保持原中文键不变；数字/日期格式片段可使用`{0}`、`{1}`，变量原样带入；不将任意正文匹配为模板变量。避免只替换中文单字，避免翻译动作/枚举值。
2. 常规Label、Button、CheckBox及菜单由宿主绑定。自绘控件显式调用T，并检查英文实际宽度。过长按钮优先使用清楚的短标题，详细说明放到工具提示。
3. 混合正文的控件先拆开固定界面文字与用户内容；标记Preserve。自定义应用、歌曲、任务标题即使刚好名为“保存”“删除”也不应被改成英文。
4. 仅显示数据的换肤/语言刷新可以注册`RefreshOnChange`；不得在回调里保存业务数据、启动程序或发起网络/模型请求。
5. 不重命名旧协议、不扩大工具读取权限，不为缺少翻译而阻止模块执行。新增语言需要完整资源和界面检查，不把未知字符串伪装成已完成翻译。

## 定向验证

`LeoLauncher.exe --ui-language-test <绝对报告路径>`：隔离窗口、语言保存字段、切换回退、动态控件/菜单、数据保护、固定选项Value、长片段保护、主要窗口英文预览、单窗口故障隔离。输出到.build，不读个人业务文件，不启动外部客户端或请求模型。

`--chat-resize-test`另外覆盖缩放、最大化还原、隐藏重开、原生文字重绘、关闭后的排队刷新和空闲无重绘循环；语言接入不替代此项检查。

## 本轮验收

中英文专项28、聊天缩放9、聊天执行41、桌面工具58、入口显示109、使用方案36、主题兼容65，共346项直接相关检查通过。构建0警告0错误。英文设置、清单/日历、足球、音乐、媒体、应用编辑、邮箱、图标库使用隔离内容生成预览并检查。球队名、例程名、媒体标题及自定义语句出现中文是数据保留，不以机器翻译替换。

入口夹具中原先“所有能力都启用”的断言不再适用于默认关闭的新可选模块；现对切换显示模式前后的完整ID→启用状态逐项比对，包含停用项，未放宽隐藏入口不改变能力状态的要求。

此实现已本地提交与更新；本次仅同步GitHub源码文档，不制作新发布包。聊天重绘修复独立提交6e4bca5；语言资源是宿主展示层，不迁移业务执行器。


---

# UI localization — English

Main provides Simplified Chinese (`zh-CN`, default) and English (`en`). Select Settings → General → Interface language and save. Existing/new windows use the selected locale. Missing old fields and unknown startup values fall back to Chinese; imports and manual saves validate supported values.

## Implementation rules

- Preferences.InterfaceLanguage controls presentation only. Do not mutate global/thread Culture, action IDs, model prompts, speech recognition language or command phrases.
- UiLanguage reads the embedded en.json; Chinese source strings are keys. Weak bindings retain canonical source text for controls/menus and handle TextChanged/new children. Chinese restoration uses source text, not reverse translation.
- Use UiLanguage.Source only for legacy identity checks; actions should use IDs and values rather than displayed labels. Custom drawing uses T/DrawText; calendars use explicit UiLanguage.Culture.
- Translate before measuring feedback/menu text. Preserve input, conversation bodies, task/app/song names and custom quotes. ComboChoice translates only with Localize=true; Value stays unchanged. UiLanguage.Preserve or Tag=user-content protects data controls/items. Never call T on user bodies.
- Tooltips/confirmations use host adapters. Unknown extension strings, raw third-party errors and unmapped technical text fall back to source; no network/model translation and no rewriting persisted logs.
- No new polling timer. Closed controls ignore changes, weak bindings avoid retaining windows, disposed tooltips leave the weak directory. Template regexes are nonbacktracking with bounded recursive composition.

## Adding text

1. Add an accurate English value to en.json while retaining the Chinese source key. Numeric/date templates may use {0}/{1}; avoid overly broad patterns that capture arbitrary user text.
2. Ordinary controls bind through the host; custom paint explicitly translates. Check actual English width; use short labels and tooltips for detail.
3. Separate labels from user content and mark the latter preserved. A user item literally named “保存” must not become “Save”.
4. RefreshOnChange may update presentation only: never save business data, launch clients or request network/model work from its callback.
5. Keep protocol identities and permissions stable. Missing translation must not block capability execution. Additional locales require real resources and layout checks.

## Targeted evidence

`LeoLauncher.exe --ui-language-test <absolute-report-path>` covers locale fields/fallback, dynamic controls/menus, data preservation, enum values, long templates, isolated window previews and failure isolation. Reports belong in ignored .build, not personal data directories.

`--chat-resize-test` separately checks resize/restore, reopen, native child repaint, queued refresh after close and absence of idle redraw loops. Localization does not replace this check.

Historical implementation checks: language28, chat resize9, conversation41, desktop tools58, entry display109, usage profiles36 and themes65:346 related checks passed, build0 warnings/errors. This is scoped evidence at the implementation commit, not a full-suite claim for every later commit. Chinese team names, routine/media titles and user quotes are intentionally preserved data. The entry fixture compares the complete ID→enabled map before/after display changes, including default-off modules.

The implementation was locally installed; this documentation/source upload creates no new release package. Chat repaint fix is commit6e4bca5, language integration42fea41.

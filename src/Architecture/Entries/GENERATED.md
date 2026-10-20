# 入口接入清册

由隔离预览宿主生成并与真实注册表核对。只导出固定协议字段；不含名称、图标、路径、简称、启用状态或业务正文。本机应用编号为示例编号。

| 入口 | 模块 | 主动作 | 固定编辑器 |
| --- | --- | --- | --- |
| football | football-data | football_open | football-icon |
| music | music | music_open | builtin-application |
| media | media | media_open | builtin-application |
| terminal | terminal | terminal_open | builtin-application |
| clash | clash | proxy_status | builtin-application |
| edge | edge | web_search | builtin-application |
| example-01 | local:example-01 | local:example-01 | local-application |

Edge 的 launcher_open(web) 是 core 宿主转接；先核验 Edge 能力归属与启用，再执行通用窗口动作。详细菜单动作见 inventory.json。

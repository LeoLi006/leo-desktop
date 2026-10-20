# 实际注册表生成的契约清册

此文件由隔离预览宿主生成。只列已接入契约的模块；不代表旧扩展已迁移。应用编号已替换，未导出启用状态、设备依赖状态、路径或业务数据。

## application · local:example-01

协议：leo-host-contract/1；运行类型：application-adapter；数据归属：host.application-settings。

| 动作 | 参数编码 | 旧参数类型 | 结果类型 | 副作用 | 并发 | 超时ms | 取消 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| local:example-01 | object | application-id/v1 | application-start | external | coalesce-inflight | 15000 | host-cooperative |

依赖：application-file（repairable / existing-path-prepare）；working-directory（repairable / existing-configuration-check）。

## football · football-data

协议：leo-host-contract/1；运行类型：in-process；数据归属：host.football-cache。

| 动作 | 参数编码 | 旧参数类型 | 结果类型 | 副作用 | 并发 | 超时ms | 取消 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| football_facts | object | football-query/v1 | football-snapshot | read | coalesce-inflight | 90000 | host-cooperative |
| football_open | object | football-query/v1 | window-result | window | coalesce-inflight | 15000 | host-cooperative |
| football_schedule | object | football-query/v1 | football-snapshot | read | coalesce-inflight | 90000 | host-cooperative |
| football_team | object | football-query/v1 | football-snapshot | read | coalesce-inflight | 90000 | host-cooperative |
| football_events | object | football-query/v1 | football-snapshot | read | coalesce-inflight | 90000 | host-cooperative |

依赖：query-service（required / existing-query-validation）；football-source（optional / cached-fallback）。

## media · media

协议：leo-host-contract/1；运行类型：in-process；数据归属：host.media-session-state。

| 动作 | 参数编码 | 旧参数类型 | 结果类型 | 副作用 | 并发 | 超时ms | 取消 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| media_open | object | media-session-or-path | media-receipt | window | coalesce-inflight | 45000 | host-cooperative |
| media_file | object | media-session-or-path | media-receipt | external | coalesce-inflight | 45000 | host-cooperative |
| media_status | object | media-session-or-path | media-sessions | read | coalesce-inflight | 45000 | host-cooperative |
| media_toggle | object | media-session-or-path | media-receipt | external | coalesce-inflight | 45000 | host-cooperative |
| media_play | object | media-session-or-path | media-receipt | external | coalesce-inflight | 45000 | host-cooperative |
| media_pause | object | media-session-or-path | media-receipt | external | coalesce-inflight | 45000 | host-cooperative |
| media_next | object | media-session-or-path | media-receipt | external | coalesce-inflight | 45000 | host-cooperative |
| media_previous | object | media-session-or-path | media-receipt | external | coalesce-inflight | 45000 | host-cooperative |
| media_stop | object | media-session-or-path | media-receipt | external | coalesce-inflight | 45000 | host-cooperative |

依赖：media-provider（optional / existing-provider-verification）；potplayer（optional / existing-path-resolution）。

## todos · todos

协议：leo-host-contract/1；运行类型：in-process；数据归属：host.todo-store。

| 动作 | 参数编码 | 旧参数类型 | 结果类型 | 副作用 | 并发 | 超时ms | 取消 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| todos_open | value-wrapper | todo-local/v1 | todo-payload | window | serialized-store | 15000 | commit-aware |
| todo_list | value-wrapper | todo-local/v1 | todo-payload | read | serialized-store | 15000 | commit-aware |
| todo_add | object | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_repeat | object | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_complete | value-wrapper | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_complete_title | value-wrapper | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_edit | object | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_move | object | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_remind | object | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_snooze | object | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_postpone | value-wrapper | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_stop_repeat | object | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_skip_repeat | value-wrapper | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_undo | value-wrapper | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_natural | value-wrapper | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_snapshot | value-wrapper | todo-local/v1 | todo-payload | read | serialized-store | 15000 | commit-aware |
| todo_ensure | value-wrapper | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_completed | object | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_important | object | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_delete | value-wrapper | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_restore | value-wrapper | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_postpone_ids | value-wrapper | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_follow_match | value-wrapper | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_match_add | value-wrapper | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_match_sync | object | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_match_hydrate | object | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_due | value-wrapper | todo-local/v1 | todo-payload | read | serialized-store | 15000 | commit-aware |
| todo_ack | value-wrapper | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_backup | value-wrapper | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_restore_backup | value-wrapper | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_quiet | value-wrapper | todo-local/v1 | todo-payload | read | serialized-store | 15000 | commit-aware |
| todo_quiet_save | value-wrapper | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |

依赖：todo-store（required / existing-store-validation）。

详细输入、输出及原执行策略见 inventory.json。参数描述是接口说明，不是第二套JSON Schema或执行器。

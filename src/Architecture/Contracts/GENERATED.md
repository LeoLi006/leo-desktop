# 实际注册表生成的契约清册

此文件由隔离预览宿主生成。只列已接入契约的模块；不代表旧扩展已迁移。应用编号已替换，未导出启用状态、设备依赖状态、路径或业务数据。

## application · local:example-01

协议：leo-host-contract/1；运行类型：application-adapter；数据归属：host.application-settings。

| 动作 | 请求协议 | 适配边界 | 参数编码 | 旧参数类型 | 结果类型 | 副作用 | 并发 | 超时ms | 取消 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| local:example-01 | leo-action/1 | application-adapter | object | application-id/v1 | application-start | external | coalesce-inflight | 15000 | host-cooperative |

依赖：application-file（repairable / existing-path-prepare）；working-directory（repairable / existing-configuration-check）。

## clipboard · clipboard

协议：leo-host-contract/1；运行类型：in-process；数据归属：host.clipboard-history。

| 动作 | 请求协议 | 适配边界 | 参数编码 | 旧参数类型 | 结果类型 | 副作用 | 并发 | 超时ms | 取消 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| clipboard_open | leo-action/1 | module-adapter | object | clipboard-local | clipboard-receipt | window | serialized-store | 15000 | host-cooperative |
| clipboard_pin_text | leo-action/1 | module-adapter | object | clipboard-local | clipboard-receipt | write | serialized-store | 15000 | host-cooperative |
| clipboard_query | leo-action/1 | module-adapter | object | clipboard-local | clipboard-local-ids | read | serialized-store | 15000 | host-cooperative |
| clipboard_copy | leo-action/1 | module-adapter | object | clipboard-local | clipboard-receipt | write | serialized-store | 15000 | host-cooperative |
| clipboard_pin | leo-action/1 | module-adapter | object | clipboard-local | clipboard-receipt | write | serialized-store | 15000 | host-cooperative |
| clipboard_delete | leo-action/1 | module-adapter | object | clipboard-local | clipboard-receipt | write | serialized-store | 15000 | host-cooperative |
| clipboard_clear | leo-action/1 | module-adapter | object | clipboard-local | clipboard-receipt | write | serialized-store | 15000 | host-cooperative |
| clipboard_undo | leo-action/1 | module-adapter | object | clipboard-local | clipboard-receipt | write | serialized-store | 15000 | host-cooperative |
| clipboard_recording | leo-action/1 | module-adapter | object | clipboard-local | clipboard-receipt | write | serialized-store | 15000 | host-cooperative |

依赖：history-store（required / existing-history-save-validation）；system-clipboard（optional / existing-sensitive-content-protection）。

## conversation · conversation

协议：leo-host-contract/1；运行类型：in-process；数据归属：host.private-conversation-jobs。

| 动作 | 请求协议 | 适配边界 | 参数编码 | 旧参数类型 | 结果类型 | 副作用 | 并发 | 超时ms | 取消 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| chat_open | leo-action/1 | module-adapter | object | private-session-handle | conversation-private-result | window | coalesce-inflight | 15000 | host-cooperative |
| chat_reply | ActionRequest | host-private | private-handle | private-session-handle | conversation-private-result | session | coalesce-inflight | 120000 | host-cooperative |
| chat_close | ActionRequest | host-private | private-handle | private-session-handle | conversation-private-result | session | coalesce-inflight | 15000 | host-cooperative |
| chat_cancel | ActionRequest | host-private | private-handle | private-session-handle | conversation-private-result | session | coalesce-inflight | 15000 | host-cooperative |
| voice_start | ActionRequest | host-private | private-handle | private-session-handle | conversation-private-result | session | coalesce-inflight | 15000 | host-cooperative |
| voice_finish | ActionRequest | host-private | private-handle | private-session-handle | conversation-private-result | session | coalesce-inflight | 15000 | host-cooperative |
| voice_stop | ActionRequest | host-private | private-handle | private-session-handle | conversation-private-result | session | coalesce-inflight | 15000 | host-cooperative |
| voice_stop_listening | ActionRequest | host-private | private-handle | private-session-handle | conversation-private-result | session | coalesce-inflight | 15000 | host-cooperative |
| voice_stop_speaking | ActionRequest | host-private | private-handle | private-session-handle | conversation-private-result | session | coalesce-inflight | 15000 | host-cooperative |
| voice_accept | ActionRequest | host-private | private-handle | private-session-handle | conversation-private-result | session | coalesce-inflight | 15000 | host-cooperative |
| voice_speak | ActionRequest | host-private | private-handle | private-session-handle | conversation-private-result | session | coalesce-inflight | 15000 | host-cooperative |

依赖：conversation-host（required / existing-session-generation-check）；model-endpoint（optional / existing-local-route-and-provider-errors）；speech-input（optional / existing-local-recognition-lifecycle）；speech-output（optional / existing-speech-output-lifecycle）。

## football · football-data

协议：leo-host-contract/1；运行类型：in-process；数据归属：host.football-cache。

| 动作 | 请求协议 | 适配边界 | 参数编码 | 旧参数类型 | 结果类型 | 副作用 | 并发 | 超时ms | 取消 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| football_facts | leo-action/1 | module-adapter | object | football-query/v1 | football-snapshot | read | coalesce-inflight | 90000 | host-cooperative |
| football_open | leo-action/1 | module-adapter | object | football-query/v1 | window-result | window | coalesce-inflight | 15000 | host-cooperative |
| football_schedule | leo-action/1 | module-adapter | object | football-query/v1 | football-snapshot | read | coalesce-inflight | 90000 | host-cooperative |
| football_team | leo-action/1 | module-adapter | object | football-query/v1 | football-snapshot | read | coalesce-inflight | 90000 | host-cooperative |
| football_events | leo-action/1 | module-adapter | object | football-query/v1 | football-snapshot | read | coalesce-inflight | 90000 | host-cooperative |

依赖：query-service（required / existing-query-validation）；football-source（optional / cached-fallback）。

## match-planning · match-planning

协议：leo-host-contract/1；运行类型：in-process；数据归属：host.match-reference-and-todos。

| 动作 | 请求协议 | 适配边界 | 参数编码 | 旧参数类型 | 结果类型 | 副作用 | 并发 | 超时ms | 取消 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| match_lookup | leo-action/1 | module-adapter | object | match-reference/v1 | match-envelope | read | coalesce-inflight | 15000 | host-cooperative |
| match_add | leo-action/1 | module-adapter | object | match-reference/v1 | match-todo-receipt | write | coalesce-inflight | 15000 | host-cooperative |

依赖：match-source（required / existing-cached-reference-validation）；todo-store（required / existing-todo-commit-validation）。

## media · media

协议：leo-host-contract/1；运行类型：in-process；数据归属：host.media-session-state。

| 动作 | 请求协议 | 适配边界 | 参数编码 | 旧参数类型 | 结果类型 | 副作用 | 并发 | 超时ms | 取消 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| media_open | leo-action/1 | module-adapter | object | media-session-or-path | media-receipt | window | coalesce-inflight | 45000 | host-cooperative |
| media_file | leo-action/1 | module-adapter | object | media-session-or-path | media-receipt | external | coalesce-inflight | 45000 | host-cooperative |
| media_status | leo-action/1 | module-adapter | object | media-session-or-path | media-sessions | read | coalesce-inflight | 45000 | host-cooperative |
| media_toggle | leo-action/1 | module-adapter | object | media-session-or-path | media-receipt | external | coalesce-inflight | 45000 | host-cooperative |
| media_play | leo-action/1 | module-adapter | object | media-session-or-path | media-receipt | external | coalesce-inflight | 45000 | host-cooperative |
| media_pause | leo-action/1 | module-adapter | object | media-session-or-path | media-receipt | external | coalesce-inflight | 45000 | host-cooperative |
| media_next | leo-action/1 | module-adapter | object | media-session-or-path | media-receipt | external | coalesce-inflight | 45000 | host-cooperative |
| media_previous | leo-action/1 | module-adapter | object | media-session-or-path | media-receipt | external | coalesce-inflight | 45000 | host-cooperative |
| media_stop | leo-action/1 | module-adapter | object | media-session-or-path | media-receipt | external | coalesce-inflight | 45000 | host-cooperative |

依赖：media-provider（optional / existing-provider-verification）；potplayer（optional / existing-path-resolution）。

## music · music

协议：leo-host-contract/1；运行类型：in-process；数据归属：host.music-session。

| 动作 | 请求协议 | 适配边界 | 参数编码 | 旧参数类型 | 结果类型 | 副作用 | 并发 | 超时ms | 取消 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| music_open | leo-action/1 | module-adapter | object | music-text | music-receipt | window | coalesce-inflight | 45000 | host-cooperative |
| music_search | leo-action/1 | module-adapter | object | music-text | music-candidates | read | coalesce-inflight | 45000 | host-cooperative |
| music_play | leo-action/1 | module-adapter | object | music-text | music-receipt | external | coalesce-inflight | 45000 | host-cooperative |
| music_step | leo-action/1 | module-adapter | object | music-text | music-receipt | external | coalesce-inflight | 45000 | host-cooperative |
| music_pause | leo-action/1 | module-adapter | object | music-text | music-receipt | external | coalesce-inflight | 45000 | host-cooperative |
| music_client | leo-action/1 | module-adapter | object | music-text | music-receipt | external | coalesce-inflight | 45000 | host-cooperative |
| music_lyrics | leo-action/1 | module-adapter | object | music-text | music-receipt | external | coalesce-inflight | 45000 | host-cooperative |
| music_status | leo-action/1 | module-adapter | object | music-text | music-client-status | read | coalesce-inflight | 5000 | host-cooperative |

依赖：music-source（optional / existing-query-error-retention）；music-client（optional / existing-start-wait-control）；song-selection（optional / existing-real-candidate-validation）。

## notes · notes

协议：leo-host-contract/1；运行类型：in-process；数据归属：host.notes。

| 动作 | 请求协议 | 适配边界 | 参数编码 | 旧参数类型 | 结果类型 | 副作用 | 并发 | 超时ms | 取消 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| notes_open | leo-action/1 | module-adapter | object | legacy-text-or-json | note-result | window | serialized-store | 15000 | host-cooperative |
| note_quick_open | leo-action/1 | module-adapter | object | legacy-text-or-json | note-result | window | serialized-store | 15000 | host-cooperative |
| note_add | leo-action/1 | module-adapter | object | legacy-text-or-json | note-result | write | serialized-store | 15000 | host-cooperative |
| note_save | leo-action/1 | module-adapter | object | legacy-text-or-json | note-result | write | serialized-store | 15000 | host-cooperative |
| note_query | leo-action/1 | module-adapter | object | legacy-text-or-json | note-list | read | serialized-store | 15000 | host-cooperative |
| note_remove | leo-action/1 | module-adapter | object | legacy-text-or-json | note-result | write | serialized-store | 15000 | host-cooperative |
| note_pin | leo-action/1 | module-adapter | object | legacy-text-or-json | note-result | write | serialized-store | 15000 | host-cooperative |
| note_link | leo-action/1 | module-adapter | object | legacy-text-or-json | note-result | write | serialized-store | 15000 | host-cooperative |

依赖：note-store（required / lazy-existing-store-validation）。

## proxy · clash

协议：leo-host-contract/1；运行类型：in-process；数据归属：host.proxy-state。

| 动作 | 请求协议 | 适配边界 | 参数编码 | 旧参数类型 | 结果类型 | 副作用 | 并发 | 超时ms | 取消 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| proxy_status | leo-action/1 | module-adapter | object | none | proxy-status | read | coalesce-inflight | 5000 | host-cooperative |
| proxy_on | leo-action/1 | module-adapter | object | none | proxy-status | external | coalesce-inflight | 60000 | host-cooperative |
| proxy_off | leo-action/1 | module-adapter | object | none | proxy-status | external | coalesce-inflight | 60000 | host-cooperative |

依赖：clash-client（optional / existing-start-wait-verify）。

## todos · todos

协议：leo-host-contract/1；运行类型：in-process；数据归属：host.todo-store。

| 动作 | 请求协议 | 适配边界 | 参数编码 | 旧参数类型 | 结果类型 | 副作用 | 并发 | 超时ms | 取消 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| todos_open | leo-action/1 | module-adapter | value-wrapper | todo-local/v1 | todo-payload | window | serialized-store | 15000 | commit-aware |
| todo_list | leo-action/1 | module-adapter | value-wrapper | todo-local/v1 | todo-payload | read | serialized-store | 15000 | commit-aware |
| todo_add | leo-action/1 | module-adapter | object | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_repeat | leo-action/1 | module-adapter | object | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_complete | leo-action/1 | module-adapter | value-wrapper | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_complete_title | leo-action/1 | module-adapter | value-wrapper | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_edit | leo-action/1 | module-adapter | object | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_move | leo-action/1 | module-adapter | object | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_remind | leo-action/1 | module-adapter | object | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_snooze | leo-action/1 | module-adapter | object | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_postpone | leo-action/1 | module-adapter | value-wrapper | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_stop_repeat | leo-action/1 | module-adapter | object | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_skip_repeat | leo-action/1 | module-adapter | value-wrapper | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_undo | leo-action/1 | module-adapter | value-wrapper | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_natural | leo-action/1 | module-adapter | value-wrapper | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_snapshot | leo-action/1 | module-adapter | value-wrapper | todo-local/v1 | todo-payload | read | serialized-store | 15000 | commit-aware |
| todo_ensure | leo-action/1 | module-adapter | value-wrapper | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_completed | leo-action/1 | module-adapter | object | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_important | leo-action/1 | module-adapter | object | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_delete | leo-action/1 | module-adapter | value-wrapper | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_restore | leo-action/1 | module-adapter | value-wrapper | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_postpone_ids | leo-action/1 | module-adapter | value-wrapper | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_follow_match | leo-action/1 | module-adapter | value-wrapper | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_match_add | leo-action/1 | module-adapter | value-wrapper | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_match_sync | leo-action/1 | module-adapter | object | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_match_hydrate | leo-action/1 | module-adapter | object | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_due | leo-action/1 | module-adapter | value-wrapper | todo-local/v1 | todo-payload | read | serialized-store | 15000 | commit-aware |
| todo_ack | leo-action/1 | module-adapter | value-wrapper | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_backup | leo-action/1 | module-adapter | value-wrapper | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_restore_backup | leo-action/1 | module-adapter | value-wrapper | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |
| todo_quiet | leo-action/1 | module-adapter | value-wrapper | todo-local/v1 | todo-payload | read | serialized-store | 15000 | commit-aware |
| todo_quiet_save | leo-action/1 | module-adapter | value-wrapper | todo-local/v1 | todo-payload | write | serialized-store | 15000 | commit-aware |

依赖：todo-store（required / existing-store-validation）。

## workflows · workflows

协议：leo-host-contract/1；运行类型：in-process；数据归属：host.workflow-journal。

| 动作 | 请求协议 | 适配边界 | 参数编码 | 旧参数类型 | 结果类型 | 副作用 | 并发 | 超时ms | 取消 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| combo_name | ActionRequest | legacy-only | legacy-value | legacy-combo-name | workflow-receipt | external | coalesce-inflight | 120000 | host-cooperative |
| scene_watch | ActionRequest | legacy-only | legacy-value | legacy-combo-name | workflow-receipt | external | coalesce-inflight | 120000 | host-cooperative |
| combo_resume | ActionRequest | legacy-only | legacy-value | legacy-combo-name | workflow-receipt | external | coalesce-inflight | 120000 | host-cooperative |

依赖：selected-actions（required / existing-plan-action-and-dependency-validation）；workflow-journal（required / existing-journal-and-recovery-validation）。

详细输入、输出及原执行策略见 inventory.json。参数描述是接口说明，不是第二套JSON Schema或执行器。

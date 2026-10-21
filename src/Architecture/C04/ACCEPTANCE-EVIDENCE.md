# C04阶段验收：历史证据索引

核对日期：2026-10-21。以下是既有本地`.build`报告的元数据，本轮未重新执行测试。不同报告覆盖范围重叠，不能相加称为本轮通过项数；源码后续的接口收敛也不意味着所有旧报告均针对同一提交。可公开的说明只记录相对报告名、数量和SHA-256，不复制原始报告或业务数据；忽略目录在其他克隆中可能不存在。

| 本地报告（相对.build） | 检查项 | 原报告结果 | SHA-256 |
| --- | --- | --- | --- |
| `application-host/application-host.json` | 30 | 通过 | `449768b5e244e8593b8c421e1716349cdc80d34407a109b26fa942d426217031` |
| `client-host/client-host.json` | 51 | 通过 | `ffdeb8a53a14d38d23f3e9198aad259489e4256eb4ae8581e1440817b7fdad35` |
| `host-services-cleanup/presentation-service.json` | 104 | 通过 | `ab1eac37382511e86985934179a3fe91275d17a8ba89700f90188063ea8e7bac` |
| `theme-cleanup/theme-cleanup-test.json` | 12 | 通过 | `173a2193f4e8d4aa597b4c5aa06731bbef31ce66cac941e4ba17fbed4f598c44` |
| `theme-cleanup/theme-service-test.json` | 12 | 通过 | `25caccab7c74cab751f7865fdbe1cba836648bb4b6cbe42536052676b003b5d4` |
| `theme-cleanup/planner-football-theme-test.json` | 27 | 通过 | `9a830fcdb5688c66cb2c234ec98f97a3f1d827aa70a3b9d1a691085d37e98978` |
| `theme-cleanup/conversation-search-theme-test.json` | 25 | 通过 | `967368541726f7c1284b8ee4a7d312ae1c3c2a5d7cabb5735a3a62af2f9cfb2f` |
| `theme-cleanup/host-service-cleanup-test.json` | 22 | 通过 | `61a2770f9534dc89455fd357d114c0e44f4d910af137f444d6376ab48f84bf0a` |
| `host-periodic/r02-host-test.json` | 44 | 通过 | `ffb592375667df73674c8ff4dd27d2e5d1537de75e59f0110196d684edf73498` |
| `host-periodic/window-periodic-test.json` | 19 | 通过 | `0acd2ae89fc25c86c2251254393792fb0abb5b5700bfa4e16d660bb998e50680` |
| `resident-periodic/resident-contract.json` | 54 | 通过 | `34177aed4523d17efaa7c973e4d978569bd34bfa47d828eb59f0ea1e957a8bcc` |

## 初次失败与复测

- 应用宿主批的`application-host/q2-capability.json`曾有每订阅期限检查失败；`application-host/q2-recheck.json`复测60项通过。未因此改变业务期限，见[首批说明](README.md)。这仍是应保留的时序波动记录，不能以一次复测消除长期可靠性疑问。
- 清单主题批的`planner-football-themes/baseline-planner.json`复现旧版菜单固定宽度断言失败；随后按文字和标记空间测量的`planner-theme-test.json`12项通过。未更改菜单宽度，见[该批边界](PLANNER-FOOTBALL-THEMES.md)。
- 以上索引是历史验证依据，不承诺长时无泄漏、真实冷启动、真实客户端成功率或外部代码隔离。样例编译依据为此前锁定构建；本轮文档编辑不重复构建或重新测试未改代码。


## 第十八批：内部模块闭环新证据

`.build/module-entry-closure/closure-final.json`：41项通过（保留20项，新增21项），SHA256 `e7e1a231246120c7fc9a1e26e1d630c4e7d28a7214e0866e96733fd3615108a6`。报告只在本机忽略目录；本批锁定构建零警告，未重跑上方历史业务专项。具体边界见[闭环收尾](MODULE-CLOSURE.md)。只扩充测试与说明，不更新运行程序。


## R06：入口诊断新证据

本轮只验证目录与直接相关渲染/宿主诊断，不沿用历史数量冒称本轮全量。

| 本机忽略报告 | 项数 | SHA256 |
| --- | --- | --- |
| `entry-diagnostics/module-entry-diagnostics.json` | 24 | `782a15dad40b61c866d4364d6174ea102475f1cfc8f415b223431ccb87f05321` |
| `entry-diagnostics/module-entry.json` | 27 | `ef3406f3faa25322890bd333247adbe27bc2642646debc183a4ce8b5c72dd50a` |
| `entry-diagnostics/module-entry-rendering.json` | 41 | `e7e1a231246120c7fc9a1e26e1d630c4e7d28a7214e0866e96733fd3615108a6` |
| `entry-diagnostics/host-service-cleanup.json` | 22 | `df13195b2e5da132472741213c3a48fccea2dc7de426f1201bc5d1da9cbf7d2e` |

四项均通过，锁定构建零警告；限制及交付见[入口诊断](ENTRY-DIAGNOSTICS.md)。


## C04-G1：三类数据边界证据

| 本机忽略报告 | 结果 | SHA256 |
| --- | --- | --- |
| `data-boundaries/before.json` | 初次24项，5项失败 | `7b2b89f03445ed63687282757f5d0c686c7d790a321dc1e0f6bd35b96f5eff93` |
| `data-boundaries/boundary-final.json` | 最终28项通过 | `65c5766ded86d6b754940ab2dc6e505598b5dd3c417bb81f638ce18095e4a929` |
| `data-boundaries/football-after.json` | 足球相关52项通过 | `6210ab022e233544b897272e99e25daf1d5671d540b5ab11cb8ddb53f794008b` |

复现、修复与边界见[数据清册](DATA-BOUNDARIES.md)。不将历史专项算作本轮测试，不以预取消样本宣称全部提交竞态已验收。

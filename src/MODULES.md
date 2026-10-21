# 模块与扩展协议

**简体中文** | [English](MODULES.en.md)

适用主分支1.04.0。公开包协议为`LeoModule/1`；内部`leo-action/1`与`leo-host-contract/1`不是worker可自由调用的服务。

[产品与开发指南](PRODUCT_AND_DEVELOPER_GUIDE.md) · [架构](Architecture/README.md)

## 选择方式

| 方式 | 用途与限制 |
| --- | --- |
| 本机应用 | EXE/LNK、名称、18种内置或自定义图标、参数、工作目录、管理员启动；只获得启动能力 |
| 数据型应用包 | 分享元数据，接收者补本机路径 |
| 声明式JSON | 包装已有公开动作及固定值 |
| 独立进程ZIP | 一次请求、一次响应的自己的程序 |
| 内部C# | 修改宿主、限定服务与重新编译；不是稳定外部SDK |

添加/停用/移除不会自动卸载或退出用户软件。入口隐藏不等于停用能力；旧入口点击仍须验证当前身份。扩展默认不因安装就获得模型权限。

## 声明式清单

UTF-8 JSON，字段大小写如下。在设置的扩展管理窗口导入。

```json
{
  "Format": "LeoModule/1",
  "Id": "daily-plan",
  "Name": "Daily plan",
  "Version": "1.0.0",
  "Runtime": "actions",
  "HostMin": "1.4.0",
  "Dependencies": ["windows", "leo-module/1"],
  "Commands": [
    {"Id":"today","Title":"Open today's plan","Phrases":["open daily plan"],"Action":"todos_open","Value":""}
  ]
}
```

| 字段 | 规则 |
| --- | --- |
| Id | 模块2–40字符、命令1–40字符；小写字母开头，随后字母/数字/连字符 |
| Name / Commands | 名称最多40字符，每包1–16命令 |
| Phrases / Value | 每条口令最多60字符；Value字符串最多4000，复杂参数使用对应动作的JSON字符串 |
| HostMin / HostMax | 可选数字版本闭区间，省略无额外限制；不是SemVer表达式 |
| Dependencies | windows、leo-module/1、leo-host-contract/1、stdio/v1、module:<扩展ID>；只被动核对 |

Action取自实际[LeoActions.cs](LeoActions.cs)，但内部私有句柄或需要额外确认的动作不因名字存在就可用。禁止递归组合、再分派文本或扩展递归。固定声明值不可用非空参数任意覆盖。未知、缺失、循环或停用依赖阻止相应调用，不自动下载。固定local_app_open同时核对真实应用、路径与工作目录。

动作身份为`ext:daily-plan:today`，贡献身份为`ext:daily-plan/today`，不可混用。默认仅搜索，可在原入口设置选择菜单/快捷栏。

## 独立进程

ZIP根目录包含module.json和worker.exe；依赖保留相对目录。清单如下：

```json
{
  "Format":"LeoModule/1","Id":"hello-worker","Name":"Hello worker","Version":"1.0.0",
  "Runtime":"stdio","Executable":"worker.exe","TimeoutSeconds":15,
  "Commands":[{"Id":"ping","Title":"Ping worker","Phrases":["ping worker"],"Action":"worker:ping","Value":""}]
}
```

用户触发后，Leo以当前用户权限启动程序，附加`--leo-module`；stdin写一行UTF-8 JSON并关闭。请求形状（路径仅为占位说明）：

```json
{"protocol":"LeoModule/1","requestId":"generated-id","command":"ping","value":"","dataDirectory":"<host-assigned-local-directory>","origin":"search","theme":{"skin":"83","name":"Leo","accent":"#E7C277","background":"#0B1221","text":"#EDF2FA","radius":14,"opacity":100}}
```

command不含worker:前缀。读取→校验协议/命令/参数→执行有界操作→stdout仅输出一行响应→正常退出；日志只能写stderr。最小响应：

```json
{"ok":true,"message":"Operation completed"}
```

失败用ok:false；非零退出、无响应、坏格式均失败。默认15秒，可设1–120秒；响应行最多64KB，message最多4000字符。取消/超时结束宿主拥有的worker及子进程，不撤销已经发生的外部效果。私有状态使用dataDirectory，不放包目录；该目录不是权限沙箱。

[WorkerFixture.cs.txt](Architecture/Extensions/WorkerFixture.cs.txt)为仓库测试worker，不是生产模板。每次调用是独立进程协议，不是常驻会话；回复只当数据，不执行为宿主命令。
[WorkerFixture.cs.txt](Architecture/Extensions/WorkerFixture.cs.txt) is a test fixture, not a production template. Each call is a one-shot process exchange, not a persistent session. Responses are data, never host commands.

## ZIP、更新与恢复

- 压缩最多64MB，展开最多256MB/2000文件；只允许exe/dll/json/png/txt/md/dat/bin；根EXE。拒绝越界、绝对/重复路径、链接及脚本。
- 框架依赖worker需要用户已有运行时；不自动下载。程序权限等同用户，不加载第三方DLL到Leo主进程。
- 同Id更新必须提高数字版本；运行/排队时拒绝更新和移除。暂存校验后提升，失败尝试恢复旧版；恢复也失败保留材料。
- 新版已生效但旧目录清理失败属于警告，不盲目重放。移除保留`module-data/<id>`；程序位于`modules/<id>`。
- 共享应用包严格八字段：Format/Protocol/Id/Version/Name/HostMin/HostMax/Dependencies。不夹带程序、路径、参数、管理员许可或业务记录。

## 调用与验收

搜索`:ext:hello-worker:ping`或登记的标题/口令；聊天/语音沿明确指令路由，自定义组合可用统一指令。旧module_list/module_run兼容动作仍受当前启用、参数和确认限制，不是自动模型授权。

相关脚本：Test-ExtensionContracts.ps1、Test-ExtensionPackages.ps1、Test-ModuleStatusContracts.ps1、Test-SharingBoundaries.ps1、Test-DeliveryRecovery.ps1（均在src）。只用隔离数据和可信测试worker验证，不用个人Key和第三方程序做默认测试。

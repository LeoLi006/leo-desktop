# 统一模块契约：接入与兼容说明

第一批日期：2026-10-20。范围：自定义应用、清单、足球与媒体。第二批新增七类，详见 [接入边界与验证](SECOND-BATCH.md)；当前清册11类91动作。协议标识为 **leo-host-contract/1**，仅为当前宿主内部描述接口，不是新的模块安装包或进程外协议。`LeoModule/1`、`leo-action/1`、旧动作编号、业务与数据格式保持。

## 文件入口

- `ModuleContracts.cs`：统一记录、验证器、四类描述；不含新的执行器。
- [GENERATED.md](GENERATED.md)：隔离预览宿主实际注册表生成的当前动作清册。
- [inventory.json](inventory.json)：同一批清册的完整定义，应用编号已替换为示意别名。
- [MinimalModule.cs](MinimalModule.cs)：可通过现有注册表/分发器运行的最小内置样例；不会自动注册，不是可安装的陌生代码包。
- [VALIDATION.md](VALIDATION.md)：专项、兼容与隐私验证。

Q1的 `leo-capability-draft/0` 是历史草案，不能作为此接口的已实现承诺。完整宿主版本范围协商、入口贡献与独立代码模块加载仍未完成，本批不整体勾选C01～C06。

## 统一描述的字段

| 记录 | 约定 |
| --- | --- |
| ModuleContract | format、稳定id、通用显示名、原模块数字version、kind、runtime、enablement、configuration、dataOwner、dependencies、actions |
| DependencyContract | id、kind、requirement、unavailableBehavior、适用动作列表。required/optional/repairable描述依赖性质，不独立授予执行权限 |
| DependencyState | 只含id和ready/missing/unknown/degraded；与定义一一对应，不记录路径、错误正文或业务值 |
| ContractAction | 原CapabilityDefinition、输入/输出说明、原输入长度上限和取消模式。动作集合必须与实际模块完全一致 |
| ParameterContract | 原请求信封、适配边界、object/value-wrapper/private-handle/legacy-value编码、原输入类型引用和动作说明 |
| ResultContract | 原ActionResult类型、Data类型说明、隐私标记与可能的证据；不是新结果信封或JSON Schema |

`Configuration=preserve-existing` 明确本批不迁移配置文件。模块版本沿用当前数字字符串（含1、1.0及多段数字）；不把宿主内部契约版本当成模块发行版本。

参数说明不代替业务校验：字段必填、真实编号、日期时区和修改确认继续由原适配器及模块负责。没有第二套JSON Schema引擎；`DataProfile` 是类型引用与开发说明，不能据此假称结果JSON已经过完整Schema校验。

## 四种能力怎样表达

| 类别 | 参数兼容 | 依赖与结果边界 |
| --- | --- | --- |
| 应用 | 结构化参数仍为applicationId；旧local_app_open归一到local:<ID>，保留原参数处理 | 文件或工作目录缺失可进入现有修复/准备路径；描述读取不启动或修复软件。accepted不等于窗口就绪 |
| 清单 | 现有Fields直接复用；其余旧动作仍通过value携带文字或JSON。描述明确指出哪些仍是JSON文字 | 共享存储Error映射degraded，不输出错误正文；修改继续走原提交回执及保存失败保护，取消前已提交可保留成功证据 |
| 足球 | force、id/league/force、ids或query继续由原适配器处理，保留view:查询形状 | 网络unknown且可用旧缓存，不因为未测网络就拒绝查询；宿主查询服务关闭显示missing，实际执行沿用原错误规则 |
| 媒体 | 空对象、path或session字段继续由原适配器处理，保留旧空值自动选择 | 未发现会话是unknown，仍可查询；已有发现快照可显示ready。PotPlayer路径仍由原模块按需解析，不从描述读取时启动客户端 |

统一描述不覆盖原`BrainCatalog`授权、模型预览确认或模块开关。依赖状态是观察结果，**不是**新的前置拦截器：缺文件不应绕过现有路径修复，网络未知不应破坏缓存回退。真正停用、配置失效或业务依赖不可用，仍由现有分发器与模块准确返回。

## 校验及兼容适配

实现 `IModuleContractSource` 的模块在Register/ReplaceBuiltin提交快照前校验；本机应用配置刷新也在整体替换前校验。缺少描述、未知格式、动作缺失/重复、策略与Describe不一致、错误适配器、结果说明缺失或不受支持证据均拒绝。恢复原异常不会改变已有注册表。

`ReadContracts` 返回已接入模块的当前描述、Enabled、ConfigurationIsCurrent及被动依赖快照；重新核验格式和依赖状态对应关系。描述或依赖探测的可恢复异常转换为不含提供者正文的 `invalid_contract`。这是开发接口，读取失败由调用者处理，不能把整个清册当成执行许可。

未实现新接口的旧内置和扩展继续走原兼容适配；不会因为没有新描述就被停用。原`LeoModule/1`导入、`Task<string>`及`ToLegacy`错误转换保持。本批不新增代码执行方式。

## 新内置模块接入顺序

1. 使用原`IStructuredLeoModule`实现执行；以`Describe`声明真实效果、合并/串行策略和超时，当前宿主不支持自动重试。
2. 原结构化参数通过`IActionRequestAdapter`转换成同一请求；保留旧字符串入口需要的映射。没有适配器的旧动作应标明legacy-only，私有会话应如实声明host-private，不为满足描述而新增调用权限。
3. 实现`DescribeModule`和`ReadDependencies`。可复用`ModuleContracts.Build`，说明原输入、输出、数据归属及依赖；依赖读取只能被动观察，不联网、不写入、不修复路径、不启动应用。
4. 经宿主可信代码调用`Registry.Register`；不能重复动作或借此替换其他内置模块。
5. 用现有`ExecuteStructuredAsync`和`ExecuteDetailedAsync`验证同一执行路径，以及缺失依赖、停用、取消和错误回执。数据提交能力继续复用已有提交规则，不能仅声明commit-aware。

最小样例只返回内存ready状态。它随源码编译供验证，未自动注册到用户环境；复制到插件目录或导入清册JSON不会执行它。独立模块交付仍遵循已有Q6数据型边界。

## 可重复生成清册

开发入口：`--contract-inventory <输出目录绝对路径>`。该入口构造固定的隔离预览宿主，再从其实际注册表生成 `GENERATED.md` 与 `inventory.json`，不读取用户应用配置。第一批4类47动作，第二批后11类91动作。

`ContractInventory.Generate` 默认导出定义，排除Enabled、配置是否当前、设备依赖状态和任何实际返回数据；应用的真实编号替换为local:example-01等示意值，名称改为通用名。清册中的别名不是可执行的应用编号，也不是可导入的模块包。可信模块作者仍须避免在静态说明中写入个人数据。

本批只开发说明、本地提交与一次必要运行更新；不改发布版本、不制作发布包、不更新GitHub发布材料、不上传。

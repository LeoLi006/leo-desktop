# Leo 开发约定

- 这是唯一正式仓库，修改 src，不再使用旧 work/leo-launcher 源码。
- 不要创建 v39/v40 等完整版本目录。用 Git 分支、提交和语义版本标签；构建到 .build/Release，安装到固定 app。
- 基线 v0.38.0。执行 scripts/Build.ps1 -Locked 和 scripts/Test.ps1；改依赖时更新锁文件与校验清单。
- 保持巴萨主题、图片宽高比、稳定窗口行为、本地指令优先。
- app、backups、模型、个人配置与缓存不进Git。不打印API Key或GitHub凭据，不使用git add -f上传个人数据。
- 安装时只关闭本仓库app中的进程，保留用户JSON和模型。用scripts/Install-Local.ps1。
- 按当前用户授权范围提交、推送和部署；每次完成的修改要有Git提交，避免源码和运行版本脱节。

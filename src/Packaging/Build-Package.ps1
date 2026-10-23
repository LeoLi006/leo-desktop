param([string]$OutputDirectory,[string]$RuntimeVersion='9.0.21')
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if(-not $OutputDirectory){$OutputDirectory=Join-Path $root '.build/distribution'}
$env:DOTNET_CLI_HOME=Join-Path $root '.cache/dotnet-home'
$env:NUGET_PACKAGES=Join-Path $root '.cache/nuget-packages'
$env:MSBuildEnableWorkloadResolver='false'
& (Join-Path $root 'scripts/Get-Dependencies.ps1')
[xml]$project=Get-Content -LiteralPath (Join-Path $root 'src/LeoLauncher.csproj')
$version=[string]$project.Project.PropertyGroup.Version
$stage=Join-Path $OutputDirectory ('Leo-'+$version+'-win-x64-'+[guid]::NewGuid().ToString('N').Substring(0,8))
$payload=Join-Path $stage 'payload'
New-Item -ItemType Directory -Path $payload -Force | Out-Null
$lock=Join-Path $root '.build/publish.packages.lock.json'
$sources=Join-Path $root '.build/publish.NuGet.Config'
$local=[System.Security.SecurityElement]::Escape((Join-Path $root 'vendor/nuget'))
"<configuration><packageSources><clear/><add key=`"local`" value=`"$local`"/><add key=`"nuget.org`" value=`"https://api.nuget.org/v3/index.json`"/></packageSources></configuration>" | Set-Content -LiteralPath $sources -Encoding utf8
$publish=@('publish',(Join-Path $root 'src/LeoLauncher.csproj'),'-c','Release','-r','win-x64','--self-contained','true','-o',$payload,'--disable-build-servers','--configfile',$sources,'/p:RestoreLockedMode=false',"/p:NuGetLockFilePath=$lock",'/p:DebugType=None','/p:DebugSymbols=false',"/p:PathMap=$root=/_/Leo",'/p:TargetLatestRuntimePatch=false',"/p:RuntimeFrameworkVersion=$RuntimeVersion")
& dotnet @publish
if($LASTEXITCODE -ne 0){throw 'Self-contained publish failed'}
# Runtime redistribution notices, alongside the existing dependency notices.
$noticeDir=Join-Path $payload 'licenses'
New-Item -ItemType Directory -Path $noticeDir -Force | Out-Null
$corePack=Join-Path $env:NUGET_PACKAGES "microsoft.netcore.app.runtime.win-x64/$RuntimeVersion"
$desktopPack=Join-Path $env:NUGET_PACKAGES "microsoft.windowsdesktop.app.runtime.win-x64/$RuntimeVersion"
Copy-Item -LiteralPath (Join-Path $corePack 'LICENSE.TXT') -Destination (Join-Path $noticeDir 'dotnet-runtime-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $corePack 'THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $noticeDir 'dotnet-runtime-THIRD-PARTY-NOTICES.txt')
Copy-Item -LiteralPath (Join-Path $desktopPack 'LICENSE') -Destination (Join-Path $noticeDir 'windowsdesktop-LICENSE.txt')
# Build from source; never read installed app or personal data.
Copy-Item -LiteralPath (Join-Path $root 'src/USER_GUIDE.md'),(Join-Path $root 'src/RELEASE_NOTES.md') -Destination $payload
# Keep supplemental docs and screenshots under the existing installer assets whitelist.
$documentation=Join-Path $payload 'assets/docs'
$screenshotDir=Join-Path $documentation 'screenshots'
New-Item -ItemType Directory -Path $screenshotDir -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $root 'src/docs/screenshots') -Filter '*.png' -File | Copy-Item -Destination $screenshotDir
# Keep all maintained guides; resolve links to packaged docs or tagged source.
$docFiles=@('QUICK_START.md','USER_GUIDE.md','RELEASE_NOTES.md','ANIMATION-NOTES.md','MODULES.md','SKIN_FORMAT.md','PERFORMANCE.md','PRODUCT_AND_DEVELOPER_GUIDE.md','ROADMAP.md','RESEARCH.md','Architecture/README.md','Localization/README.md','Assets/MatchDay/Kits/README.md')
$docFiles=@($docFiles | ForEach-Object { $_; $english=[IO.Path]::ChangeExtension($_,'en.md'); if(Test-Path -LiteralPath (Join-Path $root ('src/'+$english))){$english} })
$sourceRevision=(& git -C $root rev-parse HEAD)
if($LASTEXITCODE -ne 0 -or $sourceRevision -notmatch '^[a-f0-9]{40}$'){$sourceRevision='v'+$version}
$docMap=@{}
foreach($name in $docFiles){
 $source=[IO.Path]::GetFullPath((Join-Path $root ('src/'+$name)))
 $destination=if($name -in @('USER_GUIDE.md','RELEASE_NOTES.md')){Join-Path $payload $name}elseif($name.StartsWith('Assets/')){Join-Path $payload ('assets/'+$name.Substring(7))}else{Join-Path $documentation $name}
 $docMap[$source]=$destination
}
foreach($name in $docFiles){
 $source=[IO.Path]::GetFullPath((Join-Path $root ('src/'+$name)));$destination=$docMap[$source]
 $content=Get-Content -LiteralPath $source -Raw -Encoding utf8
 $rewrite={param($match)
  $url=$match.Groups[1].Value
  if($url -match '^[a-zA-Z][a-zA-Z0-9+.-]*:' -or $url.StartsWith('#')){return $match.Value}
  $parts=$url.Split('#',2);$anchor=if($parts.Count -gt 1){'#'+$parts[1]}else{''}
  $target=[IO.Path]::GetFullPath((Join-Path (Split-Path $source -Parent) $parts[0]))
  if($docMap.ContainsKey($target)){$link=[IO.Path]::GetRelativePath((Split-Path $destination -Parent),$docMap[$target]).Replace('\','/')}
  else{$relative=[IO.Path]::GetRelativePath($root,$target).Replace('\','/');$link="https://github.com/LeoLi006/leo-desktop/blob/$sourceRevision/$relative"}
  return ']('+$link+$anchor+')'
 }
 $content=[regex]::Replace($content,'\]\(([^)]+)\)',$rewrite)
 New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
 $content | Set-Content -LiteralPath $destination -Encoding utf8
}
Get-ChildItem -LiteralPath $payload -File | Where-Object {$_.Extension -in '.pdb','.xml'} | Remove-Item
$files=[ordered]@{}
foreach($file in Get-ChildItem -LiteralPath $payload -File -Recurse | Sort-Object FullName){
 $relative=[IO.Path]::GetRelativePath($payload,$file.FullName).Replace('\','/')
 if($relative -match '(^|/)(leo-.*\.json|.*\.(pdb|key|pem|pfx|log|onnx|wav))$' -or $relative -match '(^|/)(data|models|backups|update-backups|todo-backups)/'){throw "Unexpected personal/debug artifact: $relative"}
 if($file.Extension -eq '.json' -and $relative -notin @('LeoLauncher.runtimeconfig.json','LeoLauncher.deps.json','assets/clubs.json','assets/pinyin.json')){throw "Unexpected JSON file: $relative"}
 if($file.Extension -in '.json','.md','.txt','.cmd'){
  $content=Get-Content -LiteralPath $file.FullName -Raw
  if($content -match '(?i)C:[\\/]Users[\\/]|paratera\.com|sk-[a-zA-Z0-9_-]{20,}'){throw "Private information detected: $relative"}
 }
 $files[$relative]=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
}
@{Version=$version;Files=$files}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $payload 'release-manifest.json') -Encoding utf8
$report=Join-Path $stage 'package-verification.json'
$process=Start-Process -FilePath (Join-Path $payload 'LeoLauncher.exe') -ArgumentList @('--package-verify',('"'+$report+'"')) -WindowStyle Hidden -PassThru
if(-not $process.WaitForExit(30000)){throw 'Package verification timed out'}
if(-not(Test-Path -LiteralPath $report) -or -not (Get-Content -LiteralPath $report -Raw|ConvertFrom-Json).ok){throw 'Package verification failed'}
Remove-Item -LiteralPath $report
@'
@echo off
start "" "%~dp0payload\LeoLauncher.exe" --install-wizard
'@ | Set-Content -LiteralPath (Join-Path $stage 'Install-Leo.cmd') -Encoding ascii
@"
Leo v$version · Windows x64

1. 先完整解压，再双击 Install-Leo.cmd。
2. 选择 Leo 专用安装文件夹，首次启动按需选择模块。
3. 更新时选择同一个安装文件夹；程序与配置先备份，失败自动恢复。
4. 需要回退时，重新打开此安装入口并选择“回退上次更新”。当前个人配置保留，更新前配置副本在安装目录 update-backups 中。

包含 Windows 桌面运行时，无需安装开发环境。不含个人数据、API Key、语音模型、QQ 音乐或 Clash 客户端。
不知道功能在哪里？先打开 payload\assets\docs\QUICK_START.md，或在GitHub首页点击“快速上手”。
需要本地语音时，按安装目录 USER_GUIDE.md 的语音模型说明配置。

Extract the complete archive, then run Install-Leo.cmd and select a dedicated Leo folder.
For updates select the existing folder; the installer backs up and supports rollback.
The Windows desktop runtime is bundled. No personal data, keys, voice models or third-party clients are included.
Start with payload\assets\docs\QUICK_START.en.md, or choose Quick start on GitHub.
See USER_GUIDE.md for optional features and voice setup.
更新仅处理发布清单中的程序文件，清单、缓存、录音、模型和配置不会被覆盖。
"@ | Set-Content -LiteralPath (Join-Path $stage '使用说明.txt') -Encoding utf8
$archive=Join-Path $OutputDirectory "Leo-v$version-win-x64.zip"
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $archive -CompressionLevel Optimal -Force
$hash=(Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
"$hash  $([IO.Path]::GetFileName($archive))"|Set-Content -LiteralPath ($archive+'.sha256') -Encoding ascii
Write-Output "Clean package: $archive"
Write-Output "SHA256: $hash"

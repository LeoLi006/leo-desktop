param([switch]$SkipBuild)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(-not $SkipBuild){& (Join-Path $root 'scripts/Build.ps1') -Locked}
$folder=Join-Path $root '.build/verifyS03'
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$report=Join-Path $folder 'status-update.json'
$dll=Join-Path $root '.build/Release/LeoLauncher.dll'
$process=Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @(('"'+$dll+'"'),'--status-update-test',('"'+$report+'"')) -WindowStyle Hidden -PassThru
if(-not $process.WaitForExit(55000)){Stop-Process -Id $process.Id;throw 'S03 checks timed out'}
$result=Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
if(-not $result.ok){throw ('S03 check failed: '+(Get-Content -LiteralPath $report -Raw))}
Write-Output ('Passed S03 related checks: '+@($result.checks.PSObject.Properties).Count)

param([switch]$SkipBuild)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(-not $SkipBuild){& (Join-Path $root 'scripts/Build.ps1') -Locked}
$folder=Join-Path $root '.build/verify102'
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$dll=Join-Path $root '.build/Release/LeoLauncher.dll'
$total=0
foreach($case in @(@('--interaction-batch-test','interaction-batch.json'),@('--dock-animation-test','dock-animation.json'),@('--profile-batch-test','profile-batch.json'),@('--status-update-test','status-update.json'),@('--entry-display-test','entry-display.json'))){
 $report=Join-Path $folder $case[1]
 $process=Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @(('"'+$dll+'"'),$case[0],('"'+$report+'"')) -WindowStyle Hidden -PassThru
 if(-not $process.WaitForExit(55000)){Stop-Process -Id $process.Id;throw ('Check timed out: '+$case[0])}
 $result=Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
 if(-not $result.ok){throw ('Check failed: '+(Get-Content -LiteralPath $report -Raw))}
 $count=@($result.checks.PSObject.Properties).Count
 $total+=$count
 Write-Output ($case[1]+': '+$count+' passed')
}
Write-Output ('Passed related checks: '+$total)

param([switch]$SkipBuild)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
if(-not $SkipBuild){& (Join-Path $repo 'scripts/Build.ps1') -Locked}
$folder=Join-Path $repo '.build/quiet-planner'
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$dll=Join-Path $repo '.build/Release/LeoLauncher.dll';$total=0
foreach($case in @('quiet-planner','feedback-pointer','platform53')){
 $report=Join-Path $folder ($case+'.json')
 if(Test-Path -LiteralPath $report){Remove-Item -LiteralPath $report}
 $job=Start-Process (Get-Command dotnet).Source -ArgumentList @(('"'+$dll+'"'),('--'+$case+'-test'),('"'+$report+'"')) -WindowStyle Hidden -PassThru
 if(-not $job.WaitForExit(55000)){Stop-Process -Id $job.Id;throw ('Timed out: '+$case)}
 if($job.ExitCode -ne 0 -or -not(Test-Path -LiteralPath $report)){throw ('No completed report: '+$case)}
 $result=Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
 if(-not $result.ok){throw (Get-Content -LiteralPath $report -Raw)}
 $count=@($result.checks.PSObject.Properties).Count;$total+=$count;Write-Output ($case+': '+$count+' passed')
}
Write-Output ('Related checks passed: '+$total)

param([switch]$SkipBuild)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(-not $SkipBuild){& (Join-Path $root 'scripts/Build.ps1') -Locked}
$folder=Join-Path $root '.build/verify1021'
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$dll=Join-Path $root '.build/Release/LeoLauncher.dll'
$total=0
foreach($case in @('feedback-pointer','proxy-feature','dock-animation')){
 $report=Join-Path $folder ($case+'.json')
 if(Test-Path -LiteralPath $report){Remove-Item -LiteralPath $report}
 $process=Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @(('"'+$dll+'"'),('--'+$case+'-test'),('"'+$report+'"')) -WindowStyle Hidden -PassThru
 if(-not $process.WaitForExit(55000)){Stop-Process -Id $process.Id;throw ('Check timed out: '+$case)}
 if($process.ExitCode -ne 0 -or -not(Test-Path -LiteralPath $report)){throw ('Check did not complete: '+$case)}
 $result=Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
 if(-not $result.ok){throw ('Check failed: '+(Get-Content -LiteralPath $report -Raw))}
 $count=@($result.checks.PSObject.Properties).Count;$total+=$count
 Write-Output ($case+': '+$count+' passed')
}
Write-Output ('Passed related checks: '+$total)

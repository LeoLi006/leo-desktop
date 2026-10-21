param([switch]$SkipBuild)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
if(-not $SkipBuild){& (Join-Path $repo 'scripts/Build.ps1') -Locked}
$reports=Join-Path $repo '.build/delivery-recovery'
New-Item -ItemType Directory -Force $reports|Out-Null
$report=Join-Path $reports 'final.json'
$test=Start-Process dotnet -ArgumentList ('"'+(Join-Path $repo '.build/Release/LeoLauncher.dll')+'"'),'--delivery-recovery-test',('"'+$report+'"') -WorkingDirectory $reports -WindowStyle Hidden -PassThru
if(-not $test.WaitForExit(45000)){$test.Kill();throw 'Delivery recovery verification timed out'}
$result=Get-Content -LiteralPath $report -Raw|ConvertFrom-Json
$failed=@($result.checks.psobject.Properties|Where-Object{-not $_.Value})
if(-not $result.ok -or $failed.Count){throw ('Delivery recovery checks failed: '+$result.error+' '+($failed.Name -join ', '))}
[pscustomobject]@{Passed=$result.ok;Checks=@($result.checks.psobject.Properties).Count}

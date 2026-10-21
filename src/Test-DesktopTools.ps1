param([switch]$SkipBuild)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
if(-not $SkipBuild){& (Join-Path $repo 'scripts/Build.ps1') -Locked}
$reports=Join-Path $repo '.build/desktop-tools'
New-Item -ItemType Directory -Force $reports|Out-Null
$report=Join-Path $reports 'final.json'
$test=Start-Process dotnet -ArgumentList ('"'+(Join-Path $repo '.build/Release/LeoLauncher.dll')+'"'),'--desktop-tools-test',('"'+$report+'"') -WorkingDirectory $reports -WindowStyle Hidden -PassThru
if(-not $test.WaitForExit(45000)){$test.Kill();throw 'Desktop tools verification timed out'}
$result=Get-Content -LiteralPath $report -Raw|ConvertFrom-Json
if(-not $result.ok){throw ('Desktop tools checks failed: '+$result.error+' '+(($result.checks.psobject.Properties|Where-Object{-not $_.Value}).Name -join ', '))}
[pscustomobject]@{Passed=$result.ok;Checks=@($result.checks.psobject.Properties).Count}

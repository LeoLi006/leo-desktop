param([switch]$SkipBuild)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
if(-not $SkipBuild){& (Join-Path $repo 'scripts/Build.ps1') -Locked}
$reports=Join-Path $repo '.build/extension-contracts'
New-Item -ItemType Directory -Force $reports|Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if(-not(Test-Path -LiteralPath $compiler)){throw 'The isolated worker fixture requires the Windows .NET Framework compiler.'}
& $compiler /nologo /target:exe ('/out:'+(Join-Path $reports 'worker.exe')) (Join-Path $repo 'src/Architecture/Extensions/WorkerFixture.cs.txt')
if($LASTEXITCODE -ne 0){throw 'Worker fixture compilation failed'}
$report=Join-Path $reports 'final.json'
$test=Start-Process dotnet -ArgumentList ('"'+(Join-Path $repo '.build/Release/LeoLauncher.dll')+'"'),'--extension-contract-test',('"'+$report+'"') -WorkingDirectory $reports -WindowStyle Hidden -PassThru
if(-not $test.WaitForExit(55000)){$test.Kill();throw 'Extension contract verification timed out'}
$result=Get-Content -LiteralPath $report -Raw|ConvertFrom-Json
if(-not $result.ok){throw ('Extension contract checks failed: '+$result.error)}
[pscustomobject]@{Passed=$result.ok;Checks=@($result.checks.psobject.Properties).Count}

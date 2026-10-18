param([switch]$SkipBuild)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
if(-not $SkipBuild){& (Join-Path $repo 'scripts/Build.ps1') -Locked}
$reports=Join-Path $repo '.build/verify51'
New-Item -ItemType Directory -Path $reports -Force|Out-Null
$fixtureSource=Join-Path $reports 'WorkerFixture.cs'
$fixtureCode=@"
class LeoWorkerFixture {
 static void Main() {
  string line=System.Console.ReadLine();
  if(line!=null && line.Contains("\"command\":\"slow\""))System.Threading.Thread.Sleep(30000);
  System.Console.WriteLine("{\"ok\":true,\"message\":\"worker fixture completed\"}");
 }
}
"@
$fixtureCode|Set-Content -LiteralPath $fixtureSource -Encoding utf8
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if(-not(Test-Path -LiteralPath $compiler)){throw 'The test fixture needs the Windows .NET Framework C# compiler.'}
& $compiler /nologo /target:exe ('/out:'+(Join-Path $reports 'worker.exe')) $fixtureSource
if($LASTEXITCODE -ne 0){throw 'Worker fixture compilation failed'}
$report=Join-Path $reports 'platform51.json'
$test=Start-Process -FilePath (Join-Path $repo '.build/Release/LeoLauncher.exe') -ArgumentList @('--platform51-test',$report) -WindowStyle Hidden -PassThru
if(-not $test.WaitForExit(50000)){throw 'Focused platform tests timed out'}
$result=Get-Content -LiteralPath $report -Raw|ConvertFrom-Json
if(-not $result.ok){throw ('Focused tests failed: '+$result.error)}
[pscustomobject]@{Passed=$result.ok;Checks=@($result.checks.psobject.Properties).Count;Report=$report}

param([switch]$SkipBuild,[switch]$Measure,[switch]$Soak)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
if(-not $SkipBuild){& (Join-Path $repo 'scripts/Build.ps1') -Locked}
$folder=Join-Path $repo '.build/robustness'
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$fixture=Join-Path $folder 'WorkerFixture.cs'
@'
class LeoWorkerFixture {
 static void Main() {
  string line=System.Console.ReadLine();
  if(line!=null && line.Contains("\"command\":\"slow\""))System.Threading.Thread.Sleep(30000);
  System.Console.WriteLine("{\"ok\":true,\"message\":\"worker fixture completed\"}");
 }
}
'@ | Set-Content -LiteralPath $fixture -Encoding utf8
& (Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe') /nologo /target:exe ('/out:'+(Join-Path $folder 'worker.exe')) $fixture
if($LASTEXITCODE -ne 0){throw 'Worker fixture compilation failed'}
$dll=Join-Path $repo '.build/Release/LeoLauncher.dll'
$cases=@('robustness','feedback-pointer','dock-animation','interaction-batch','profile-batch','platform51','client-operations','planner-feature','pet-transparency','recovery45')
if($Measure){$cases+='performance'}
if($Soak){$cases+='soak'}
$total=0
foreach($case in $cases){
 $report=Join-Path $folder ($case+'.json')
 if(Test-Path -LiteralPath $report){Remove-Item -LiteralPath $report}
 $job=Start-Process (Get-Command dotnet).Source -ArgumentList @(('"'+$dll+'"'),('--'+$case+'-test'),('"'+$report+'"')) -WindowStyle Hidden -PassThru
 $watch=[Diagnostics.Stopwatch]::StartNew();$limit=if($case -eq 'soak'){240}else{55}
 while(-not $job.WaitForExit(1000)){if($watch.Elapsed.TotalSeconds -gt $limit){Stop-Process -Id $job.Id;throw ('Timeout: '+$case)}}
 if($job.ExitCode -ne 0 -or -not(Test-Path -LiteralPath $report)){throw ('No completed report: '+$case)}
 $result=Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
 if(-not $result.ok){throw ('Failed '+$case+': '+(Get-Content -LiteralPath $report -Raw))}
 $count=@($result.checks.PSObject.Properties).Count;$total+=$count
 Write-Output ($case+': passed ('+$count+' checks)')
}
Write-Output ('Related checks passed: '+$total)

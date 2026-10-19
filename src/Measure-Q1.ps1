param([int]$Runs=3,[int]$LiveSeconds=30,[switch]$LiveOnly,[ValidateSet('preview','common','companion')][string[]]$Profiles=@('preview','companion','common'))
$ErrorActionPreference='Stop'
if($Runs -lt 1 -or $Runs -gt 10 -or $LiveSeconds -lt 5 -or $LiveSeconds -gt 120){throw 'Runs 1..10; LiveSeconds 5..120'}
$repo=Split-Path $PSScriptRoot -Parent
$out=Join-Path $repo '.build/q1'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$dll=Join-Path $repo '.build/Release/LeoLauncher.dll'
if(-not(Test-Path -LiteralPath $dll)){throw 'Build first using scripts/Build.ps1 -Locked'}
foreach($profile in $Profiles){for($i=1;(-not $LiveOnly) -and $i -le $Runs;$i++){
    $report=Join-Path $out "$profile-$i.json"
    if(Test-Path -LiteralPath $report){Remove-Item -LiteralPath $report}
    $mode=switch($profile){'common'{'--baseline-runtime-test'}'companion'{'--baseline-companion-test'}default{'--baseline-test'}}
    $layout=Join-Path $repo '.build/Release/leo-window-layouts.json'
    $layoutBytes=if(Test-Path -LiteralPath $layout){[IO.File]::ReadAllBytes($layout)}else{$null}
    try{
        $process=Start-Process dotnet -ArgumentList ('"'+$dll+'"'),$mode,('"'+$report+'"') -WindowStyle Hidden -PassThru
        if(-not $process.WaitForExit(55000)){$process.Kill();$process.WaitForExit();throw 'Baseline timed out'}
    }finally{
        # Fixture window placement must never leak into a later local install.
        if($null -ne $layoutBytes){[IO.File]::WriteAllBytes($layout,$layoutBytes)}
        elseif(Test-Path -LiteralPath $layout){Remove-Item -LiteralPath $layout}
    }
    if(-not(Test-Path -LiteralPath $report)){throw 'Preview report missing'}
    $sample=Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    if($process.ExitCode -ne 0 -or -not $sample.ok){throw "$profile baseline failed: run $i"}
    Write-Output "$profile baseline $i/$Runs complete."
}}
# Read only resource counters of this repository's installed Leo. No titles, paths,
# settings, module arguments, clipboard, key material, or user documents are exported.
$exe=Join-Path $repo 'app/LeoLauncher.exe'
$live=@(Get-Process LeoLauncher -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq $exe})
if($live.Count -ne 1){Write-Output 'Live sample skipped: expected exactly one installed Leo process.';return}
$p=$live[0];$p.Refresh();$cpu=$p.TotalProcessorTime;$clock=[Diagnostics.Stopwatch]::StartNew();$rows=@()
for($i=0;$i -lt $LiveSeconds;$i++){
    Start-Sleep -Milliseconds 1000
    $p.Refresh();if($p.HasExited){throw 'Installed Leo exited during sampling'}
    $rows+=@{seconds=$clock.Elapsed.TotalSeconds;cpu_ms=($p.TotalProcessorTime-$cpu).TotalMilliseconds;private_bytes=$p.PrivateMemorySize64;working_set=$p.WorkingSet64;handles=$p.HandleCount;threads=$p.Threads.Count}
}
@{format='leo-q1-live-1';mode='passive-existing-process';samples=$rows;limitations=@('Existing user configuration and ambient desktop activity are uncontrolled. No profile or module is switched.','No managed allocations, GDI/USER breakdown or network request counts are collected across processes.','This is a current live-state observation, not a pure-pet/common-profile comparison or a cold-start measurement.')} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $out 'live.json') -Encoding UTF8
Write-Output 'Passive live baseline complete.'

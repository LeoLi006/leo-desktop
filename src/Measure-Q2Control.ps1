$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$out=Join-Path $root '.build/q2'
$build=Join-Path $root '.build/Release'
$old=Join-Path $root 'app/LeoLauncher.dll'
$current=Join-Path $build 'LeoLauncher.dll'
if((Get-FileHash -LiteralPath $old).Hash -eq (Get-FileHash -LiteralPath $current).Hash){throw 'Run before local deployment; installed control must be the previous binary'}
foreach($asset in @('leo.png','vertical-drag.png','edge-motion-v1.png','edge-bridge-v1.png')){
    $before=Join-Path $root ('app/assets/'+$asset);$after=Join-Path $build ('assets/'+$asset)
    if((Get-FileHash -LiteralPath $before).Hash -ne (Get-FileHash -LiteralPath $after).Hash){throw 'Control assets differ'}
}
for($i=1;$i -le 3;$i++){foreach($kind in @('old','new')){
    $report=Join-Path $out "matched-$kind-$i.json"
    if(Test-Path -LiteralPath $report){Remove-Item -LiteralPath $report}
    $dll=if($kind -eq 'old'){$old}else{$current}
    $p=Start-Process dotnet -ArgumentList 'exec','--runtimeconfig',('"'+(Join-Path $build 'LeoLauncher.runtimeconfig.json')+'"'),'--depsfile',('"'+(Join-Path $build 'LeoLauncher.deps.json')+'"'),('"'+$dll+'"'),'--performance-test',('"'+$report+'"') -WindowStyle Hidden -PassThru
    if(-not $p.WaitForExit(55000)){$p.Kill();throw 'Matched control timed out'}
    $r=Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    if(-not $r.ok){throw 'Matched control failed'}
    Write-Output "$kind control $i/3 complete."
}}
@{same_assets=$true;old_binary_hash=(Get-FileHash -LiteralPath $old).Hash;new_binary_hash=(Get-FileHash -LiteralPath $current).Hash;method='Existing unchanged --performance-test, paired old/new order; explicit same framework runtime/deps; preview fixtures only.'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $out 'control-method.json') -Encoding UTF8

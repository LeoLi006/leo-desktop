param([int]$Runs=3)
$ErrorActionPreference='Stop'
if($Runs -lt 3 -or $Runs -gt 10){throw 'Use 3..10 samples'}
$root=Split-Path $PSScriptRoot -Parent
$out=Join-Path $root '.build/q2'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$dll=Join-Path $root '.build/Release/LeoLauncher.dll'
for($i=1;$i -le $Runs;$i++){
    $report=Join-Path $out "preview-$i.json"
    if(Test-Path -LiteralPath $report){Remove-Item -LiteralPath $report}
    $p=Start-Process dotnet -ArgumentList ('"'+$dll+'"'),'--baseline-test',('"'+$report+'"') -WindowStyle Hidden -PassThru
    if(-not $p.WaitForExit(55000)){$p.Kill();throw 'Q2 baseline timed out'}
    $result=Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    if(-not $result.ok){throw "Q2 baseline failed: $i"}
    Write-Output "Q2 baseline $i/$Runs complete."
}

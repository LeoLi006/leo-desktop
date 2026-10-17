$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$exe=Join-Path $root '.build/Release/LeoLauncher.exe'
if(-not(Test-Path -LiteralPath $exe)){throw 'Build first'}
$out=Join-Path $root '.build/test-results'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$results=@()
foreach($mode in @('upgrade38-test','upgrade37-test','upgrade36-test','upgrade35-test','planner-feature-test','quick-self-test','upgrade-self-test')){
 $file=Join-Path $out ($mode+'.json')
 $process=Start-Process -FilePath $exe -ArgumentList @('--'+$mode,$file) -WindowStyle Hidden -Wait -PassThru
 if($process.ExitCode -ne 0 -or -not(Test-Path -LiteralPath $file)){throw "Test process failed: $mode"}
 $result=Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
 $results+=[pscustomobject]@{Test=$mode;OK=$result.ok}
 if(-not $result.ok){throw "Check failed: $mode"}
}
$results | Format-Table -AutoSize

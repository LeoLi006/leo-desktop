param([switch]$Start)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$build=Join-Path $root '.build/Release'
$app=Join-Path $root 'app'
$exe=Join-Path $app 'LeoLauncher.exe'
if(-not(Test-Path -LiteralPath (Join-Path $build 'LeoLauncher.exe'))){throw 'Build first'}
foreach($process in @(Get-Process LeoLauncher -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq $exe})){
 if($process.CloseMainWindow()){$null=$process.WaitForExit(3000)}
 if(-not $process.HasExited){Stop-Process -Id $process.Id -Force}
}
New-Item -ItemType Directory -Path $app -Force | Out-Null
Get-ChildItem -LiteralPath $build | Copy-Item -Destination $app -Recurse -Force
if($Start){Start-Process -FilePath $exe -WorkingDirectory $app -WindowStyle Hidden}
Write-Output "Installed to $app (personal data preserved)."

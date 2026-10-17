$root=Split-Path $PSScriptRoot -Parent
$exe=Join-Path $root 'app/LeoLauncher.exe'
if(-not(Test-Path -LiteralPath $exe)){throw 'Build and install first'}
Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe -Parent) -WindowStyle Hidden

$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.IO.Compression.FileSystem
$manifest=Get-Content -LiteralPath (Join-Path $root 'vendor/packages.json') -Raw | ConvertFrom-Json
foreach($package in $manifest){$file=Join-Path $root ('vendor/nuget/'+$package.file);if((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $package.sha256){throw "Checksum failed: $($package.file)"}}
$packages=@{'naudio.core.2.2.1.nupkg'='naudio.core';'naudio.winmm.2.2.1.nupkg'='naudio.winmm';'org.k2fsa.sherpa.onnx.1.13.8.nupkg'='sherpa';'org.k2fsa.sherpa.onnx.runtime.win-x64.1.13.8.nupkg'='native'}
foreach($name in $packages.Keys){$dir=Join-Path $root ('.deps/'+$packages[$name]);$stamp=Join-Path $dir '.ready';if(Test-Path -LiteralPath $stamp){continue};New-Item -ItemType Directory -Path $dir -Force | Out-Null;[IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $root ('vendor/nuget/'+$name)),$dir);Set-Content -LiteralPath $stamp -Value $name}
Write-Output 'Pinned dependencies verified.'

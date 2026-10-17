$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$config=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'model.json') -Raw | ConvertFrom-Json
$dest=Join-Path $root 'app/models/sensevoice'
$ready=$true
foreach($entry in $config.files.PSObject.Properties){$file=Join-Path $dest $entry.Name;if(-not(Test-Path -LiteralPath $file) -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $entry.Value){$ready=$false;break}}
if($ready){Write-Output 'Voice model verified.';return}
$cache=Join-Path $root '.cache/model-download'
New-Item -ItemType Directory -Path $cache -Force | Out-Null
$archive=Join-Path $cache ($config.name+'.tar.bz2')
if(-not(Test-Path -LiteralPath $archive)){Invoke-WebRequest -Uri $config.url -OutFile $archive -UseBasicParsing}
if((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $config.archive_sha256){throw 'Model archive checksum failed'}
& tar -xjf $archive -C $cache ($config.name+'/model.int8.onnx') ($config.name+'/tokens.txt')
if($LASTEXITCODE -ne 0){throw 'Model extraction failed'}
foreach($entry in $config.files.PSObject.Properties){if((Get-FileHash -LiteralPath (Join-Path $cache ($config.name+'/'+$entry.Name)) -Algorithm SHA256).Hash -ne $entry.Value){throw 'Extracted model checksum failed'}}
New-Item -ItemType Directory -Path $dest -Force | Out-Null
foreach($entry in $config.files.PSObject.Properties){Copy-Item -LiteralPath (Join-Path $cache ($config.name+'/'+$entry.Name)) -Destination $dest -Force}
Write-Output 'SenseVoice installed.'

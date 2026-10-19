param([switch]$SkipBuild)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(-not $SkipBuild){& (Join-Path $root 'scripts/Build.ps1') -Locked}
$folder=Join-Path $root '.build/verify53'
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$report=Join-Path $folder 'platform53.json'
$dll=Join-Path $root '.build/Release/LeoLauncher.dll'
# Explicit host avoids an old self-contained host left in the incremental build directory.
$process=Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @(('"'+$dll+'"'),'--platform53-test',('"'+$report+'"')) -WindowStyle Hidden -PassThru
if(-not $process.WaitForExit(55000)){Stop-Process -Id $process.Id;throw 'Focused check timed out'}
$result=Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
if(-not $result.ok){throw ('Focused check failed: '+(Get-Content -LiteralPath $report -Raw))}
Write-Output ('Passed related checks: '+@($result.checks.PSObject.Properties).Count)

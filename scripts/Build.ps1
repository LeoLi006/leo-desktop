param([switch]$Locked)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'Get-Dependencies.ps1')
$env:DOTNET_CLI_HOME=Join-Path $root '.cache/dotnet-home'
$env:NUGET_PACKAGES=Join-Path $root '.cache/nuget-packages'
$env:MSBuildEnableWorkloadResolver='false'
Push-Location $root
try{
 $restore=@('restore','src/LeoLauncher.csproj','--configfile','NuGet.Config','--disable-parallel')
 if($Locked){$restore+='--locked-mode'}
 & dotnet @restore
 if($LASTEXITCODE -ne 0){throw 'Restore failed'}
 & dotnet build src/LeoLauncher.csproj -c Release -o .build/Release --no-restore --disable-build-servers
 if($LASTEXITCODE -ne 0){throw 'Build failed'}
}finally{Pop-Location}

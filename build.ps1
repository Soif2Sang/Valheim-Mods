param(
 [Parameter(Mandatory=$true)][string]$GamePath,
 [Parameter(Mandatory=$true)][string]$CompilerPath
)
$ErrorActionPreference='Stop'
$output=Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force $output | Out-Null
foreach($name in @('BiomeClarity','ProgressiveMap','QuickConnectCrossplayFix')){
 & (Join-Path $PSScriptRoot "mods/$name/build.ps1") -GamePath $GamePath
}
$managed=Join-Path $GamePath 'valheim_Data/Managed'
$refs=@(Get-ChildItem $managed -Filter '*.dll' | ForEach-Object {'/reference:'+$_.FullName})
foreach($name in @('BepInEx.dll','0Harmony.dll')){$refs+='/reference:'+(Join-Path $GamePath "BepInEx/core/$name")}
& $CompilerPath /nologo /nostdlib+ /target:library "/out:$output/CraftFromContainers.dll" @refs (Join-Path $PSScriptRoot 'upstream/CraftFromContainers/BepInExPlugin.cs') (Join-Path $PSScriptRoot 'upstream/CraftFromContainers/DrawerInfo.cs')
if($LASTEXITCODE -ne 0){throw 'CraftFromContainers compilation failed'}
Get-FileHash (Join-Path $output '*.dll') -Algorithm SHA256

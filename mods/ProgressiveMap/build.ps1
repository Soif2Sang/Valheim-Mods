param([Parameter(Mandatory=$true)][string]$GamePath)
$ErrorActionPreference='Stop'
$managed=Join-Path $GamePath 'valheim_Data/Managed'
$core=Join-Path $GamePath 'BepInEx/core'
$output=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../dist/ProgressiveMap.dll'))
$argsList=@('/noconfig','/nologo','/nostdlib+','/target:library','/optimize+',"/out:$output")
foreach($name in @('netstandard.dll','mscorlib.dll','System.dll','System.Core.dll','UnityEngine.dll','UnityEngine.CoreModule.dll','UnityEngine.UI.dll','UnityEngine.UIModule.dll','Unity.TextMeshPro.dll','assembly_valheim.dll','assembly_utils.dll')){$argsList+='/reference:'+(Join-Path $managed $name)}
foreach($name in @('BepInEx.dll','0Harmony.dll')){$argsList+='/reference:'+(Join-Path $core $name)}
& (Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe') @argsList (Join-Path $PSScriptRoot 'Plugin.cs')
if($LASTEXITCODE -ne 0){throw 'ProgressiveMap compilation failed'}
Get-FileHash $output -Algorithm SHA256


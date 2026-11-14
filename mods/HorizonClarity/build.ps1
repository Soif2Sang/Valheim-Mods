param([Parameter(Mandatory=$true)][string]$GamePath)
$ErrorActionPreference='Stop'
$output=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../dist'))
New-Item -ItemType Directory -Force $output | Out-Null
$managed=Join-Path $GamePath 'valheim_Data/Managed';$core=Join-Path $GamePath 'BepInEx/core'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$argsList=@('/noconfig','/nologo','/nostdlib+','/target:library','/optimize+',"/out:$output/HorizonClarity.dll")
foreach($name in @('netstandard.dll','mscorlib.dll','System.dll','System.Core.dll','UnityEngine.dll','UnityEngine.CoreModule.dll','assembly_valheim.dll')){$argsList+='/reference:'+(Join-Path $managed $name)}
foreach($name in @('BepInEx.dll','0Harmony.dll')){$argsList+='/reference:'+(Join-Path $core $name)}
& $compiler @argsList (Join-Path $PSScriptRoot 'Plugin.cs') (Join-Path $PSScriptRoot 'Policy.cs')
if($LASTEXITCODE -ne 0){throw 'HorizonClarity compilation failed'}
& $compiler /nologo /target:exe "/out:$output/HorizonPolicyTests.exe" (Join-Path $PSScriptRoot 'Policy.cs') (Join-Path $PSScriptRoot 'tests/PolicyTests.cs')
if($LASTEXITCODE -ne 0){throw 'Horizon tests compilation failed'}
& (Join-Path $output 'HorizonPolicyTests.exe')
if($LASTEXITCODE -ne 0){throw 'Horizon policy tests failed'}
Get-FileHash (Join-Path $output 'HorizonClarity.dll')

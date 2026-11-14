param([Parameter(Mandatory=$true)][string]$GamePath)
$ErrorActionPreference='Stop'
$core=Join-Path $GamePath 'BepInEx/core'
$managed=Join-Path $GamePath 'valheim_Data/Managed'
$output=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../dist/QuickConnectCrossplayFix.dll'))
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$argsList=@('/noconfig','/nologo','/nostdlib+','/target:library','/optimize+',"/out:$output")
foreach($name in @('netstandard.dll','mscorlib.dll','System.dll','System.Core.dll','UnityEngine.dll','UnityEngine.CoreModule.dll')){$argsList+='/reference:'+(Join-Path $managed $name)}
foreach($name in @('BepInEx.dll','0Harmony.dll')){$argsList+='/reference:'+(Join-Path $core $name)}
& $compiler @argsList (Join-Path $PSScriptRoot 'Plugin.cs') (Join-Path $PSScriptRoot 'Identity.cs')
if($LASTEXITCODE -ne 0){throw 'Crossplay fix compilation failed'}
$test=Join-Path $PSScriptRoot '../../dist/QuickConnectIdentityTests.exe'
& $compiler /nologo /target:exe "/out:$test" (Join-Path $PSScriptRoot 'Identity.cs') (Join-Path $PSScriptRoot 'tests/IdentityTests.cs')
if($LASTEXITCODE -ne 0){throw 'Test compilation failed'}
& $test
if($LASTEXITCODE -ne 0){throw 'Identity tests failed'}
Get-FileHash $output -Algorithm SHA256

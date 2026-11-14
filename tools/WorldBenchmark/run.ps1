param(
 [Parameter(Mandatory=$true)][string]$GamePath,
 [Parameter(Mandatory=$true)][string]$WorldDirectory,
 [Parameter(Mandatory=$true)][string]$ClientPack,
 [ValidateSet('NativeFog','ClearFog','FarLands')][string]$Profile='FarLands',
 [switch]$AllowConcurrentClient
)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$sourceWorld=[IO.Path]::GetFullPath($WorldDirectory).TrimEnd([IO.Path]::DirectorySeparatorChar,[IO.Path]::AltDirectorySeparatorChar)
if(!(Test-Path -LiteralPath $sourceWorld -PathType Container) -or !(Get-ChildItem -LiteralPath $sourceWorld -Filter '_main.*.ok' -File)){throw 'Expected a complete chunked world directory with an .ok marker'}
$concurrent=@(Get-CimInstance Win32_Process -Filter "Name='valheim.exe'").Count -gt 0
if($concurrent -and !$AllowConcurrentClient){throw 'Close Valheim for meaningful measurements or use -AllowConcurrentClient for a provisional load/render check.'}
$run=Join-Path $root ('dist/world-benchmark/'+(Get-Date -Format yyyyMMdd-HHmmss)+'-'+$Profile)
$client=Join-Path $run 'client'
New-Item -ItemType Directory -Force $client,(Join-Path $run 'saves/worlds_local') | Out-Null
$files=[ordered]@{}
foreach($file in Get-ChildItem $sourceWorld -File){$files[$file.Name]=(Get-FileHash $file.FullName).Hash.ToLowerInvariant()}
Copy-Item -LiteralPath $sourceWorld -Destination (Join-Path $run 'saves/worlds_local') -Recurse
foreach($name in $files.Keys){
 $copied=Join-Path (Join-Path $run ('saves/worlds_local/'+[IO.Path]::GetFileName($sourceWorld))) $name
 if((Get-FileHash $copied).Hash.ToLowerInvariant() -ne $files[$name]){throw 'Working world copy differs from snapshot'}
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::ExtractToDirectory([IO.Path]::GetFullPath($ClientPack),$client)
$farlands=Join-Path $client 'BepInEx/plugins/FarLands/FarLands.dll'
if($Profile -eq 'FarLands'){
 if(!(Test-Path $farlands)){
  $disabled=@(Get-ChildItem (Split-Path $farlands) -Filter 'FarLands.dll.disabled*' -File)
  if($disabled.Count -ne 1){throw 'FarLands reference requires exactly one active or disabled FarLands DLL in the pack'}
  Copy-Item -LiteralPath $disabled[0].FullName -Destination $farlands
 }
 Set-Content (Join-Path $client 'BepInEx/config/ru.ivest.farlands.cfg') -Encoding UTF8 -Value "[General]`nEnabled = true`nTerrainDistance = 4000`nForestDistance = 2500`n[Buildings]`nEnabled = false"
}elseif(Test-Path $farlands){throw 'NativeFog and ClearFog require FarLands disabled in the supplied pack'}
foreach($name in @('valheim.exe','UnityPlayer.dll','steam_appid.txt')){Copy-Item -LiteralPath (Join-Path $GamePath $name) -Destination $client -Force}
foreach($name in @('valheim_Data','MonoBleedingEdge','doorstop_libs')){
 if(!(Test-Path (Join-Path $client $name))){New-Item -ItemType Junction -Path (Join-Path $client $name) -Target (Join-Path $GamePath $name) | Out-Null}
}
# Steady-state measurements exclude map generation spread across frames in both profiles.
$progressive=Join-Path $client 'BepInEx/plugins/ProgressiveMap/ProgressiveMap.dll'
if(Test-Path $progressive){Rename-Item -LiteralPath $progressive -NewName 'ProgressiveMap.dll.disabled-benchmark'}
$horizonConfig=Join-Path $client 'BepInEx/config/local.valheim.horizonclarity.cfg'
$enabled=if($Profile -eq 'ClearFog'){'true'}else{'false'}
Set-Content $horizonConfig -Encoding UTF8 -Value "[Display]`nEnabled = $enabled`nFogMultiplier = 0.25`nViewDistance = 4000"
$managed=Join-Path $GamePath 'valheim_Data/Managed'
$refs=@(Get-ChildItem $managed -Filter '*.dll' | ForEach-Object {'/reference:'+$_.FullName})
foreach($name in @('BepInEx.dll','0Harmony.dll')){$refs+='/reference:'+(Join-Path $GamePath "BepInEx/core/$name")}
& (Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe') /nologo /noconfig /nostdlib+ /target:library "/out:$client/BepInEx/plugins/WorldBenchmark.dll" @refs (Join-Path $PSScriptRoot 'Benchmark.cs')
if($LASTEXITCODE -ne 0){throw 'Benchmark compilation failed'}
[ordered]@{profile=$Profile;concurrentClient=$concurrent;hiddenWindow=$true;performanceRepresentative=$false;reason='Hidden isolated local host; concurrent game and world simulation can bias timing. Compare only controlled runs, not normal-server FPS.';worldFiles=$files;clientPackSha256=(Get-FileHash $ClientPack).Hash.ToLowerInvariant();probeSha256=(Get-FileHash (Join-Path $PSScriptRoot 'Benchmark.cs')).Hash.ToLowerInvariant()} | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $run 'inputs.json') -Encoding UTF8
. (Join-Path $PSScriptRoot '../MapValidation/DisplayPreferences.ps1')
$before=Get-MapDisplayPreferences
$before | Export-Clixml (Join-Path $run 'display-before.xml')
$oldOut=$env:VALHEIM_BENCH_OUTPUT;$oldName=$env:VALHEIM_BENCH_WORLD
$env:VALHEIM_BENCH_OUTPUT=$run;$env:VALHEIM_BENCH_WORLD=[IO.Path]::GetFileName($sourceWorld)
try {
 $process=Start-Process -FilePath (Join-Path $client 'valheim.exe') -WorkingDirectory $client -WindowStyle Hidden -PassThru -ArgumentList @('-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-logFile',('"'+(Join-Path $run 'Unity.log')+'"'))
}catch{Restore-MapDisplayPreferences -Snapshot $before;throw}finally{$env:VALHEIM_BENCH_OUTPUT=$oldOut;$env:VALHEIM_BENCH_WORLD=$oldName}
Write-Output "World copy validation PID $($process.Id); reports: $run"
try {
 $deadline=(Get-Date).AddSeconds(480)
 while(!$process.HasExited){if((Get-Date) -gt $deadline){throw 'Benchmark timed out'};Start-Sleep -Seconds 2;$process.Refresh()}
}finally{
 if(!$process.HasExited){$process.Kill();$process.WaitForExit()}
 Restore-MapDisplayPreferences -Snapshot $before
 $after=Get-MapDisplayPreferences;$after | Export-Clixml (Join-Path $run 'display-after.xml')
 if(($before | ConvertTo-Json -Depth 6 -Compress) -ne ($after | ConvertTo-Json -Depth 6 -Compress)){throw 'Display preferences changed'}
}
foreach($name in $files.Keys){if((Get-FileHash (Join-Path $sourceWorld $name)).Hash.ToLowerInvariant() -ne $files[$name]){throw 'Original snapshot changed'}}
if(Test-Path (Join-Path $run 'FAILED.txt')){throw (Get-Content (Join-Path $run 'FAILED.txt') -Raw)}
if($process.ExitCode -ne 0){throw "Benchmark exited with code $($process.ExitCode)"}
if(!(Test-Path (Join-Path $run 'PASS.json'))){throw 'Benchmark report missing'}
if($Profile -eq 'FarLands' -and !(Test-Path (Join-Path $run 'farlands-ready.json'))){throw 'FarLands reference did not confirm terrain and forest completion'}
$effective=[ordered]@{profile=$Profile;performanceRepresentative=$false;files=[ordered]@{}}
foreach($relative in @('BepInEx/config/ru.ivest.farlands.cfg','BepInEx/config/com.chase.valheimenhancededition.cfg','BepInEx/config/local.valheim.horizonclarity.cfg','BepInEx/plugins/FarLands/FarLands.dll')){
 $path=Join-Path $client $relative
 if(Test-Path $path){$effective.files[$relative]=(Get-FileHash $path).Hash.ToLowerInvariant()}
}
$effective | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $run 'effective-profile.json') -Encoding UTF8
Get-Content (Join-Path $run 'PASS.json')

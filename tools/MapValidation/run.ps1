param([Parameter(Mandatory=$true)][string]$GamePath, [switch]$AllowConcurrentClient, [switch]$ProfileFarLands, [switch]$TestProgressiveMap, [ValidateSet(512,2048)][int]$ProgressiveMapTestSize=2048, [switch]$TestQuickConnect, [switch]$TestWarband, [switch]$TestHorizon)
$ErrorActionPreference = 'Stop'
if (@(@($ProfileFarLands, $TestProgressiveMap, $TestQuickConnect) | Where-Object { $_ }).Count -gt 1) { throw 'Choose only one validation mode.' }
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$client = Join-Path $project 'dist/map-validation-client'
$run = Join-Path $project ('dist/map-validation/' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
if ((Get-Process valheim -ErrorAction SilentlyContinue) -and -not $AllowConcurrentClient) { throw 'Close the normal Valheim client or use -AllowConcurrentClient for an isolated parallel run.' }
if (Get-CimInstance Win32_Process -Filter "Name='valheim.exe'" | Where-Object { $_.ExecutablePath -eq (Join-Path $client 'valheim.exe') }) { throw 'The isolated validation client is already running.' }
New-Item -ItemType Directory -Force $client, $run | Out-Null
foreach ($name in @('valheim.exe','UnityPlayer.dll','winhttp.dll','doorstop_config.ini','steam_appid.txt')) {
    Copy-Item -LiteralPath (Join-Path $GamePath $name) -Destination $client -Force
}
foreach ($name in @('valheim_Data','MonoBleedingEdge','doorstop_libs')) {
    $link = Join-Path $client $name
    $target = Join-Path $GamePath $name
    if (-not (Test-Path $link)) { New-Item -ItemType Junction -Path $link -Target $target | Out-Null }
    elseif ((Get-Item -LiteralPath $link).Target -ne $target) { throw "Unexpected test junction: $link" }
}
$core = Join-Path $client 'BepInEx/core'
$plugins = Join-Path $client 'BepInEx/plugins'
$config = Join-Path $client 'BepInEx/config'
New-Item -ItemType Directory -Force $core, $plugins, $config | Out-Null
Copy-Item -Path (Join-Path $GamePath 'BepInEx/core/*') -Destination $core -Force
$build = Join-Path $run 'build'
& (Join-Path $project 'mods/BiomeClarity/build.ps1') -GamePath $GamePath -OutputDirectory $build
Copy-Item -LiteralPath (Join-Path $build 'BiomeClarity.dll') -Destination $plugins -Force
Copy-Item -LiteralPath (Join-Path $GamePath 'BepInEx/plugins/LivingMap/LivingMap.dll') -Destination $plugins -Force
Copy-Item -LiteralPath (Join-Path $project 'config/j1ga.livingmap.cfg') -Destination (Join-Path $config 'j1ga.livingmap.cfg') -Force
$cfg = Get-Content -LiteralPath (Join-Path $config 'j1ga.livingmap.cfg') -Raw -Encoding UTF8
$cfg = $cfg.Replace('SaveOverlay = true','SaveOverlay = false').Replace('RespectFog = true','RespectFog = false').Replace('HighGroundOverlay = Both','HighGroundOverlay = Off')
Set-Content -LiteralPath (Join-Path $config 'j1ga.livingmap.cfg') -Value $cfg -Encoding UTF8
Set-Content -LiteralPath (Join-Path $config 'local.valheim.biomeclarity.cfg') -Value "[Display]`r`nEnabled = true`r`nBorders = true" -Encoding UTF8
$farlandsDll = Join-Path $plugins 'FarLands.dll'
if ($ProfileFarLands) {
    Copy-Item -LiteralPath (Join-Path $GamePath 'BepInEx/plugins/FarLands/FarLands.dll') -Destination $farlandsDll -Force
    Copy-Item -LiteralPath (Join-Path $GamePath 'BepInEx/config/ru.ivest.farlands.cfg') -Destination (Join-Path $config 'ru.ivest.farlands.cfg') -Force
} elseif (Test-Path $farlandsDll) { Remove-Item -LiteralPath $farlandsDll }
$progressiveDll = Join-Path $plugins 'ProgressiveMap.dll'
if ($TestProgressiveMap) {
    & (Join-Path $project 'mods/ProgressiveMap/build.ps1') -GamePath $GamePath
    Copy-Item -LiteralPath (Join-Path $project 'dist/ProgressiveMap.dll') -Destination $progressiveDll -Force
} elseif (Test-Path $progressiveDll) { Remove-Item -LiteralPath $progressiveDll }
$fixDll = Join-Path $plugins 'QuickConnectCrossplayFix.dll'
if ($TestQuickConnect) {
    & (Join-Path $project 'mods/QuickConnectCrossplayFix/build.ps1') -GamePath $GamePath
    Copy-Item -LiteralPath (Join-Path $project 'dist/QuickConnectCrossplayFix.dll') -Destination $fixDll -Force
} elseif (Test-Path $fixDll) { Remove-Item -LiteralPath $fixDll }
$quickDll = Join-Path $plugins 'MagiCorp.RememberServerPassword.dll'
if ($TestQuickConnect) {
    Copy-Item -LiteralPath (Join-Path $GamePath 'BepInEx/plugins/RememberServerPassword/MagiCorp.RememberServerPassword.dll') -Destination $quickDll -Force
} elseif (Test-Path $quickDll) { Remove-Item -LiteralPath $quickDll }
$warbandDll = Join-Path $plugins 'Warband.dll'
if ($TestWarband) {
    if (-not $TestQuickConnect) { throw 'Use -TestQuickConnect with -TestWarband for the menu probe.' }
    Copy-Item -LiteralPath (Join-Path $project 'dist/Warband.dll') -Destination $warbandDll -Force
} elseif (Test-Path $warbandDll) { Remove-Item -LiteralPath $warbandDll }
$horizonDll = Join-Path $plugins 'HorizonClarity.dll'
if ($TestHorizon) {
    if (-not $TestQuickConnect) { throw 'Use -TestQuickConnect with -TestHorizon for the menu probe.' }
    Copy-Item -LiteralPath (Join-Path $project 'dist/HorizonClarity.dll') -Destination $horizonDll -Force
    Copy-Item -LiteralPath (Join-Path $project 'config/local.valheim.horizonclarity.cfg') -Destination (Join-Path $config 'local.valheim.horizonclarity.cfg') -Force
} elseif (Test-Path $horizonDll) { Remove-Item -LiteralPath $horizonDll }
$managed = Join-Path $GamePath 'valheim_Data/Managed'
$refs = @('netstandard.dll','mscorlib.dll','System.dll','System.Core.dll','assembly_valheim.dll','assembly_utils.dll','Splatform.dll','com.rlabrecque.steamworks.net.dll','UnityEngine.dll','UnityEngine.CoreModule.dll','UnityEngine.ImageConversionModule.dll') | ForEach-Object { Join-Path $managed $_ }
$refs += Join-Path $core 'BepInEx.dll'
$refs += Join-Path $core '0Harmony.dll'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$argsList = @('/nologo','/noconfig','/nostdlib+','/target:library','/optimize+',('/out:' + (Join-Path $plugins 'MapValidation.dll')))
$argsList += $refs | ForEach-Object { '/reference:' + $_ }
& $compiler @argsList (Join-Path $PSScriptRoot 'MapValidation.cs') (Join-Path $PSScriptRoot 'FarLandsProfile.cs') (Join-Path $PSScriptRoot 'ProgressiveMapChecks.cs') (Join-Path $PSScriptRoot 'QuickConnectChecks.cs')
if ($LASTEXITCODE -ne 0) { throw 'Validation plugin compilation failed' }
$savedir = Join-Path $run 'saves'
New-Item -ItemType Directory -Path $savedir | Out-Null
$oldQuick = $env:VALHEIM_QUICK_CONNECT_TEST
$env:VALHEIM_QUICK_CONNECT_TEST = if ($TestQuickConnect) { '1' } else { '0' }
$oldMapSize = $env:VALHEIM_PROGRESSIVE_MAP_SIZE
$env:VALHEIM_PROGRESSIVE_MAP_SIZE = $ProgressiveMapTestSize.ToString()
$oldProgressive = $env:VALHEIM_PROGRESSIVE_MAP_TEST
$env:VALHEIM_PROGRESSIVE_MAP_TEST = if ($TestProgressiveMap) { '1' } else { '0' }
$oldProfile = $env:VALHEIM_FARLANDS_PROFILE
$env:VALHEIM_FARLANDS_PROFILE = if ($ProfileFarLands) { '1' } else { '0' }
$oldOutput = $env:VALHEIM_MAP_VALIDATION_OUTPUT
$env:VALHEIM_MAP_VALIDATION_OUTPUT = $run
. (Join-Path $PSScriptRoot 'DisplayPreferences.ps1')
$displayBefore = Get-MapDisplayPreferences
$displayBefore | Export-Clixml -LiteralPath (Join-Path $run 'display-preferences-before.xml')
try {
    $process = Start-Process -FilePath (Join-Path $client 'valheim.exe') -WorkingDirectory $client -WindowStyle Hidden -PassThru -ArgumentList @('-savedir',('"' + $savedir + '"'),'-screen-fullscreen','0','-screen-width','512','-screen-height','512','-logFile',('"' + (Join-Path $run 'Unity.log') + '"'))
} catch {
    Restore-MapDisplayPreferences -Snapshot $displayBefore
    throw
} finally { $env:VALHEIM_MAP_VALIDATION_OUTPUT = $oldOutput; $env:VALHEIM_FARLANDS_PROFILE = $oldProfile; $env:VALHEIM_PROGRESSIVE_MAP_TEST = $oldProgressive; $env:VALHEIM_PROGRESSIVE_MAP_SIZE = $oldMapSize; $env:VALHEIM_QUICK_CONNECT_TEST = $oldQuick }
Write-Output "GPU validation PID $($process.Id); output: $run"
$limit = (Get-Date).AddSeconds(300)
try {
    while (-not $process.HasExited) {
        if ((Get-Date) -gt $limit) { throw 'GPU validation timed out' }
        Start-Sleep -Seconds 2
        $process.Refresh()
    }
} finally {
    # Only this runner's child process; never terminate a player's normal game.
    if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
    Restore-MapDisplayPreferences -Snapshot $displayBefore
    $displayAfter = Get-MapDisplayPreferences
    $displayAfter | Export-Clixml -LiteralPath (Join-Path $run 'display-preferences-after.xml')
    $beforeJson = $displayBefore | ConvertTo-Json -Depth 6 -Compress
    $afterJson = $displayAfter | ConvertTo-Json -Depth 6 -Compress
    if ($beforeJson -ne $afterJson) { throw 'Display preferences were not fully restored' }
    $log = Join-Path $client 'BepInEx/LogOutput.log'
    if (Test-Path $log) { Copy-Item -LiteralPath $log -Destination $run }
}
if (Test-Path (Join-Path $run 'FAILED.txt')) { throw (Get-Content (Join-Path $run 'FAILED.txt') -Raw) }
if ($process.ExitCode -ne 0) { throw "Validation process exited with code $($process.ExitCode). See $run" }
if (-not (Test-Path (Join-Path $run 'PASS.json'))) { throw "No validation report. See $run" }
if ($TestWarband -and -not (Test-Path (Join-Path $run 'Warband-PASS.json'))) { throw "No Warband validation report. See $run" }
$inputs = [ordered]@{ checkedAt=(Get-Date -Format o); fixture=$(if ($TestQuickConnect) { 'quick-connect-menu-smoke' } elseif ($TestProgressiveMap) { 'progressive-map-equivalence' } elseif ($ProfileFarLands) { 'local-world-farlands-profile' } else { 'synthetic' }); serverConnection=$false; files=[ordered]@{} }
foreach ($path in @((Join-Path $PSScriptRoot 'MapValidation.cs'), (Join-Path $PSScriptRoot 'FarLandsProfile.cs'), (Join-Path $PSScriptRoot 'ProgressiveMapChecks.cs'), (Join-Path $PSScriptRoot 'QuickConnectChecks.cs'), (Join-Path $PSScriptRoot 'DisplayPreferences.ps1'), (Join-Path $PSScriptRoot 'run.ps1'), (Join-Path $project 'mods/BiomeClarity/Plugin.cs'), (Join-Path $project 'mods/BiomeClarity/Palette.cs'), (Join-Path $build 'BiomeClarity.dll'), (Join-Path $plugins 'LivingMap.dll'))) {
    $inputs.files[$path.Substring($project.Length+1).Replace('\','/')] = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
}
if ($ProfileFarLands) { $inputs.files['installed-profile-copy/FarLands.dll'] = (Get-FileHash -LiteralPath $farlandsDll -Algorithm SHA256).Hash.ToLowerInvariant() }
if ($TestProgressiveMap) { $inputs.files['test-plugin/ProgressiveMap.dll'] = (Get-FileHash -LiteralPath $progressiveDll -Algorithm SHA256).Hash.ToLowerInvariant() }
if ($TestQuickConnect) { $inputs.files['test-plugin/QuickConnectCrossplayFix.dll'] = (Get-FileHash -LiteralPath $fixDll -Algorithm SHA256).Hash.ToLowerInvariant(); $inputs.files['test-plugin/RememberServerPassword.dll'] = (Get-FileHash -LiteralPath $quickDll -Algorithm SHA256).Hash.ToLowerInvariant() }
if ($TestWarband) { $inputs.files['test-plugin/Warband.dll'] = (Get-FileHash $warbandDll -Algorithm SHA256).Hash.ToLowerInvariant() }
if ($TestHorizon) { if (-not (Test-Path (Join-Path $run 'Horizon-PASS.json'))) { throw 'Horizon report missing' }; $inputs.files['test-plugin/HorizonClarity.dll'] = (Get-FileHash $horizonDll).Hash.ToLowerInvariant() }
$inputs | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $run 'inputs.json') -Encoding UTF8
Get-Content -LiteralPath (Join-Path $run 'PASS.json')
Write-Output "Render artifacts: $run"

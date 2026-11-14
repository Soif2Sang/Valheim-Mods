param([Parameter(Mandatory=$true)][string]$GamePath, [string]$ModPath)
$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $GamePath 'BepInEx/core/Mono.Cecil.dll')
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
if (-not $ModPath) { $ModPath = Join-Path $project 'dist/BiomeClarity.dll' }
$mod=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($ModPath)
$living=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $project 'upstream/LivingMap/build/LivingMap.dll'))
try {
 $t=$living.MainModule.Types | Where-Object FullName -eq 'LivingMap.LivingMapPlugin'
 $field=$t.Fields | Where-Object Name -eq '_vanillaTex'
 if($field.FieldType.FullName -ne 'UnityEngine.Texture2D'){throw 'Unsupported base field'}
 $rebuild=$t.Methods | Where-Object Name -eq 'RebuildGpuLayer'
 $blit=$rebuild.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.FullName -eq 'UnityEngine.Graphics' -and $_.Operand.Name -eq 'Blit' } | Select-Object -First 1
 if(-not $blit -or $blit.Previous.Previous.Previous.Operand.Name -ne '_vanillaTex'){throw 'Base texture is no longer the Blit source'}
 $plugin=$mod.MainModule.Types | Where-Object FullName -eq 'BiomeClarity.Plugin'
 foreach($name in @('BeforeLivingMapRebuild','AfterLivingMapRebuild')){
  $m=$plugin.Methods | Where-Object Name -eq $name
  if(-not ($m.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'SetValue' })){throw "Missing texture assignment: $name"}
 }
 $writes=$plugin.Methods | ForEach-Object {if($_.HasBody){$_.Body.Instructions}} | Where-Object { $_.OpCode.Name -eq 'stfld' -and $_.Operand.Name -eq 'm_mapTexture' }
 if($writes){throw 'Native map texture must remain unchanged'}
 Write-Output 'PASS: shipped LivingMap bridge target, Blit source, substitution/restoration, native map field preservation.'
} finally {$mod.Dispose();$living.Dispose()}


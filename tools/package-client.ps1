param(
 [Parameter(Mandatory=$true)][string]$BaseZip,
 [Parameter(Mandatory=$true)][string]$OutputDirectory,
 [string]$ReleaseName=('Valheim-Client-Complet-'+(Get-Date -Format 'yyyy-MM-dd-HHmmss'))
)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output=[IO.Path]::GetFullPath($OutputDirectory)
if($ReleaseName -notmatch '^[A-Za-z0-9._-]+$'){throw 'Invalid release name'}
$stage=Join-Path $root ('dist/releases/'+$ReleaseName)
$zip=Join-Path $output ($ReleaseName+'.zip')
if((Test-Path $stage) -or (Test-Path $zip)){throw 'Release already exists'}
New-Item -ItemType Directory -Force $stage,$output | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::ExtractToDirectory([IO.Path]::GetFullPath($BaseZip),$stage)
foreach($name in @('BiomeClarity','ProgressiveMap','QuickConnectCrossplayFix','CraftFromContainers')){
 $target=Join-Path $stage "BepInEx/plugins/$name/$name.dll"
 if(!(Test-Path $target)){throw "Base pack missing $name"}
 Copy-Item -LiteralPath (Join-Path $root "dist/$name.dll") -Destination $target -Force
}
foreach($file in Get-ChildItem (Join-Path $root 'config') -File){Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $stage 'BepInEx/config') -Force}
$personal=Get-ChildItem $stage -Recurse -File | Where-Object {$_.Extension -in @('.dat','.key')}
if($personal){throw 'Personal data/key files must not be packaged'}
$config=Get-Content (Join-Path $stage 'BepInEx/config/magicorp.valheim.rememberserverpassword.cfg') -Raw
if($config -notmatch '(?m)^LastServer =\s*$'){throw 'Generic LastServer must be empty'}
Copy-Item (Join-Path $root 'docs/VALIDATION.md') (Join-Path $stage 'Mod-Documentation/VALIDATION-MONOREPO.md')
Copy-Item (Join-Path $root 'upstream/CraftFromContainers/FIXES.md') (Join-Path $stage 'Mod-Documentation/CraftFromContainersFix.md') -Force
$commit=git -C $root rev-parse HEAD
if($LASTEXITCODE -ne 0){throw 'Cannot determine source commit'}
@"

RELEASE MONOREPO — $ReleaseName
Sources : https://github.com/Soif2Sang/Valheim-Mods/commit/$commit
Fermer Valheim, extraire dans le dossier du jeu et remplacer les fichiers aux memes emplacements, sans doublon de DLL. Relancer seulement le client.
Inclut Biome Clarity 0.3.4, Progressive Map 0.1.1, Quick Connect Crossplay Fix 0.1.0 et Craft From Containers 4.1.0 correctif local 1 compiles depuis le monorepo.
Craft From Containers est desormais compile depuis les sources C# corrigees, au lieu de modifier la DLL amont avec un script IL. Version affichee toujours 4.1.0.
Connexion rapide : effectuer une premiere connexion normale avec son personnage et le mot de passe, puis utiliser Quick Connect. Aucun identifiant personnel inclus.
Carte progressive : repartit la generation pour limiter les blocages ; la carte complete peut arriver plus tard. LivingMap conserve ses mises a jour grossieres, sans surimpression sur l'eau.
Validation : compilation et tests de palette/identite passes. Les tests Unity historiques du monorepo sont decrits dans Mod-Documentation/VALIDATION-MONOREPO.md ; cette archive complete n'a pas ete testee en connexion multijoueur.
"@ | Add-Content (Join-Path $stage 'INSTALLATION.txt') -Encoding UTF8
$inventoryPath=Join-Path $stage 'MODS.json'
$inventory=Get-Content $inventoryPath -Raw -Encoding UTF8 | ConvertFrom-Json
$inventory | Add-Member -Force NoteProperty release ([ordered]@{name=$ReleaseName;source='https://github.com/Soif2Sang/Valheim-Mods';commit=$commit;builtFromSource=$true;fullPackMultiplayerTested=$false})
$inventory.craftFromContainersFix.status='C# source build from monorepo; compilation passed; real multiplayer refill untested'
$hashes=[ordered]@{}
foreach($file in Get-ChildItem $stage -Recurse -File | Sort-Object FullName){
 if($file.FullName -eq $inventoryPath){continue}
 $relative=$file.FullName.Substring($stage.Length+1).Replace('\','/')
 $hashes[$relative]=(Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
}
$inventory.files=$hashes
$inventory | ConvertTo-Json -Depth 20 | Set-Content $inventoryPath -Encoding UTF8
$writer=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
try {
 foreach($file in Get-ChildItem $stage -Recurse -File){
  $relative=$file.FullName.Substring($stage.Length+1).Replace('\','/')
  [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($writer,$file.FullName,$relative,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
 }
} finally {$writer.Dispose()}
$archive=[IO.Compression.ZipFile]::OpenRead($zip)
try {
 if($archive.Entries.Count -ne $hashes.Count+1){throw 'ZIP file count mismatch'}
 foreach($relative in $hashes.Keys){
  $entry=$archive.GetEntry($relative)
  if(!$entry){throw "Missing entry: $relative"}
  $stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create()
  try{$actual=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','').ToLowerInvariant()}finally{$stream.Dispose();$sha.Dispose()}
  if($actual -ne $hashes[$relative]){throw "ZIP content mismatch: $relative"}
 }
 Write-Output "Verified $($archive.Entries.Count) files including MODS.json."
} finally {$archive.Dispose()}
Get-FileHash $zip -Algorithm SHA256

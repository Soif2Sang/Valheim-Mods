param(
 [Parameter(Mandatory=$true)][string]$BaseZip,
 [Parameter(Mandatory=$true)][string]$OutputDirectory,
 [string]$ReleaseName=('Valheim-Client-Complet-'+(Get-Date -Format 'yyyy-MM-dd-HHmmss')),
 [switch]$WithoutFarLands,
 [switch]$HorizonClarity,
 [switch]$FarLandsLight
)
$ErrorActionPreference='Stop'
if($FarLandsLight -and ($WithoutFarLands -or $HorizonClarity)){throw 'FarLandsLight cannot be combined with WithoutFarLands or HorizonClarity'}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output=[IO.Path]::GetFullPath($OutputDirectory)
if($ReleaseName -notmatch '^[A-Za-z0-9._-]+$'){throw 'Invalid release name'}
$stage=Join-Path $root ('dist/releases/'+$ReleaseName)
$zip=Join-Path $output ($ReleaseName+'.zip')
if((Test-Path $stage) -or (Test-Path $zip)){throw 'Release already exists'}
New-Item -ItemType Directory -Force $stage,$output | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::ExtractToDirectory([IO.Path]::GetFullPath($BaseZip),$stage)
foreach($name in @('BiomeClarity','ProgressiveMap','QuickConnectCrossplayFix','CraftFromContainers','Warband')){
 $target=Join-Path $stage "BepInEx/plugins/$name/$name.dll"
 if(!(Test-Path $target)){throw "Base pack missing $name"}
 Copy-Item -LiteralPath (Join-Path $root "dist/$name.dll") -Destination $target -Force
}
foreach($file in Get-ChildItem (Join-Path $root 'config') -File){
 if($file.Name -eq 'local.valheim.horizonclarity.cfg' -and -not $HorizonClarity){continue}
 Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $stage 'BepInEx/config') -Force
}
if($HorizonClarity){
 $target=Join-Path $stage 'BepInEx/plugins/HorizonClarity'
 New-Item -ItemType Directory -Force $target | Out-Null
 Copy-Item (Join-Path $root 'dist/HorizonClarity.dll') $target -Force
 Copy-Item (Join-Path $root 'mods/HorizonClarity/README.md') (Join-Path $stage 'Mod-Documentation/HorizonClarity.md') -Force
}
if($WithoutFarLands){
 $farlands=Join-Path $stage 'BepInEx/plugins/FarLands/FarLands.dll'
 if(Test-Path $farlands){Rename-Item -LiteralPath $farlands -NewName 'FarLands.dll.disabled-performance-test'}
 Copy-Item (Join-Path $root 'profiles/without-farlands/com.chase.valheimenhancededition.cfg') (Join-Path $stage 'BepInEx/config/com.chase.valheimenhancededition.cfg') -Force
}
if($FarLandsLight){
 $farlands=Join-Path $stage 'BepInEx/plugins/FarLands/FarLands.dll'
 if(!(Test-Path $farlands)){
  $disabled=@(Get-ChildItem (Split-Path $farlands) -Filter 'FarLands.dll.disabled*' -File)
  if($disabled.Count -ne 1){throw 'Expected one FarLands binary in base pack'}
  Rename-Item -LiteralPath $disabled[0].FullName -NewName 'FarLands.dll'
 }
 if((Get-FileHash $farlands).Hash.ToLowerInvariant() -ne 'b81d9c266e2b8eafc7b137ed063c85e3caf0dd9054bb5dbacee86ad237fbacb2'){throw 'FarLandsLight was validated with FarLands 1.3.0 only'}
 Copy-Item (Join-Path $root 'profiles/farlands-light/ru.ivest.farlands.cfg') (Join-Path $stage 'BepInEx/config/ru.ivest.farlands.cfg') -Force
 Copy-Item (Join-Path $root 'profiles/without-farlands/com.chase.valheimenhancededition.cfg') (Join-Path $stage 'BepInEx/config/com.chase.valheimenhancededition.cfg') -Force
 Set-Content (Join-Path $stage 'BepInEx/config/local.valheim.horizonclarity.cfg') -Value "[Display]`nEnabled = false`nFogMultiplier = 0.25`nViewDistance = 4000" -Encoding UTF8
 Copy-Item (Join-Path $root 'profiles/farlands-light/README.md') (Join-Path $stage 'Mod-Documentation/FarLands-Light.md') -Force
}
$personal=Get-ChildItem $stage -Recurse -File | Where-Object {$_.Extension -in @('.dat','.key')}
if($personal){throw 'Personal data/key files must not be packaged'}
if(Test-Path (Join-Path $stage 'BepInEx/config/Warband')){throw 'Personal Warband friends/party files must not be packaged'}
$config=Get-Content (Join-Path $stage 'BepInEx/config/magicorp.valheim.rememberserverpassword.cfg') -Raw
if($config -notmatch '(?m)^LastServer =\s*$'){throw 'Generic LastServer must be empty'}
Copy-Item (Join-Path $root 'docs/VALIDATION.md') (Join-Path $stage 'Mod-Documentation/VALIDATION-MONOREPO.md')
Copy-Item (Join-Path $root 'upstream/CraftFromContainers/FIXES.md') (Join-Path $stage 'Mod-Documentation/CraftFromContainersFix.md') -Force
Copy-Item (Join-Path $root 'upstream/Warband/PERSISTENCE.md') (Join-Path $stage 'Mod-Documentation/Warband-Persistence.md') -Force
$commit=git -C $root rev-parse HEAD
if($LASTEXITCODE -ne 0){throw 'Cannot determine source commit'}
@"

RELEASE MONOREPO — $ReleaseName
Sources : https://github.com/Soif2Sang/Valheim-Mods/commit/$commit
Fermer Valheim, extraire dans le dossier du jeu et remplacer les fichiers aux memes emplacements, sans doublon de DLL. Relancer seulement le client.
Inclut Biome Clarity 0.3.4, Progressive Map 0.1.1, Quick Connect Crossplay Fix 0.1.0 et Craft From Containers 4.1.0 correctif local 1 compiles depuis le monorepo.
Craft From Containers est desormais compile depuis les sources C# corrigees, au lieu de modifier la DLL amont avec un script IL. Version affichee toujours 4.1.0.
Warband 1.3.1 (revision locale) : groupe memorise par monde et personnage, retour automatique des membres. Tous les membres doivent installer cette version et former le groupe normalement une premiere fois. Depart volontaire/exclusion effacent l'appartenance. Maj+P > Party > Leave saved party permet aussi de quitter quand on est seul connecte. Sauvegarde personnelle dans BepInEx/config/Warband/parties, jamais incluse dans les packs. Validation locale reussie ; reconnexion multijoueur reelle a confirmer.
Connexion rapide : effectuer une premiere connexion normale avec son personnage et le mot de passe, puis utiliser Quick Connect. Aucun identifiant personnel inclus.
Carte progressive : repartit la generation pour limiter les blocages ; la carte complete peut arriver plus tard. LivingMap conserve ses mises a jour grossieres, sans surimpression sur l'eau.
Validation : compilation et tests de palette/identite passes. Les tests Unity historiques du monorepo sont decrits dans Mod-Documentation/VALIDATION-MONOREPO.md ; cette archive complete n'a pas ete testee en connexion multijoueur.
"@ | Add-Content (Join-Path $stage 'INSTALLATION.txt') -Encoding UTF8
if($WithoutFarLands){
 @'

PROFIL SANS FARLANDS — prioritaire
FarLands desactive. VEE Far builds Distance=4000, Preset=Auto. Portee commune des objets lointains, pas un equivalent du terrain 4 km / forets 2,5 km de FarLands. Rendu et FPS a confirmer apres relance.
Jeu ferme, renommer ou retirer tout FarLands.dll deja present dans BepInEx/plugins/FarLands : extraire le fichier .disabled seul ne desactive pas une ancienne DLL active. Les autres mods et le chargeur restent actifs.
'@ | Add-Content (Join-Path $stage 'INSTALLATION.txt') -Encoding UTF8
}
$inventoryPath=Join-Path $stage 'MODS.json'
$inventory=Get-Content $inventoryPath -Raw -Encoding UTF8 | ConvertFrom-Json
$inventory | Add-Member -Force NoteProperty release ([ordered]@{name=$ReleaseName;source='https://github.com/Soif2Sang/Valheim-Mods';commit=$commit;builtFromSource=$true;fullPackMultiplayerTested=$false})
$inventory.craftFromContainersFix.status='C# source build from monorepo; compilation passed; real multiplayer refill untested'
if($HorizonClarity){
 $inventory | Add-Member -Force NoteProperty horizonClarity ([ordered]@{version='0.1.0';clientOnly=$true;fogMultiplier=0.25;minimumCameraDistance=4000;terrainGeneration=$false;status='Unity synthetic fog/camera/restore checks passed; real world rendering and FPS untested'})
 @'

HORIZON CLARITY 0.1.0 — essai local
Brume de distance a 25 % de la densite native et camera principale a au moins 4 km. FarLands reste desactive dans ce profil. Aucun nouveau terrain genere ; meteo/couleurs conservees, les brumes volumetriques ne sont pas desactivees. Le rendu de l'eau a distance peut varier avec la brume, sans modification de ses textures ou materiaux.
Relancer uniquement le client pour charger. Pour annuler, jeu ferme, retirer BepInEx/plugins/HorizonClarity/HorizonClarity.dll. Performances et rendu reel a confirmer.
'@ | Add-Content (Join-Path $stage 'INSTALLATION.txt') -Encoding UTF8
}
$inventory | Add-Member -Force NoteProperty warband ([ordered]@{version='1.3.1';upstreamVersion='1.3.0';localRevision=1;persistentParty=$true;clientOnly=$true;personalDataIncluded=$false;status='disk/policy tests and Unity packet/logout/forget probe passed; real multiplayer reconnect untested'})
if($WithoutFarLands){$inventory | Add-Member -Force NoteProperty distanceProfile ([ordered]@{farLandsEnabled=$false;veeFarBuildsDistance=4000;veePreset='Auto';separateTerrainForestDistances=$false;liveRenderingTested=$false})}
if($FarLandsLight){
 $inventory | Add-Member -Force NoteProperty distanceProfile ([ordered]@{name='FarLandsLight';farLandsEnabled=$true;farLandsVersion='1.3.0';terrainDistance=4000;forestDistance=2500;treeModelDistance=600;grassDistance=80;terrainShadows=$false;autoDetail=$false;impostorResolution=64;smoothImpostors=$false;veeFarBuildsDistance=4000;horizonClarityEnabled=$false;playerFPSGainVerified=$false})
 $inventory | Add-Member -Force NoteProperty localPerformanceTest ([ordered]@{profile='FarLandsLight';allModsDisabled=$false;farLandsDisabled=$false;playerFPSGainVerified=$false})
 if($inventory.PSObject.Properties['horizonClarity']){$inventory.horizonClarity | Add-Member -Force NoteProperty enabled $false}
 @'

PACK CLIENT COMPLET — FARLANDS ALLEGE
FarLands 1.3.0 actif : terrain 4000 m, foret 2500 m, modeles 3D jusqu'a 600 m puis imposteurs, herbe 80 m, atlas 64, interpolation et ombres lointaines coupees, detail automatique coupe. VEE conserve les batiments a 4 km. HorizonClarity est desactive pour laisser FarLands gerer l'air.
Fermer le jeu, extraire et remplacer les fichiers. Supprimer les doublons actifs de FarLands.dll ; le fichier BepInEx/plugins/FarLands/FarLands.dll doit etre unique. Ne pas renommer une ancienne copie .disabled en plus de cette DLL active.
Le banc hors ecran utilise une copie privee du monde ; il ne certifie pas un gain de FPS en partie. Les arbres lointains restent visibles mais sont plus plats ; l'herbe et les ombres sont moins detaillees. Aucun serveur a modifier.
Retour au profil precedent : retirer cette DLL active, restaurer la configuration FarLands sauvegardee et reactiver HorizonClarity si souhaite. Sauvegarder ses reglages personnels avant extraction.
'@ | Set-Content (Join-Path $stage 'INSTALLATION.txt') -Encoding UTF8
 @"

Sources : https://github.com/Soif2Sang/Valheim-Mods/commit/$commit
Connexion rapide : effectuer une premiere connexion normale pour memoriser la destination, puis Quick Connect. Aucun mot de passe ni personnage inclus.
Carte progressive et carte dynamique incluses. Warband 1.3.1 : groupe persistant par monde/personnage, tous les joueurs doivent installer la meme version et former une premiere fois le groupe. Maj+P > Party > Leave saved party pour quitter explicitement.
Craft From Containers 4.1.0 correctif local inclus. Voir Mod-Documentation pour les validations et limites. Ce pack complet n'a pas ete teste en connexion multijoueur.
"@ | Add-Content (Join-Path $stage 'INSTALLATION.txt') -Encoding UTF8
}
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

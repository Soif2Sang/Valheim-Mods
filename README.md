# Valheim Mods

Monorepo des mods client et correctifs de Soif2Sang. Les sources amont sont importées avec `git subtree` sans squash : leurs auteurs et historiques restent consultables. Nos ajouts et corrections ont leurs propres commits. Un clone normal contient toutes les sources, sans sous-modules.

| Dossier | Origine / état |
| --- | --- |
| `upstream/CraftFromContainers` | Extrait du dépôt aedenthorn/ValheimMods ; correctif Maj + E et actualisation des coffres |
| `upstream/LivingMap` | tbsj1ga/LivingMapValheim, tag v0.19.2 ; code inchangé, réglages dans config |
| `upstream/RememberServerPassword` | iMagic16/RememberServerPassword, source publique v1.1.1 ; code inchangé |
| `mods/BiomeClarity` | 0.3.4 : palette, contour sombre et ponts LivingMap |
| `mods/ProgressiveMap` | 0.1.1 : génération de carte par tranches, affichage différé |
| `mods/QuickConnectCrossplayFix` | 0.1.0 : destination PlayFab remise en majuscules |

Remember Server Password : le paquet utilisé localement est en **1.2.4**, tandis que le dépôt public importé annonce **1.1.1**. Ce monorepo ne prétend pas que ces sources reproduisent la DLL 1.2.4. Le correctif de compatibilité reste un plugin indépendant, validé avec le paquet 1.2.4.

FarLands 1.3.0 n'a pas été modifié et ses sources n'ont pas été trouvées. Le réglage utilisé est terrain 4000 m, arbres 2500 m, bâtiments désactivés (déjà gérés par VEE). Progressive Map répartit le travail de la carte pour réduire les blocages ; il ne promet pas un temps total de connexion plus court.

## Compilation

Windows, Valheim installé avec BepInEx. Les assemblies du jeu sont des dépendances locales, jamais incluses ici.

```powershell
./build.ps1 -GamePath 'CHEMIN_DU_JEU' -CompilerPath 'CHEMIN_ROSLYN/csc.exe'
```

Le script compile nos trois plugins et Craft From Containers ; il exécute aussi les contrôles de palette et d'identité PlayFab. Roslyn 4.8 a été utilisé pour Craft From Containers avec Valheim 1.0.21 / BepInEx 5.4.2360. Pour LivingMap et Remember Server Password, conserver leurs instructions amont et leurs licences ; le script racine ne les compile pas.

## Validation et installation

Voir [VALIDATION.md](docs/VALIDATION.md). Ce dépôt contient les sources et configurations génériques. Il n'est pas un pack à extraire dans le jeu : copier seulement les DLL souhaitées de dist vers les dossiers de plugins correspondants, client fermé. Ne pas installer une seconde copie du même plugin. Aucune commande de serveur n'est nécessaire.

Les licences amont restent applicables dans leurs sous-dossiers. La déclaration de domaine public d'aedenthorn est conservée dans docs/CraftFromContainers-LICENSE.txt. Nos sources sous mods sont sous licence MIT (LICENSE). Aucun mot de passe, clé, sauvegarde, log personnel ou assembly du jeu n'est distribué.

# Valheim Mods

Monorepo des mods client et correctifs de Soif2Sang. Les sources amont sont importées avec `git subtree` sans squash : leurs auteurs et historiques restent consultables. Nos ajouts et corrections ont leurs propres commits. Un clone normal contient toutes les sources, sans sous-modules.

| Dossier | Origine / état |
| --- | --- |
| `upstream/CraftFromContainers` | Extrait du dépôt aedenthorn/ValheimMods ; correctif Maj + E et actualisation des coffres |
| `upstream/LivingMap` | tbsj1ga/LivingMapValheim, tag v0.19.2 ; code inchangé, réglages dans config |
| `upstream/RememberServerPassword` | iMagic16/RememberServerPassword, source publique v1.1.1 ; code inchangé |
| `upstream/Warband` | Felihens/Warband v1.3.0, modifié en 1.3.1 local : groupes persistants par monde/personnage |
| `mods/BiomeClarity` | 0.3.4 : palette, contour sombre et ponts LivingMap |
| `mods/ProgressiveMap` | 0.1.1 : génération de carte par tranches, affichage différé |
| `mods/QuickConnectCrossplayFix` | 0.1.0 : destination PlayFab remise en majuscules |
| `mods/HorizonClarity` | 0.1.0 expérimental : brume native ×0,25, caméra principale au moins 4 km ; sans génération de terrain |

Remember Server Password : le paquet utilisé localement est en **1.2.4**, tandis que le dépôt public importé annonce **1.1.1**. Ce monorepo ne prétend pas que ces sources reproduisent la DLL 1.2.4. Le correctif de compatibilité reste un plugin indépendant, validé avec le paquet 1.2.4.

FarLands 1.3.0 n'a pas été modifié et ses sources n'ont pas été trouvées. Le réglage utilisé est terrain 4000 m, arbres 2500 m, bâtiments désactivés (déjà gérés par VEE). Progressive Map répartit le travail de la carte pour réduire les blocages ; il ne promet pas un temps total de connexion plus court.

## Compilation

Windows, Valheim installé avec BepInEx. Les assemblies du jeu sont des dépendances locales, jamais incluses ici.

```powershell
./build.ps1 -GamePath 'CHEMIN_DU_JEU' -CompilerPath 'CHEMIN_ROSLYN/csc.exe'
```

Le script compile nos quatre plugins, Craft From Containers et Warband ; il exécute aussi les contrôles de palette, d'identité PlayFab et de persistance Warband. Roslyn 4.8 a été utilisé pour Craft From Containers avec Valheim 1.0.21 / BepInEx 5.4.2360. Pour LivingMap et Remember Server Password, conserver leurs instructions amont et leurs licences ; le script racine ne les compile pas.

## Validation et installation

Pour produire un **pack client complet** depuis un ZIP complet de référence déjà approuvé, compiler d'abord les six plugins avec build.ps1, puis lancer :

```powershell
./tools/package-client.ps1 -BaseZip 'PACK_COMPLET_PRECEDENT.zip' -OutputDirectory 'DOSSIER_TELECHARGEMENTS'
```

Le pack conserve les autres mods du ZIP de référence, remplace les DLL recompilées par les builds du monorepo, copie les configurations génériques et actualise INSTALLATION.txt / MODS.json avec le commit source et les SHA-256. Chaque contenu du ZIP est vérifié. Les fichiers personnels .dat/.key sont refusés et LastServer doit être vide. Aucune installation ni connexion au serveur n'est effectuée.

Ce monorepo est désormais la référence pour **toutes les modifications, compilations et validations**. Les anciennes copies du projet de déploiement sont historiques et ne doivent plus servir à développer ou tester les mods.

Depuis la racine de ce dépôt :

```powershell
./tools/MapValidation/run.ps1 -GamePath 'CHEMIN_DU_JEU' -TestQuickConnect -AllowConcurrentClient
./tools/MapValidation/run.ps1 -GamePath 'CHEMIN_DU_JEU' -TestProgressiveMap -ProgressiveMapTestSize 512 -AllowConcurrentClient
```

Sans mode spécial, le banc vérifie le rendu synthétique des biomes et du pont LivingMap. `-ProfileFarLands` réalise le profil isolé. Un seul mode spécial par exécution. Le jeu normal n'est pas arrêté ; le banc ne rejoint aucun serveur. Les résultats, empreintes et sauvegardes fictives restent sous `dist/map-validation/`, le client isolé sous `dist/map-validation-client/`. Les préférences d'affichage sont restaurées après le test.

Les DLL LivingMap et RememberServerPassword proviennent de leurs dossiers de plugins dans GamePath ; elles ne sont pas publiées ici. Installer les versions documentées avant les tests. Le banc compile nos plugins depuis les sources de ce dépôt. Il ne récupère pas de code depuis les anciens dossiers du projet de déploiement.

Voir [VALIDATION.md](docs/VALIDATION.md). Ce dépôt contient les sources et configurations génériques. Il n'est pas un pack à extraire dans le jeu : copier seulement les DLL souhaitées de dist vers les dossiers de plugins correspondants, client fermé. Ne pas installer une seconde copie du même plugin. Aucune commande de serveur n'est nécessaire.

Les licences amont restent applicables dans leurs sous-dossiers. La déclaration de domaine public d'aedenthorn est conservée dans docs/CraftFromContainers-LICENSE.txt. Nos sources sous mods sont sous licence MIT (LICENSE). Aucun mot de passe, clé, sauvegarde, log personnel ou assembly du jeu n'est distribué.

## Groupes persistants — Warband 1.3.1 local

Profil optionnel sans FarLands : ajouter `-WithoutFarLands` au packaging pour appliquer `profiles/without-farlands` (VEE portée commune 4 km, FarLands désactivé). Cette option ne reproduit pas les distances séparées terrain/forêt de FarLands. Rendu et FPS à vérifier en jeu ; aucun serveur modifié.

Tous les membres doivent installer la nouvelle version, puis former le groupe une première fois. Les reconnexions au même monde/personnage réutilisent le groupe mémorisé. Quitter ou être exclu efface cette appartenance ; Maj+P > Party > Leave saved party permet aussi de quitter seul. Voir upstream/Warband/PERSISTENCE.md pour le stockage local et les limites de synchronisation hors ligne.

Contrôle Unity du plugin : tools/MapValidation/run.ps1 -GamePath CHEMIN_DU_JEU -TestQuickConnect -TestWarband -AllowConcurrentClient. Sonde synthétique, aucune reconnexion multijoueur réelle.

## Essai de visibilité sans FarLands

HorizonClarity est un essai client optionnel : brume native multipliée par 0,25 et portée de caméra minimale 4 km. Ajouter -HorizonClarity -WithoutFarLands au packaging pour ce profil. Contrôle isolé : tools/MapValidation/run.ps1 -GamePath CHEMIN_DU_JEU -TestQuickConnect -TestHorizon -AllowConcurrentClient. Tests de propriétés Unity réussis ; rendu réel et FPS non mesurés. Voir mods/HorizonClarity/README.md pour les limites et le retour arrière.

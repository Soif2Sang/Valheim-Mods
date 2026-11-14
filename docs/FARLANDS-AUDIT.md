# Audit performances FarLands 1.3.0 — 14 novembre 2026

FarLands prolonge le terrain et la végétation, dessine une partie des arbres avec leurs modèles 3D, ajoute des imposteurs, de l'herbe, des ombres et une atmosphère. Charge étudiée : un client Valheim 1.0.21 avec FarLands 1.3.0 et VEE 1.1.1, terrain 4000 m / forêt 2500 m, bâtiments FarLands désactivés.

## Périmètre et niveau de preuve

Audit statique de la DLL réellement utilisée : SHA-256 b81d9c266e2b8eafc7b137ed063c85e3caf0dd9054bb5dbacee86ad237fbacb2. Lecture C# reconstruite par ILSpy 7.2.1.6856 avec les dépendances locales du jeu. Ce n'est pas un dépôt source officiel ni une reproduction compilée du mod. Aucun dépôt source officiel identifié dans le paquet 1.3.0 ou les liens de publication consultés ; le paquet contient DLL, README, changelog et API, sans projet C#.

La DLL VEE du profil de référence a aussi été examinée pour vérifier la coordination entre les deux mods. Code reconstruit, bibliothèques de décompilation, configurations et logs restent privés dans dist. Aucun jeu ni serveur lancé ou arrêté pour cet audit. Les essais précédents en fenêtre masquée servent de contexte de chargement, pas de preuve de coûts GPU/FPS.

**Conclusion : des mécanismes coûteux et une piste d'optimisation de code sont identifiés ; aucune mesure ne permet encore d'attribuer la perte de FPS du joueur à un poste précis.**

## À traiter en priorité avant une optimisation de code

1. **Des modèles 3D sont soumis par grands secteurs, jusqu'à 1200 m.**
   - Fonction : FarForest.Tick appelle MeshDrawer.Draw pour les arbres et les rochers. La configuration de référence a TreeModelDistance=1200.
   - Constat : MeshDrawer.Build range les instances dans huit secteurs par partie de mesh. Draw teste une boîte englobante par secteur, puis envoie tous ses lots, jusqu'à 1023 instances par appel. Une forêt partiellement dans le champ peut donc entraîner la soumission d'arbres hors champ dans le même secteur. Le GPU découpe ensuite ces objets ; leur soumission et traitement géométrique ne sont pas gratuits. Le chemin des imposteurs a, lui, un filtre GPU distinct : ne pas généraliser cette observation à tout le mod.
   - Plus petit essai : réduire seulement TreeModelDistance (par exemple 600 m), en gardant ForestDistance=2500 et TerrainDistance=4000. Les arbres restent visibles au loin sous forme d'imposteurs. L'essai extrême à 0 est utile pour isoler ce poste, avec une perte de relief des arbres.
   - Piste code : regrouper spatialement les lots 3D plus finement et tester leurs bornes avant soumission, en réutilisant ces groupes jusqu'au prochain changement. Cela peut augmenter les appels de dessin : gain à prouver, pas un correctif certain. Vérifier les rotations rapides de caméra et les transitions.
   - Sans action : le coût potentiel de ces modèles reste présent, même après lecture du cache.
   - Repères dans la reconstruction locale : FarForest.Tick vers 13749 ; MeshDrawer.Draw vers 20491 ; MeshDrawer.Build vers 20602 ; Sectors=8 vers 20287.

2. **FarLands augmente aussi l'herbe, indépendamment de l'horizon.**
   - Fonction : GrassRadius.Bind porte ClutterSystem.m_distance à GrassDistance ; la référence utilise 200 m. GpuGrass gère construction, buffers et dessins indirects.
   - Constat : l'herbe proche est un poste supplémentaire qui n'est pas nécessaire pour conserver la côte à 4 km ou la forêt à 2,5 km. Le chemin GPU sélectionne les instances et les dessine par espèce ; présence d'un rendu instancié ne signifie pas coût nul.
   - Plus petit essai : réduire seulement GrassDistance à 60–80 m, sans toucher aux deux distances lointaines. Garder le même niveau d'herbe natif entre profils.
   - Bug identifié dans 1.3.0 : le fournisseur GpuGrass est activé selon General.Enabled, même lorsque GrassDistance=0 ; Bind restaure le rayon, sans couper ce fournisseur. Il ne faut donc pas utiliser 0 comme preuve que le chemin GPU du mod est supprimé dans cette version. Le changelog officiel annonce la correction en 1.4.0.
   - Autre cas corrigé en 1.4.0 selon l'auteur : deux mods réécrivant le rayon pouvaient reconstruire l'herbe chaque seconde. Dans 1.3.0, RefreshSettings appelle Bind chaque seconde et Bind demande ResetGrass lorsque le rayon diffère. Aucun conflit de ce type n'a été démontré avec le pack actuel ; ne pas l'annoncer comme cause chez le joueur.
   - Sans action : ce poste demeure ou un test à 0 donne une conclusion trompeuse.
   - Repères : GrassRadius.Bind vers 18626 ; FarTerrain.RefreshSettings vers 16936 ; FarLandsPlugin.Awake vers 25635 ; GpuGrass.EndPass vers 2195.

3. **Le niveau de détail peut augmenter automatiquement pendant le jeu.**
   - Fonction : DetailGovernor observe la part GPU du temps de trame ; FarTerrain.Detail utilise son résultat lorsque AutoDetail=true, comme dans la référence.
   - Constat : après deux fenêtres de dix secondes avec de la marge, le terrain devient plus détaillé. Il attend également deux fenêtres chargées pour revenir. Deux captures prises à des moments différents peuvent donc avoir une géométrie différente avec le même réglage affiché de base. Ce mécanisme est volontaire, pas un bug prouvé.
   - Plus petit essai : AutoDetail=false dans les seuls profils de mesure. Cela fixe le détail sans réduire TerrainDistance ou ForestDistance.
   - Sans action : on peut attribuer à tort au changement testé une variation due à l'ajustement automatique.
   - Repères : DetailGovernor.Frame/Detail vers 4272 ; FarTerrain.Detail vers 16536.

## Autres postes à isoler

4. **Ombres, atmosphère et atlas ajoutent du travail ou de la mémoire.**
   - Fonction : TerrainShadows=true active FarShadows et ForestShade ; AirPass ajoute une passe écran et peut en ajouter aux caméras de réflexion ; ImpostorResolution=128 crée les atlas des arbres. SmoothImpostors=true est également actif.
   - Constat : le cache de terrain ne supprime pas ces passes de rendu. Les coûts dépendent de la scène, de la résolution, de la carte graphique et des shaders. Le C# permet de tracer les appels mais ne donne pas le temps GPU des shaders embarqués.
   - Plus petit essai : tester séparément TerrainShadows=false et ImpostorResolution=64, puis SmoothImpostors=false si nécessaire. Conserver l'air et ses réflexions dans un premier temps pour respecter le souhait de préserver l'apparence de l'eau. Réduire les atlas vise d'abord la mémoire, sans promesse de FPS.
   - Sans action : on mélange plusieurs coûts graphiques sous le nom de « longue distance ».
   - Repères : FarTerrain.TickShadows vers 16558 ; FarShadows vers 15601 ; AirPass vers 10331 ; ImpostorBaker vers 18708.

## Vérifications qui évitent de faux diagnostics

- **VEE et FarLands ne dessinent pas naïvement deux fois toute la forêt.** VEE lit FarLandsLink.ForestRange et utilise OthersVegetation dans FarVisibility et SuperVisibility pour masquer sa végétation dans la portée prise en charge par l'autre mod. Des coûts de préparation et des transitions restent possibles. Rien ne justifie d'accuser un doublon complet sans capture/profil GPU. Repères VEE : OthersVegetation vers 5401, FarVisibility vers 6007, SuperVisibility vers 8200.
- **Les bâtiments FarLands sont déjà désactivés.** FarLandsPlugin.TickBuildings quitte via StopBuildings quand BuildingsOn=false. La correction 1.4.1 sur les lumières lointaines ne suffit donc pas à expliquer ce profil.
- **La génération est déjà asynchrone et mise en cache.** TileBuilder et ForestGenerator utilisent travail en arrière-plan et parallélisme ; les anciens contrôles de cache montraient bien sa réutilisation. Optimiser ce chargement ne prouve pas une amélioration des FPS une fois immobile.
- **Les anciens temps de trame masqués restent exclus du diagnostic FPS.** Le banc copie le monde en hôte local, ce qui donne aussi à VEE accès aux objets de tout le monde sauvegardé. Ce n'est pas exactement le travail d'un client sur serveur distant.

## Versions plus récentes et sources consultées

La page officielle annonce 1.4.1 lors de la consultation. Son changelog décrit les corrections d'herbe en 1.4.0 et des améliorations sur les arbres/rochers en 1.4.1. Ce sont des déclarations de l'auteur, pas des mesures sur cette machine. La version installée n'a pas été remplacée et aucune compatibilité complète du pack avec 1.4.1 n'a été testée.

- [Publication officielle FarLands](https://thunderstore.io/c/valheim/p/Ivvty/FarLands/)
- [Changelog officiel](https://thunderstore.io/c/valheim/p/Ivvty/FarLands/changelog)
- [Version auditée 1.3.0](https://thunderstore.io/c/valheim/p/Ivvty/FarLands/v/1.3.0/)

## Suite utile

Conserver la référence 1.3.0, puis mesurer chaque changement seul sur le même snapshot, scène, météo, résolution et paramètres natifs, après stabilisation du streaming. Relever CPU principal, GPU, mémoire, nombre de modèles/triangles soumis et saccades, en premier plan sans client concurrent ; répéter les essais. Une version 1.4.1 inchangée peut être une référence supplémentaire avant de porter un correctif de code. Ne pas changer les distances 4000/2500 pour ces essais. Le présent audit n'a exécuté aucun de ces nouveaux profils.

Verdict : commencer par mesurer le rayon des modèles 3D et l'herbe, puis les passes graphiques ; la granularité du rejet des modèles 3D est la piste de code la plus concrète relevée. Aucun gain chiffré ni correctif de DLL validé.

Non vérifiés : sources originales, code source des shaders embarqués, temps GPU en jeu au premier plan, profil détaillé de la session du joueur, exécution de 1.4.1 et nouveaux réglages. Aucun mod, configuration du jeu installé, pack client ou serveur modifié.
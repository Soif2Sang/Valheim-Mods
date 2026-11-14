# Validation

Les validations historiques ont été effectuées dans une instance Unity isolée avec Valheim 1.0.21. Les logs, sauvegardes de test et assemblies du jeu ne sont pas publiés.

- Biome Clarity : tests de palette et contrôles du pont LivingMap ; rendu GPU synthétique avec eau/prairies inchangées, forêt recolorée et contour sombre visible. Aucun contrôle exhaustif du monde réel.
- Progressive Map : génération de 2048² pixels comparée aux textures natives, empreintes identiques ; calcul réparti en tranches ciblant 4 ms. Version 0.1.1 : visibilité différée testée en 512². Un résultat natif identique ne prouve pas une connexion plus rapide ; la publication finale reste synchrone.
- Quick Connect : constructeur officiel testé avec une identité fictive, stockage inchangé, destination PlayFab en majuscules ; utilisateur ayant confirmé la connexion réelle avec Remember Server Password 1.2.4. Aucun identifiant réel publié.
- Craft From Containers : compilation des sources et chargement Harmony d'un patch binaire équivalent validés. Remplissage réel multijoueur avec le build source non testé ; les courses réseau des transferts ne sont pas annoncées comme corrigées. Scénarios de régression dans upstream/CraftFromContainers/FIXES.md.
- LivingMap : configuration conservatrice, rendu détaillé/hauteurs/surimpressions sur l'eau désactivés. Couper/replanter des arbres et modifier une base dans le monde réel reste à vérifier.

Le build racine exécute les contrôles existants de palette et d'identité. Il ne lance ni Valheim ni un serveur, et n'installe aucune DLL.

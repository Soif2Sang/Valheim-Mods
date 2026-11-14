# FarLands allégé

Profil de réglages pour la DLL officielle FarLands 1.3.0, sans modification de son code. Terrain 4000 m, forêt 2500 m, modèles 3D 600 m puis imposteurs, herbe 80 m, atlas 64, interpolation des imposteurs et ombres lointaines désactivées. Détail automatique désactivé. VEE conserve les bâtiments à 4 km et les bâtiments FarLands restent désactivés. HorizonClarity doit être désactivé pour laisser FarLands gérer l'air.

Le recul des modèles 3D change le relief des arbres, la baisse d'herbe et les ombres changent le détail proche/lointain. Les paramètres d'air, d'eau et leurs réflexions restent ceux de FarLands par défaut ; aucun shader, texture ou matériau du jeu n'est remplacé. Les distances de portée ne garantissent pas que toute végétation du serveur soit exactement reproduite.

Test : `tools/WorldBenchmark/run.ps1 -GamePath GAME_DIRECTORY -WorldDirectory PRIVATE_WORLD_DIRECTORY -ClientPack COMPLETE_CLIENT_ZIP -Profile FarLandsLight`. Garder le profil FarLands comme référence ; le banc fixe AutoDetail=false aussi sur cette référence pour éviter une géométrie variable. Rendu forcé de caméra et lecture synchrone d'un pixel : coût CPU/GPU avec attente et transfert, pas FPS réels du client.

Packaging : `tools/package-client.ps1 -BaseZip APPROVED_FULL_ZIP -OutputDirectory OUTPUT_DIRECTORY -FarLandsLight`. Les binaires sous dist doivent avoir été compilés et vérifiés depuis les sources inchangées du monorepo. Le script réactive uniquement la DLL 1.3.0 attendue, applique les réglages génériques et actualise le pack complet et ses empreintes. Aucun identifiant ni sauvegarde à distribuer.

Validation du 14 novembre 2026 : chargement et rendu sur copie du monde réussis, mêmes portées vérifiées. Un couple de relevés hors écran donne une petite baisse du coût caméra/synchronisation (4,2 % de moyenne) ; il ne prouve pas un gain de FPS en partie. Voir docs/VALIDATION.md pour le protocole et ses limites. Profil proposé comme essai utilisateur réversible.

# Historique et mises à jour amont

Les imports sont des subtrees Git sans squash. Les historiques amont sont donc accessibles via `git log --all`. Pour suivre un fichier importé, utiliser `git log --follow -- <fichier>`. Le manifeste UPSTREAMS.json fixe les versions ; aucune mise à jour automatique au build.

LivingMap et RememberServerPassword peuvent être mis à jour par `git subtree pull --prefix=upstream/NOM URL REF`, puis revue du diff et mise à jour du manifeste. Ne pas utiliser aveuglément HEAD : garder une version choisie et revalider nos ponts.

Craft From Containers partage son dépôt amont avec beaucoup d'autres mods. Seul son dossier a été extrait avec `git subtree split --prefix=CraftFromContainers`, ce qui garde auteurs et messages mais donne de nouveaux identifiants aux commits extraits. Pour mettre à jour, refaire ce split dans un clone du dépôt amont, puis importer la branche extraite par `git subtree pull --prefix=upstream/CraftFromContainers CHEMIN_CLONE BRANCHE_EXTRAITE`. Notre correctif reste un commit distinct après l'import.

Les licences sont conservées. Les DLL et ZIP suivis par les auteurs ont été retirés de l'arbre courant ; ils restent dans leur historique public importé. Ce dépôt ne republie aucune assembly du jeu ni aucune donnée personnelle locale.

Ce dépôt est un agrégat avec plusieurs origines. GitHub ne peut pas l'afficher comme un fork natif de plusieurs dépôts à la fois. Le fork individuel Craft From Containers reste disponible sur https://github.com/Soif2Sang/ValheimMods/tree/fix/craft-from-containers-shift-fill pour préparer une éventuelle contribution amont.

# Progressive Map 0.1.1

Correctif client pour Valheim 1.0.21. Il remplace la génération synchrone des textures de carte par une coroutine sur le thread principal : calcul par tranches d'environ 4 ms, sans déplacer d'API Unity dans un thread de travail. Les conditions natives de sol, collision, réseau et apparition du personnage ne sont pas modifiées. FarLands reste inchangé et conserve sa génération asynchrone.

## Comportement

La procédure native peut continuer pendant le calcul. La carte complète, les couleurs Biome Clarity et les couches LivingMap deviennent disponibles à la fin. Les sauvegardes et le brouillard d'exploration restent gérés par le jeu. Si le cache natif est chargé sans régénération, le correctif ne démarre pas de travail supplémentaire.

Le calcul total n'est pas supprimé : la carte peut mettre plus longtemps à être prête, en échange d'un jeu qui continue de répondre. Le test local, avec un nouveau monde et une instance masquée, a duré 200,51 secondes de calcul réparti sur 2215 images. Ce temps comprend les pauses où le moteur charge/génère le monde. Ce n'est pas une promesse de durée sur le serveur ou dans un client au premier plan. Les images de terrain de la petite et de la grande carte restent masquées pendant l'attente. Le libellé du biome affiche « Carte en préparation » avec le pourcentage ; les textures sont révélées après publication et une image supplémentaire permettant à LivingMap de s'initialiser.

## Compatibilité

Le même GetBiome, GetBiomeHeight, GetPixelColor et GetMaskColor que le générateur natif est utilisé, aux mêmes coordonnées et avec les mêmes arguments. Les textures sont publiées d'un bloc à la fin, puis le cache natif est écrit par la méthode native. Il n'y a pas de cache de monde personnalisé à synchroniser.

Biome Clarity.Build et LivingMap.Tick attendent la fin de ce calcul pour éviter une recoloration vide ou un cache de forêt calculé sur des textures incomplètes. Biome Clarity est ensuite appliqué ; LivingMap s'initialise au tick suivant. Les références de textures natives ne sont pas remplacées par le correctif. Versions testées : Biome Clarity 0.3.3, LivingMap 0.19.2. Autres mods du pack non inclus dans le test isolé.

En cas d'exception, retour au générateur synchrone natif. Quitter le monde annule le travail en cours. Ces chemins de repli/annulation sont implémentés mais n'ont pas été déclenchés dans le test documenté.

## Vérification effectuée

`dist/map-validation/20261114-163403/PASS.json` : génération complète 2048², 2215 images, tranche de calcul maximale 7,876 ms, initialisation 73,259 ms, publication/cache/recoloration 867,921 ms. La cible de 4 ms est approximative : le contrôle se fait tous les 128 pixels et ne peut interrompre une opération native ou le ramasse-miettes.

Les trois textures brutes finales (biomes, forêt, hauteur) ont ensuite été comparées aux textures régénérées par la méthode native dans le même moteur et le même monde : empreintes identiques. L'appel natif de référence a bloqué pendant 9429,341 ms, recoloration comprise. La publication finale reste synchrone et peut occasionner un gel bref ; ce correctif n'élimine pas toutes les saccades et ne garantit pas l'absence de crash.

Les logs montrent Starting respawn avant la fin de la carte et la fin de l'introduction (Destroying valkyrie) avant publication. Le personnage était présent au contrôle final. Initialisation LivingMap avec les couches forêt confirmée après publication, et Biome Clarity appliqué. Il ne s'agit pas d'un test de connexion au VPS.

Commande de reproduction :

```powershell
.\tools\MapValidation\run.ps1 -TestProgressiveMap -AllowConcurrentClient
```

L'instance de test et ses sauvegardes sont isolées, les préférences d'affichage restaurées. Le contrôle effectue volontairement une génération native synchrone de référence à la fin dans la seule instance de test. Le client habituel ne doit jamais recevoir les DLL de validation.

## Installation / retrait

DLL : BepInEx/plugins/ProgressiveMap/ProgressiveMap.dll. Relancer uniquement le client pour la charger. Aucun besoin de modifier ou relancer le serveur. Pour revenir au comportement natif, retirer cette seule DLL jeu fermé ; les textures/cache/exploration restent au format natif. Aucune DLL FarLands modifiée.

## Correction du rendu provisoire — 0.1.1
Une capture joueur a révélé que les images de carte natives restaient visibles pendant le calcul avec des textures non initialisées. La 0.1.1 désactive les deux RawImage dès la demande de génération, affiche un état de préparation dans les libellés existants et ne restaure leur visibilité qu'une image après publication/recoloration. Le calcul et les vérifications d'apparition ne changent pas. Les visibilités et textes précédents sont aussi restaurés à l'annulation/repli.

Test moteur ciblé `dist/map-validation/20261114-165409/PASS.json`, carte de test 512² : 169 images observées de préparation avec les deux images de terrain désactivées, puis les deux réactivées ; empreintes des textures finales égales à celles du générateur natif. Ce test complète l'équivalence 2048² de la 0.1.0 ; il ne constitue pas un nouveau test de visibilité à 2048² ni une capture du vrai serveur. Aucun test déclenché du repli/annulation.

Reproduire le test de présentation plus court :
```powershell
.\tools\MapValidation\run.ps1 -TestProgressiveMap -ProgressiveMapTestSize 512 -AllowConcurrentClient
```
Sans l'option de taille, le test reste en 2048². La petite taille ne s'applique qu'au client de validation, jamais au client du joueur.

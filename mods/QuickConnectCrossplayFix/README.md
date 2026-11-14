# Quick Connect Crossplay Fix 0.1.0

Compatibilité client pour Remember Server Password officiel 1.2.4, testé avec Valheim 1.0.21.

Les logs du 14 novembre 2026 montrent une connexion normale réussie avec un identifiant PlayFab en majuscules. Remember Server Password mémorise une clé en minuscules ; Quick Connect la réutilise pour chercher le lobby, qui renvoie zéro résultat. Le correctif transforme uniquement les identifiants PlayFab hexadécimaux de 16 caractères en majuscules lors de la construction de la destination de connexion.

La DLL officielle, la clé de mémorisation et les fichiers chiffrés restent inchangés. Aucun changement côté serveur. Les destinations host/Steam restent inchangées.

Installation : BepInEx/plugins/QuickConnectCrossplayFix/QuickConnectCrossplayFix.dll, puis relancer seulement le client. Un mot de passe déjà mémorisé est conservé ; inutile de le saisir à nouveau pour cette correction.

Validation : build.ps1 compile et exécute les cas unitaires (majuscules/minuscules, destinations étrangères, clés invalides). Le rapport dist/map-validation/20261114-173702/PASS.json confirme dans Unity que le constructeur officiel corrigé produit une destination PlayFab en majuscules et laisse la destination mémorisée intacte. Chiffrement synthétique, propriété native du mot de passe et création du bouton également validés. Aucune connexion réelle au serveur effectuée par ce test ; la reconnexion réelle reste à confirmer.

Retour arrière, jeu fermé : retirer uniquement cette DLL. Les données mémorisées ne sont pas effacées.

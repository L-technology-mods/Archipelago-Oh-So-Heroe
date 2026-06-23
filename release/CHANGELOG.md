# Changelog

## 0.16.8 RC22

- Renommage des 259 locations en noms lisibles cote joueur.
- Ajout d'une table de traduction client `nom interne -> nom affiche`.
- La logique `collect_all_scenes` continue d'utiliser les noms internes pour exclure Bates, pickups, talks, gauntlets, etc.
- Cette mise a jour renomme les locations : nouvel APWorld et nouveau seed obligatoires.

## 0.16.7 RC21

- Renommage du check `KetSheoIslandsBeachSecret` en `Ket_SecretBeachBall`.
- Ajout de l'alias client `KetSheoIslandsBeachSecret` vers `Ket_SecretBeachBall`.
- Cette mise a jour renomme une location : nouvel APWorld et nouveau seed obligatoires.

## 0.16.6 RC20

- Ajout de l'alias d'animation `Brask/NSP` vers la location existante `Brask_JoeLonoe_Sex`.
- Cette mise a jour est client-only : pas de nouvel APWorld ni de nouveau seed si vous utilisez deja RC19.

## 0.16.5 RC19

- Ajout du check fiable `Pickup_HirotoDojo_HirotoDojoKey` pour la cle en haut a gauche du dojo.
- Suppression des checks non fiables ou impossibles : `Defeat_NinjaClan`, `Pickup_CasHouse_ButtStompAbility_110001`, `HirotoDojo_DojoSafe_Visited`, `Ket_Sitting`, `BraskPalaceLonoeOrgy`.
- Suppression des checks `LoodCityPark_GameOver01` a `LoodCityPark_GameOver08`, car ils deviennent inaccessibles apres Bates.
- Le goal `collect_all_scenes` passe a 158 scenes requises.
- Les items de soin/stat sont maintenant retardes si Joe est KO, pour eviter les overlays et etats HP incoherents.
- Cette mise a jour modifie les locations : nouvel APWorld et nouveau seed obligatoires.

## 0.16.4 RC18

- Ajout du check `TurnIn_HirotoDojo_HirotoDojoKey` quand une porte du dojo demandant `HirotoDojoKey` est ouverte.
- Ajout du check `Visit_DojoSecretBasement` quand le joueur entre dans le sous-sol du dojo.
- Ajout du check vanilla `HirotoDojo_DojoSafe_Visited`.
- Ajout des checks d'achat uniques `Buy_MirillBar_Drink1` et `Buy_MirillBar_Drink2`.
- Les checks `Visit_` et `Buy_` sont exclus du compteur `collect_all_scenes`.
- Cette mise a jour ajoute des locations : nouvel APWorld et nouveau seed obligatoires.

## 0.16.3 RC17

- Ajout du check `TurnIn_LoodCityWestAvenue_RedKeyCard`.
- Le check est envoye quand le jeu consomme la `RedKeyCard`, normalement au PNJ qui ouvre l'acces vers le parc de Bates.
- Le nouveau check est exclu du compteur `collect_all_scenes`.
- Cette mise a jour ajoute une location : nouvel APWorld et nouveau seed obligatoires.

## 0.16.2 RC16

- En mode `collect_all_scenes`, un KO de Bates trop tot lance maintenant une punition Bates puis renvoie le joueur vers `LoodCityWestAvenue`, au lieu de laisser un combat infini.
- Ajout d'une synchronisation periodique des flags de discussions et gauntlets deja valides dans la sauvegarde.
- Corrige les checks rates quand un joueur avait deja parle a un PNJ comme Brask avant la connexion AP ou avant le patch.
- Cette mise a jour client ne demande pas de nouveau seed si vous utilisez deja RC14/RC15.

## 0.16.1 RC15

- En mode `collect_all_scenes`, Bates ne peut plus etre battu avant que toutes les scenes non-Bates soient validees.
- Le KO final de Bates est annule et Bates est soigne si l'objectif scenes n'est pas termine, afin d'eviter le blocage apres les credits.
- Cette mise a jour ne change pas la logique APWorld et ne demande pas de nouveau seed si vous utilisez deja RC14.

## 0.16.0 RC14

- Slide Ability est maintenant obligatoire pour quitter Brask Jungle.
- Brask Palace et Forbidden Bayou exigent tous les deux Slide Ability.
- Cette mise a jour de logique necessite un nouvel APWorld et un nouveau seed.

## 0.15.9 RC13

- Les objets-capacites sont maintenant ajoutes a l'inventaire avant leur activation.
- Correction definitive de Butt Stomp, Fap On Command, Damage Numbers et Throw Moves a la reprise.
- Une competence desactivee manuellement par le joueur reste desactivee.

## 0.15.8 RC12

- Correction du demarrage du plugin bloque depuis RC9 par une surcharge Harmony ambigue.
- Le hook Damage Numbers cible maintenant exactement `ItemData.Collect(Collider, int)`.
- Conservation de la restauration des competences et du correctif de prechargement.

## 0.15.7 RC11

- Les competences AP deja recues sont restaurees apres un redemarrage du jeu.
- Correction de FapOnCommand, Damage Numbers et des autres competences absentes a la reprise.
- Les consommables et les ameliorations de statistiques ne sont pas dupliques.

## 0.15.6 RC10

- Les animations seulement prechargees ne peuvent plus envoyer de checks.
- Une scene est maintenant detectee au moment ou elle est reellement affichee.
- Les animations liees et les animations plein ecran restent prises en charge.

## 0.15.5 RC9

- Ajout du check de l'abaque Damage Numbers dans Treewish Forest.
- Detection specifique des objets physiques dont l'InstanceID vaut zero.

## 0.15.4 RC8

- Bates Attack Trap attend maintenant la fin d'une scene active avant de se lancer.
- Correction des grabs ennemis et des scenes interrompues apres un piege Bates.

## 0.15.3 RC7

- La mini-carte du HUD reste cachee tant que l'item Archipelago `AutoMap` n'a pas ete recu.
- La mini-carte est affichee des la reception de `AutoMap`.

## 0.15.2 RC6

- Le deverrouillage groupe de la galerie par Bates ne valide plus de checks.
- Les animations de Bates reellement jouees restent detectees normalement.

## 0.15.1 RC5

- Blocage direct des objets et competences attribues par le jeu vanilla.
- Ket ne peut plus donner Auto Map sans l'item Archipelago.
- Envoi DeathLink deplace hors du thread principal pour eviter un gel au KO.

## 0.15.0 RC4

- `Nothing` remplace par le filler visible `OhSoSnack` dans les nouveaux seeds.
- Lancement via Steam pour eviter le redemarrage apparent du jeu.
- Ajout de l'item `LustAttackAbility`.
- Verrouillage de Lust Attack, Auto Map et des competences non recues.
- Reconstruction des items recus lors de la reprise d'une partie.

## 0.14.0 RC3

- Console texte visible avec le jeu.
- Affichage des messages du serveur Archipelago.
- Affichage des locations envoyees et des items recus.

## 0.13.1 RC2

- Connexion WSS automatique aux serveurs heberges sur archipelago.gg.
- Locations conservees en attente pendant une deconnexion.
- Lecture des donnees JSON corrigee dans l'archive APWorld.

## 0.13.0 RC1

- Client BepInEx connecte a Archipelago.
- Bouton `Oh So Hero Client` dans Archipelago Launcher.
- 264 locations et 51 types d'items.
- Trois pieges fonctionnels.
- DeathLink.
- Verrouillage de 17 zones et protection anti-softlock.
- Objectifs `defeat_bates` et `collect_all_scenes`.

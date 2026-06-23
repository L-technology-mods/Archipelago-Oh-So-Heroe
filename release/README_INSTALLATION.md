# Oh So Hero! Archipelago - Release Candidate 0.16.8

Cette archive contient l'APWorld et le client BepInEx pour Oh So Hero!.

## Prerequis

- Oh So Hero! pour Windows via Steam.
- Archipelago 0.6.7 recommande.
- Une nouvelle sauvegarde pour chaque partie Archipelago.

## Installation du client

1. Ouvrir le dossier du jeu Steam. Il contient `OhSoHero.exe`.
2. Extraire tout le contenu de `OhSoHeroArchipelago-Client.zip` dans ce dossier.
3. Verifier que ce fichier existe :
   `BepInEx/plugins/OhSoHeroArchipelago/OhSoHeroArchipelago.dll`.
4. Lancer le jeu une premiere fois, puis le fermer.

Le paquet client contient BepInEx 5.4.23.5, le plugin et ses dependances.

## Installation de l'APWorld

1. Ouvrir `oh_so_hero.apworld` avec Archipelago Launcher.
2. Copier `OhSoHeroArchipelago.yaml` dans le dossier `Players` d'Archipelago.
3. Modifier au minimum `name` dans le YAML.
4. Generer et heberger la partie normalement.

## Connexion

1. Ouvrir Archipelago Launcher.
2. Cliquer sur `Oh So Hero Client`.
3. Selectionner `OhSoHero.exe` si demande.
4. Entrer l'adresse du serveur, le nom du slot et le mot de passe eventuel.

Une console texte s'ouvre avec le jeu. Elle affiche les connexions, les
locations envoyees, les items recus, les pieges et les messages DeathLink.

Un lien `archipelago://` peut aussi ouvrir directement ce client.

## Options YAML

- `goal`: `defeat_bates` ou `collect_all_scenes`.
- `trap_percentage`: pourcentage de fillers remplaces par des pieges.
- `submissive_trap_duration`: duree du blocage des attaques.
- `death_link`: active ou desactive DeathLink.

## Important pour les tests

- Ne pas utiliser une sauvegarde deja terminee pour valider une partie complete.
- Ne pas reutiliser un seed apres avoir modifie le YAML.
- Regenerer un seed apres toute mise a jour de l'APWorld.
- Les medkits et chillpills ne sont pas des checks.
- Les scenes de Bates ne comptent pas pour `collect_all_scenes`.

## Etat de cette release candidate

Valide en jeu : connexion, items, scenes, dialogues, ennemis, pieges,
DeathLink et verrouillage des zones.

A confirmer pendant une partie complete : gauntlets, sept objets importants
et signalement final des deux objectifs.

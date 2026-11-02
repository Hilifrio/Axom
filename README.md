# Axom

Jeu d'arcade à un bouton (Unity 2023.1). L'orbe tourne seule sur le cercle : il faut appuyer quand elle passe sur les zones.

Le cercle est une mesure de musique : un tour vaut 4 temps et les zones sont posées sur la grille rythmique. Viser le centre, c'est taper sur le temps.

## Règles

| Zone | Couleur | Action |
| --- | --- | --- |
| Tap | Cyan | Appuyer, le plus près possible de la marque blanche. Peut se déplacer dès le niveau 2. |
| Mine | Rouge | Ne pas appuyer dedans. Coûte une vie. |
| Flip | Violet | Appuyer : l'orbe change de sens. |
| Hold | Vert | Appuyer sur un bord, relâcher sur l'autre. |
| Gold | Or | Bonus éphémère : +1 vie, score ×3 et Fever. |

- Laisser passer une zone coûte une vie. Un appui dans le vide casse le combo sans coûter de vie.
- Le combo fait monter le multiplicateur jusqu'à ×6.
- Les Perfect et Great remplissent la jauge de Fever : score doublé et invincibilité pendant 16 temps.
- Tous les 8 coups, le niveau augmente : la musique accélère et s'étoffe, les zones rétrécissent.

## Réglages

Tout l'équilibrage est dans `GameConfig`, éditable depuis le composant `GameManager` de la scène.

## Code

- `Assets/Scripts/Core` : règles du jeu, sans dépendance à l'affichage (`GameSession`).
- `Assets/Scripts/View` : rendu néon sans sprite ni police (`GameView`, `NeonRenderer`, `VectorFont`).
- `Assets/Scripts/Audio` : musique et bruitages synthétisés en temps réel (`SynthEngine`).
- `Assets/Tests` : tests EditMode et PlayMode, dont une partie complète jouée par un bot.

## Tests

Depuis Window > General > Test Runner, ou en ligne de commande avec `-runTests -testPlatform EditMode` (ou `PlayMode`).
Dans les tests PlayMode, la variable d'environnement `AXOM_CAPTURE` indique un dossier où enregistrer des captures d'écran.

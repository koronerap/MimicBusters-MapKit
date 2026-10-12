# Mimic Busters Map Kit

Crée tes propres cartes pour **Mimic Busters** dans Unity, puis parcours-les dans le jeu en un clic.

<!-- languages -->
🌐 [English](README.md) · [Türkçe](README.tr.md) · [Deutsch](README.de.md) · [Español](README.es.md) · **Français** · [Italiano](README.it.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [Українська](README.uk.md) · [Ελληνικά](README.el.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [简体中文](README.zh-Hans.md) · [繁體中文](README.zh-Hant.md) · [العربية](README.ar.md)

Le kit est un projet Unity qui contient le même code de carte que le jeu. Tu construis une carte comme une
scène Unity ordinaire, tu fais le bake de son éclairage dans Unity, et le kit la vérifie selon les règles du
jeu, l'empaquette dans un seul fichier que Windows, Linux et macOS ouvrent tous, l'installe dans ton jeu et
lance le jeu dessus.

Les cartes faites avec ce kit se jouent en **Standard** (Classic dans le jeu : Chasseurs et Modeleurs,
ainsi que ses variantes Companion et Infestation, qui utilisent les mêmes marqueurs), **Entraînement**
(Practice dans le kit : construction libre, sans manches), **Deathmatch** (tout le monde est armé, pas de
camps) et **Devine l'imitation** (chacun son tour, un joueur modélise et les autres devinent).

## Ce qu'il te faut

- **Unity 6000.3.8f1**, exactement cette version, depuis Unity Hub. Les cartes sont des asset bundles Unity,
  et le jeu ne peut lire que les bundles créés par sa propre version d'Unity. Aucun module supplémentaire :
  le kit empaquette avec ce qui est fourni avec l'éditeur.
- **Mimic Busters**, installé via Steam, pour tester tes cartes.

## Pour commencer

1. Télécharge le kit : **Code > Download ZIP** sur GitHub, ou un zip depuis **Releases**. Décompresse-le dans
   un dossier au chemin court.
2. Dans Unity Hub : **Add > Add project from disk**, choisis le dossier décompressé et ouvre-le avec
   6000.3.8f1. La première importation prend quelques minutes.
   Une fois qu'elle est terminée, la fenêtre **Bienvenue** s'ouvre : choisis-y ta langue (le kit parle
   anglais, turc, allemand, espagnol, français, italien, portugais brésilien, russe, ukrainien, grec,
   japonais, coréen et chinois) et vérifie qu'il a trouvé le jeu. Tu peux la rouvrir avec
   **Map Kit > Welcome**, et changer la langue plus tard dans **Map Kit > Settings** ou avec la roue dentée de
   n'importe quelle fenêtre du kit.
3. **Map Kit > New Map**. Donne un nom à la carte, coche ses modes et laisse **Ajouter une salle d'exemple**
   activé pour ta première carte. Le kit crée une scène dans `Assets/Maps/<Name>/` où tout ce dont le jeu a
   besoin est déjà en place : les infos de la carte, les points d'apparition, les limites de la carte, un
   Probe Volume et les bons réglages d'éclairage.
4. **Window > Rendering > Lighting > Generate Lighting**. Le premier bake prend un moment.
5. **Map Kit > Validate**. Il doit indiquer que le jeu ouvrira la carte.
6. **Map Kit > Pack and Test**. La première fois, regarde la section **Jeu** : le kit cherche le jeu dans tes
   bibliothèques Steam ; s'il est ailleurs, appuie sur **Choisir...** et sélectionne l'exécutable du jeu.
   Appuie ensuite sur **Empaqueter et tester**. Le jeu démarre directement dans une session privée en
   Entraînement sur ta carte.

Ensuite, fais-en ta carte : supprime la salle d'exemple, importe tes propres modèles et répète les étapes 4
à 6.

## Construire une carte

**Géométrie.** Importe les modèles (FBX, OBJ, fichiers Blender) et les textures comme d'habitude et place-les
dans la scène. Utilise les shaders de l'Universal Render Pipeline : **Lit**, **Simple Lit**, **Unlit** ou
**Baked Lit**. Donne un collider à tout ce sur quoi les joueurs marchent ou contre quoi ils butent. Marque la
géométrie statique comme **Static** pour qu'elle reçoive la lumière bakée.

**Infos de la carte.** L'objet `Map` de la nouvelle scène porte un **Kit Map Descriptor** : le nom, la
description, l'auteur et les modes de la carte, tels que le jeu les affiche. Il doit y en avoir exactement un.

**Points d'apparition.** Un point d'apparition est un objet vide avec un **Map Marker**. Son Inspector demande
deux choses dans l'ordre : **1. Modes**, quels modes de jeu l'utilisent, et **2. Types**, à quoi il sert. En
dessous, il indique quel mode utilise le marqueur et comme quoi, et avertit d'un type qu'aucun des modes
cochés ne lit :

| Type | Sert à |
|---|---|
| HiderSpawn | Là où les Modeleurs commencent une manche (Standard). C'est aussi là qu'apparaissent les joueurs en Entraînement. |
| HunterSpawn | Là où sortent les Chasseurs quand la chasse commence (Standard). Ils passent la préparation dans la salle des Chasseurs du jeu. |
| LobbySpawn | Là où tout le monde attend pendant que le salon se remplit. Facultatif : sans lui, le salon utilise les HiderSpawns. |
| HunterDoor | Facultatif. Un objet qui reste fermé pendant l'attente au salon et la préparation, et s'ouvre quand la chasse commence. Seul le premier compte. |
| ModelerSpawn | Entraînement : là où les joueurs apparaissent, comme HiderSpawn et LobbySpawn. « Devine l'imitation » : là où modélise le joueur dont c'est le tour. |
| ArenaSpawn | Facultatif. Une position de duel en 1v1 (Standard). La salle des Chasseurs du jeu a les siennes. |
| GuessComputer | Facultatif (« Devine l'imitation »). Là où le jeu installe son bureau d'ordinateur ; le joueur dont c'est le tour s'y assoit pendant que son modèle se tient sur le ModelerSpawn. Seul le premier compte. Sans lui, le joueur est caché dans son modèle. |

Standard a besoin d'au moins un HunterSpawn et d'un HiderSpawn. Le mode Entraînement a besoin d'un HiderSpawn,
d'un LobbySpawn ou d'un ModelerSpawn avec Entraînement coché. Deathmatch utilise tous les HunterSpawn,
HiderSpawn et LobbySpawn avec Deathmatch coché et place un joueur sur celui qui est le plus éloigné de tous :
prévois-en donc au moins six, bien répartis. Le mode « Devine l'imitation » place ceux qui devinent sur les
LobbySpawns (sur les HiderSpawns s'il n'y en a pas) et le joueur dont c'est le tour sur un seul ModelerSpawn,
tous avec « Devine l'imitation » coché : mets celui-là là où tout le monde peut le voir. Places-en quelques-uns de chaque, bien répartis, sur le sol, tournés vers l'intérieur de la carte. La ligne tracée depuis le
marqueur montre dans quelle direction le joueur regarde. Un marqueur sans mode coché n'est utilisé par aucun
mode.

**Limites de la carte.** Le **Map Bounds Volume** est une boîte autour de toute la zone de jeu. Une fois le
bake fait, le jeu sait où les joueurs peuvent se trouver et ramène quiconque tombe hors de la carte. Fais en
sorte que la boîte couvre tous les endroits qu'un joueur peut atteindre, avec de la marge au-dessus pour les
sauts et les toits, puis lance **Map Kit > Bake Map Bounds**. Refais le bake chaque fois que tu déplaces des
murs, des sols ou des points d'apparition. Si le validateur dit que la zone de jeu touche la paroi de la
boîte sur une carte fermée, il y a un trou là où il l'indique.

Le bake remplit l'air à partir de tes points d'apparition vers l'extérieur, par cellules d'un demi-mètre. Une
salle que les joueurs n'atteignent que par téléportation n'est jamais trouvée de cette façon : ajoute un point
dans cette salle aux **Seeds** du volume, et un autre pour le reste de la carte, car une fois Seeds rempli, le
remplissage part uniquement de ces points. Le validateur te prévient si un point d'apparition ou la
destination d'un téléporteur se retrouve dehors. Un passage d'une seule cellule de large (une fente au-dessus
d'un mur, une fissure étroite à côté d'une plateforme) est signalé comme fuite possible : fais des passages
soit bien fermés, soit d'au moins un mètre.

**Échelles.** Une **Map Ladder** est une boîte dans laquelle les joueurs grimpent tant qu'ils s'y trouvent,
comme on grimpe à une échelle dans Half-Life et Counter-Strike : avancer vers l'échelle fait grimper,
regarder en haut ou en bas décide du sens, les touches latérales déplacent le long de l'échelle, sauter fait
lâcher prise. Ajoute-la à un objet vide (ou au modèle de l'échelle) et dimensionne la boîte dans la Scene view
avec ses poignées. La boîte est l'espace où se tient un joueur qui grimpe : les barreaux et l'air devant eux.
Sa **flèche bleue (Z) est la direction où regarde le grimpeur**, vers l'échelle. La montée suit le haut de
l'objet ; pour une échelle appuyée contre un mur, incline donc l'objet sur X, le haut vers le mur, de
30 degrés au plus. Fais monter le haut de la boîte jusqu'au sol auquel mène l'échelle, au besoin un peu
au-dessus. Le modèle lui-même peut être solide ; les joueurs n'ont pas besoin de le toucher.

**Variantes de carte.** Certaines parties de la carte peuvent changer à chaque manche, pour que les Chasseurs
ne puissent pas apprendre la carte par cœur. Mets un **Map Variant Group** sur un objet vide et fais de chaque
version de cette partie un **enfant direct** de cet objet : un couloir ouvert, le même couloir bouché, des
caisses empilées de trois façons. À chaque manche, le jeu active UN SEUL enfant et désactive les autres ; le
serveur choisit et tout le monde voit la même chose. **Modes** indique dans quels modes le choix se fait
(Standard par défaut) ; dans les autres, les enfants restent tels que tu les as laissés dans la scène.
L'Inspector du groupe liste les versions, chacune avec un bouton **Afficher**, pour les construire et les
vérifier une par une.
- **Poids :** ajoute une **Map Variant Option** à un enfant pour le rendre plus ou moins probable (1 sans
  elle ; 0 jamais).
- **Avoid Repeat** (activé par défaut) : la version de la manche précédente n'est pas rechoisie tant qu'une
  autre peut l'être.
- **Link Id :** les groupes qui ont le même identifiant choisissent le même numéro d'enfant, pour que les deux
  moitiés d'une même idée concordent : une porte fermée ici pendant qu'un passage s'ouvre là-bas.
- Des groupes peuvent se trouver dans les versions d'un autre groupe.
- Les points d'apparition à l'intérieur d'une version ne servent que pendant les manches où cette version est
  activée.
- Ne marque rien à l'intérieur d'une version comme **Occluder Static** : l'occlusion culling le considérerait
  comme toujours présent, et tout ce qui se trouve derrière disparaîtrait pendant les manches où il est
  désactivé. Le validateur t'en avertit.

**Éclairage.** Garde les réglages d'éclairage fournis avec la nouvelle carte : éclairage Mixed en mode
**Shadowmask**, lightmaps **Directional**, **Realtime Global Illumination désactivée**. Le jeu n'embarque des
shaders que pour cette configuration. Les lumières sont **Mixed** ou **Baked** ; plus de quatre lumières en
temps réel avec ombres ralentissent le jeu. Le brouillard, si tu en veux, doit être en
**Exponential Squared**. L'**Adaptive Probe Volume** éclaire les joueurs et les Modeleurs pour qu'ils
s'accordent à la carte ; son bake se fait avec le reste (il fait partie de Generate Lighting). Refais le bake après chaque
modification de la géométrie ou des lumières, sinon le jeu affiche l'ancienne lumière.

**Post-traitement.** Un **Volume** ne peut porter que des effets de couleur et de tonalité : Bloom,
Tonemapping, Color Adjustments, White Balance, Lift Gamma Gain, Shadows Midtones Highlights, Split Toning,
Channel Mixer, Color Curves, Vignette.

## Logique de la carte

Tu peux rendre une carte interactive à la manière de Hammer : une plateforme qui bouge quand on appuie sur un
bouton et revient un peu plus tard, un son, une zone qui blesse. Aucun code à écrire ; tout se relie dans
l'Inspector, et le jeu fait le travail.

Ajoute-les depuis **Add Component > Map Kit > Logic** :

| Composant | Ce qu'il fait |
|---|---|
| Map Button | Un bouton que les joueurs pressent avec la touche d'interaction. Il lui faut un collider. Qui peut le presser, un temps de recharge, une invite. |
| Map Mover | Déplace et fait tourner l'objet et ses enfants le long d'une liste d'arrêts, en emportant les joueurs qui sont dessus. Return After : revient de lui-même ce nombre de secondes après avoir atteint le dernier arrêt. PingPong et Loop tournent tout seuls. |
| Map Trigger | Une boîte invisible : des événements quand un joueur entre ou sort, et pour le premier entré et le dernier sorti. |
| Map Teleport | Envoie quiconque entre dans sa boîte vers un objet de destination, tourné dans la direction où celui-ci regarde. |
| Map Hurt Zone | Blesse quiconque est à l'intérieur à chaque intervalle, ou tue sur le coup. Personne ne marque de point pour une mort causée par la carte. |
| Map Sound | Un son à l'endroit où il se trouve : une fois ou en boucle, atténué avec la distance ou identique partout. |
| Map Effect | Un des effets du jeu : fumée, vapeur, feu, étincelles, poussière, gouttes. Couleur, taille, quantité. |
| Map Toggle | Affiche ou masque l'objet et ses enfants (ce qui est dessiné, ce qui est solide, les lumières). |
| Map Relay | Transmet une entrée à plusieurs sorties, chacune avec son propre délai. |
| Map Timer | Déclenche un événement à chaque intervalle tant qu'il est activé. |
| Map Counter | Compte, et déclenche un événement quand le compte atteint sa cible. |
| Map Round Events | Manche commencée, chasse commencée, manche terminée. |

**Relier.** Chaque composant a une liste de **Sorties** : un *événement* (Pressed, ArrivedAtEnd, Entered...),
une *cible* (un autre composant de logique, glissé dedans), une *entrée* (Forward, Back, Play, Enable,
Disable...), un *délai* et, quand l'entrée en prend un, un nombre (un arrêt, une quantité). L'Inspector ne
propose que les événements que le composant déclenche et les entrées que sa cible accepte, marque en orange
une connexion qui ne peut pas fonctionner, et liste en dessous ce qui est relié au composant.
**Map Kit > Logic** montre toutes les connexions sur une seule page.

Exemple : l'événement *Pressed* du bouton vers l'entrée *Forward* de l'ascenseur. L'ascenseur a un arrêt
(1,8 m plus haut) et Return After 4 : on appuie, il monte, quatre secondes plus tard il redescend. La salle
d'exemple de **New Map** contient exactement cela, plus un bruit de moteur (le *Started* de l'ascenseur vers
le *Play* d'un Map Sound) et, dans l'autre salle, un radiateur : un seul bouton active et désactive à la fois
une Map Hurt Zone et un Map Effect de feu.

**Celui qui l'a déclenché.** Un événement causé par un joueur (une pression, l'entrée dans un déclencheur, une
blessure) se souvient de ce joueur tout au long de la chaîne, délais compris. Deux entrées agissent sur lui :
*HurtActivator* sur une zone de dégâts lui inflige ses dégâts une fois (ou le nombre de la sortie à la
place), et *TeleportActivator* sur un téléporteur l'envoie à sa destination. Un bouton de trappe qui ne fait
tomber que celui qui appuie, un déclencheur qui ne blesse que celui qui y a mis le pied : relie-le, aucune
boîte nécessaire. Un téléporteur utilisé uniquement de cette façon doit démarrer désactivé, sinon sa propre
boîte envoie aussi des gens.

**Le voir avant d'empaqueter.** Dans la Scene view, la ligne de chaque sortie est étiquetée avec son événement
et son entrée. Un Map Mover sélectionné a une poignée sur chaque arrêt et, sous Aperçu, une copie
transparente de lui-même que tu peux faire glisser le long du trajet ou lire comme le jeu la joue, avec la
durée du trajet.

Au début de chaque manche, tout revient à l'état où tu l'as écrit. Un composant dont Start Enabled est
désactivé ne fait rien tant qu'une entrée *Enable* n'arrive pas.

**Attention à ceci.**

- Rien de ce qui bouge ne peut être **Static** : le static batching le fige sur place et sa lumière bakée
  reste derrière. Ce sont les probes qui éclairent ce qui bouge.
- Une plateforme mobile pousse un joueur sur son chemin ; quand elle ne le peut pas, elle fait ce que dit
  **When Blocked** : faire demi-tour, écraser ou continuer.
- Un joueur qui saute d'une plateforme mobile garde sa vitesse latérale jusqu'à l'atterrissage ; un saut sur
  un bac en mouvement retombe donc sur le bac.
- La destination d'un téléporteur est vérifiée comme un point d'apparition : rien de solide à cet endroit, un
  sol en dessous. Une destination dans la propre boîte d'un téléporteur renvoie le joueur une seconde plus
  tard ; le validateur t'en avertit.
- Sons : 2 minutes au plus chacun, mono ou stéréo, jusqu'à 48 kHz ; tous les sons d'une carte, 32 Mo au plus
  en mémoire. Charge les sons longs en **Compressed In Memory** ou **Streaming**. Les sons passent par le
  Volume des effets du joueur.
- Une boucle sans délai (A déclenche B et B déclenche A aussitôt) est coupée à 32 étapes dans le jeu, et le
  validateur la compte comme une erreur. Donne un délai à une sortie de la boucle. Seules les entrées qui
  font agir leur cible sur-le-champ ferment une telle boucle (une pression, le déclenchement d'un relais, le
  changement d'un compteur, un mover envoyé quelque part) : un bouton qui se désactive lui-même, ou un
  morceau qui en lance un autre quand il se termine, ne pose pas de problème.
- Limites : 256 composants de logique, 16 sorties chacun, 200 entrées par seconde pour toute la carte.

**L'essayer.** Pack and Test ouvre la carte en Entraînement, où il n'y a ni Chasseurs ni Modeleurs. Dans la
console développeur, `maprole hunter` ou `maprole hider` te permet d'essayer les filtres de rôle, `maplogic`
liste l'état de chaque composant, et `mapfire <number> <input>` envoie une entrée à la main.

### La carte d'exemple : Logic Playground

**Map Kit > Examples > Logic Playground** construit dans `Assets/Maps/LogicPlayground/` une carte en greybox
où chaque composant de logique est à l'œuvre. Fais le bake de son éclairage quand le kit te le demande, puis
teste-la avec Pack and Test comme n'importe quelle carte, ou ouvre ses objets pour voir comment ils sont
reliés (**Map Kit > Logic** liste toutes les connexions). Ses couleurs disent ce que sont les choses : le gris
est le monde, l'orange est ce sur quoi on appuie, le jaune bouge, le rouge blesse, le bleu téléporte, le
violet apparaît et disparaît. Le plafond est invisible : un collider sans rien de dessiné, qui ferme la cour
ouverte pour le bake des limites de la carte.

Neuf salles sur une grille de 3 sur 3, 36 m de large. Tout le monde commence dans celle du milieu.

| Salle | Ce qu'elle montre |
|---|---|
| 00 Hub (milieu) | Map Round Events : un carillon quand une manche commence ; une sirène et une balise de fumée rouge quand la chasse commence ; un feu d'artifice quand la manche se termine (un relais qui joue des étincelles et un son avec des délais). Un générateur qui ronronne en boucle depuis le début. |
| 01 Doors (sud) | Une porte coulissante qui se referme toute seule quatre secondes après s'être ouverte (Return After), fait demi-tour si quelqu'un est sur son chemin, et bourdonne. Un déclencheur devant elle qui appuie sur son bouton pour les Chasseurs (Press). Une porte battante que chacun des deux boutons ouvre ou ferme (Reverse). |
| 02 Elevator (nord) | Trois étages : des boutons d'appel à chacun, des boutons qui voyagent avec la cabine (Go To Stop avec l'étage comme nombre), un moteur au démarrage, un carillon à chaque étage. |
| 03 Platforms (est) | Une fosse qui tue quiconque y tombe. Un bac qui fait l'aller-retour au-dessus (PingPong) avec des boutons pour l'arrêter et le relancer (Pause, Resume) ; un carrousel qui tourne au milieu (Loop), allumé et éteint (Toggle). Saute du bac : tu gardes sa vitesse. |
| 04 Hazards (ouest) | Un écraseur (When Blocked: Crush). Des bouches de feu allumées à des moments aléatoires : un minuteur déclenche un relais qui active trois zones de dégâts et le feu, puis les éteint une seconde et demie plus tard. Un tuyau de vapeur qui ébouillante. Un interrupteur général qui met tout en pause. |
| 05 Vault (nord-est) | Trois runes, chacune comptée une seule fois (Add, et la rune se désactive elle-même) ; à trois, le bouton du coffre se déverrouille. Pressé alors qu'il est verrouillé, il bourdonne. Une rune leurre en retire une (Subtract) ; le passe-partout des Chasseurs fixe le compte d'un coup (Set Value) ; une remise à zéro referme tout (Reset Count, Lock, Back, Enable). |
| 06 Teleports (nord-ouest) | Deux pads qui envoient les joueurs l'un vers l'autre. Une salle sans porte : un pad t'y fait entrer, un bouton à l'intérieur ne fait sortir que toi (Teleport Activator sur un téléporteur désactivé). Un raccourci pour les Modeleurs vers la corniche de la salle sud-est. |
| 07 Triggers and Toggles (sud-est) | Une plaque de pression : tant que quelqu'un se tient dessus (Occupied, Emptied), un pont mène à la corniche. La rambarde du pont est un toggle à l'intérieur du toggle du pont. Une alarme qui sonne une fois par manche quand un Chasseur entre. Un recoin caché derrière un panneau. |
| 08 Sounds and Effects (sud-ouest) | Chaque effet sur un socle avec son bouton. Un juke-box de deux morceaux, chacun lançant l'autre quand il se termine (Finished). Une annonce entendue dans toute la carte, une fois par manche. Un bouton qui ne blesse que celui qui appuie (Hurt Activator). |

## Les règles

Le jeu lit une carte avant de laisser Unity l'ouvrir, et refuse tout ce qui sort de ces règles.
**Map Kit > Validate** les vérifie toutes dans l'éditeur ; **Pack and Test** vérifie à nouveau les fichiers
empaquetés avec le propre code du jeu.

- **Composants :** Mesh Filter, Mesh Renderer, LOD Group, Box, Sphere, Capsule et Mesh Colliders, Light,
  Reflection Probe, Light Probe Group, Occlusion Area et Portal, Adaptive Probe Volume, Volume, ainsi que les
  composants du kit (Map Marker, Map Mode Object, Map Bounds Volume, Penetrable Surface, Probe Volume
  Region, Kit Map Descriptor, Map Ladder, Map Variant Group, Map Variant Option) et les composants de logique de la carte. Pas de scripts à toi,
  ni caméras, audio sources, systèmes de particules, UI, vidéo, terrain, animation ou corps physiques. Le son
  et les effets passent par Map Sound et Map Effect.
- **Shaders :** Universal Render Pipeline Lit, Simple Lit, Unlit, Baked Lit. Ciel : Skybox Procedural,
  Cubemap, 6 Sided ou Panoramic.
- **Layers :** Default, Ignore Raycast, MapBlocker (murs invisibles), GuessMapBlocker.
- **Éclairage :** comme ci-dessus. 64 lumières au plus.
- **Taille :** au plus 256 Mo pour le paquet, 512 Mo de textures, 8192 pixels sur le plus long côté d'une
  texture, 3 millions de triangles, 6 millions de vertices et 10 000 renderers.
- **Nom :** l'identifiant de la carte est le nom de sa scène en minuscules : lettres, chiffres, `-` et `_`,
  jusqu'à 23 caractères. Les noms de scène que le jeu utilise lui-même (Startup, Menu, Game, HunterRoom et
  d'autres, ainsi que tout ce qui commence par `Map_` ou `GameMode_`) sont interdits.

Les erreurs signifient que le jeu n'ouvrira pas la carte. Les avertissements méritent un coup d'œil mais ne
l'empêchent pas.

## Jouer avec des amis

Tous les joueurs de la session doivent avoir exactement les mêmes fichiers de carte : le jeu les compare
avant de charger la carte. Le paquet est le même pour tous les systèmes, donc empaquette la carte une fois et
envoie à tes amis le dossier `Builds/Maps/<id>/` du kit (zippé). Chacun d'eux place le dossier, sans le
modifier, dans le dossier des cartes de son jeu :

| Système | Dossier |
|---|---|
| Windows | `%USERPROFILE%\AppData\LocalLow\DevAdam\MimicBusters\Maps\Local\` |
| macOS | `~/Library/Application Support/DevAdam/MimicBusters/Maps/Local/` |
| Linux | `~/.config/unity3d/DevAdam/MimicBusters/Maps/Local/` |

Le résultat ressemble à `...\Maps\Local\<id>\map.json` avec `map.any.bundle` à côté. La carte apparaît alors
dans le choix de la carte du jeu, sous **Communauté**. La prise en charge du Workshop Steam viendra plus
tard, depuis le jeu.

## Quand quelque chose ne va pas

- **Le kit dit que le jeu refuserait la carte.** Les raisons sont listées dans la fenêtre de Pack and Test.
  Le rapport complet est dans la Console, ainsi que dans `Builds/Maps/<id>.report.txt`.
- **Le jeu dit que la carte n'a pas pu être ouverte.** Active **Paramètres > Jeu > Console développeur** dans
  le jeu, ouvre la console avec la touche sous Échap, et tape `kitmaps` : la commande liste tes cartes et la
  raison pour laquelle l'une d'elles ne peut pas être jouée. `kitinspect <id>` lit une carte sans la charger.
- **La carte est trop sombre ou a le mauvais éclairage.** Refais le bake de l'éclairage, puis empaquette à
  nouveau. Si le validateur dit que la scène utilise les données d'éclairage d'une autre scène (cela arrive
  avec les scènes copiées), utilise **Map Kit > Tools > Detach Copied Lighting**, puis fais le bake.
- **Les joueurs passent à travers le sol ou apparaissent dans les murs.** Vérifie les colliders, déplace le
  point d'apparition et refais le bake des limites de la carte.
- **Le jeu signale que la carte a été faite avec un kit plus ancien.** Télécharge le nouveau kit, ouvre ta
  carte dedans et empaquette-la à nouveau.

## Licence

Voir [LICENSE.md](LICENSE.md). Mimic Busters et son logo appartiennent à DevAdam et ne font pas partie de la
licence.

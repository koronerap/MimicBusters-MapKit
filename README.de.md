# Mimic Busters Map Kit

Bau in Unity deine eigenen Karten für **Mimic Busters** und lauf sie dann mit einem Klick im Spiel ab.

<!-- languages -->
🌐 [English](README.md) · [Türkçe](README.tr.md) · **Deutsch** · [Español](README.es.md) · [Français](README.fr.md) · [Italiano](README.it.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [Українська](README.uk.md) · [Ελληνικά](README.el.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [简体中文](README.zh-Hans.md) · [繁體中文](README.zh-Hant.md) · [العربية](README.ar.md)

Das Kit ist ein Unity-Projekt mit demselben Kartencode, den das Spiel verwendet. Du baust eine Karte als
ganz normale Unity-Szene, backst ihr Licht in Unity, und das Kit prüft sie anhand der Regeln des Spiels,
packt sie in eine einzige Datei, die Windows, Linux und macOS gleichermaßen öffnen, installiert sie in
deinem Spiel und startet das Spiel auf ihr.

Karten aus diesem Kit lassen sich in **Standard** (im Spiel Classic: Jäger und Modellierer, samt den
Varianten Companion und Infestation, die dieselben Marker verwenden), **Übung** (im Kit Practice: freies
Bauen, keine Runden), **Deathmatch** (alle bewaffnet, keine Seiten) und **Errate die Nachahmung** (reihum
modelliert einer, die anderen raten) spielen.

## Was du brauchst

- **Unity 6000.3.8f1**, genau diese Version, aus dem Unity Hub. Karten sind Unity-Asset-Bundles, und das
  Spiel kann nur Bundles lesen, die mit seiner eigenen Unity-Version erstellt wurden. Keine zusätzlichen
  Module: Das Kit packt mit dem, was der Editor mitbringt.
- **Mimic Busters**, über Steam installiert, um deine Karten zu testen.

## Erste Schritte

1. Lade das Kit herunter: **Code > Download ZIP** auf GitHub oder eine ZIP-Datei unter **Releases**. Entpacke
   es an einen Ort mit kurzem Pfad.
2. Im Unity Hub: **Add > Add project from disk**, wähle den entpackten Ordner und öffne ihn mit 6000.3.8f1.
   Der erste Import dauert ein paar Minuten.
   Danach öffnet sich das Fenster **Willkommen**: Wähle dort deine Sprache (das Kit spricht Englisch,
   Türkisch, Deutsch, Spanisch, Französisch, Italienisch, brasilianisches Portugiesisch, Russisch,
   Ukrainisch, Griechisch, Japanisch, Koreanisch und Chinesisch) und prüfe, ob es das Spiel gefunden hat. Du
   kannst es über **Map Kit > Welcome** wieder öffnen und die Sprache später unter **Map Kit > Settings** oder
   über das Zahnrad in jedem Fenster des Kits ändern.
3. **Map Kit > New Map**. Gib der Karte einen Namen, hake ihre Modi an und lass bei deiner ersten Karte
   **Beispielraum hinzufügen** eingeschaltet. Das Kit erstellt in `Assets/Maps/<Name>/` eine Szene, in der
   alles, was das Spiel braucht, schon an seinem Platz ist: die Karteninfo, Spawnpunkte, die Kartengrenzen,
   ein Probe Volume und die richtigen Lichteinstellungen.
4. **Window > Rendering > Lighting > Generate Lighting**. Das erste Backen dauert eine Weile.
5. **Map Kit > Validate**. Es sollte melden, dass das Spiel die Karte öffnet.
6. **Map Kit > Pack and Test**. Schau beim ersten Mal in den Abschnitt **Spiel**: Das Kit sucht das Spiel in
   deinen Steam-Bibliotheken; liegt es woanders, drück **Wählen...** und wähle die ausführbare Datei des
   Spiels. Drück dann **Packen und testen**. Das Spiel startet direkt in eine private Sitzung im Modus Übung
   auf deiner Karte.

Danach mach sie zu deiner eigenen: Lösch den Beispielraum, bring deine eigenen Modelle hinein und wiederhole
die Schritte 4 bis 6.

## Eine Karte bauen

**Geometrie.** Importiere Modelle (FBX, OBJ, Blender-Dateien) und Texturen wie gewohnt und platziere sie in
der Szene. Verwende die Shader der Universal Render Pipeline: **Lit**, **Simple Lit**, **Unlit** oder
**Baked Lit**. Gib allem, worauf Spieler laufen oder wogegen sie stoßen, einen Collider. Markiere statische
Geometrie als **Static**, damit sie gebackenes Licht bekommt.

**Karteninfo.** Das Objekt `Map` in der neuen Szene trägt einen **Kit Map Descriptor**: Name, Beschreibung,
Autor und Modi der Karte, so wie das Spiel sie anzeigt. Es muss genau einen geben.

**Spawnpunkte.** Ein Spawnpunkt ist ein leeres Objekt mit einem **Map Marker**. Sein Inspector fragt der
Reihe nach zwei Dinge: **1. Modi**, welche Spielmodi ihn verwenden, und **2. Arten**, wofür er da ist.
Darunter steht, welcher Modus den Marker als was verwendet, und er warnt vor einer Art, die keiner der
angehakten Modi auswertet:

| Art | Wofür |
|---|---|
| HiderSpawn | Wo die Modellierer eine Runde beginnen (Standard). Auch dort erscheinen Spieler in Übung. |
| HunterSpawn | Wo die Jäger herauskommen, wenn die Jagd beginnt (Standard). Die Vorbereitung verbringen sie im Jägerraum des Spiels. |
| LobbySpawn | Wo alle warten, während sich die Lobby füllt. Optional: Ohne ihn verwendet die Lobby die HiderSpawns. |
| HunterDoor | Optional. Ein Objekt, das in der Lobby und während der Vorbereitung geschlossen bleibt und sich öffnet, wenn die Jagd beginnt. Nur das erste zählt. |
| ModelerSpawn | Übung: wo Spieler erscheinen, wie HiderSpawn und LobbySpawn. „Errate die Nachahmung“: wo der Spieler modelliert, der an der Reihe ist. |
| ArenaSpawn | Optional. Eine Position für ein 1v1-Duell (Standard). Der Jägerraum des Spiels hat seine eigenen. |
| GuessComputer | Optional („Errate die Nachahmung“). Wo das Spiel seinen Computertisch aufstellt; der Spieler, der an der Reihe ist, sitzt daran, während sein Modell auf dem ModelerSpawn steht. Nur der erste zählt. Ohne ihn ist der Spieler in seinem Modell versteckt. |

Standard braucht mindestens einen HunterSpawn und einen HiderSpawn. Übung braucht einen HiderSpawn, LobbySpawn
oder ModelerSpawn, bei dem Übung angehakt ist. Deathmatch verwendet jeden HunterSpawn, HiderSpawn und
LobbySpawn, bei dem Deathmatch angehakt ist, und setzt einen Spieler auf den, der am weitesten von allen
entfernt ist; gib dem Modus also mindestens sechs, gut verteilt. Der Modus „Errate die Nachahmung“ setzt die
Ratenden auf LobbySpawns (auf HiderSpawns, wenn es keine gibt) und den Spieler, der an der Reihe ist, auf
einen einzigen ModelerSpawn, alle mit angehaktem „Errate die Nachahmung“: Setz diesen dorthin, wo ihn alle sehen können. Setz von jedem ein paar, gut verteilt, auf den Boden, mit Blick in die Karte. Die vom Marker
ausgehende Linie zeigt, in welche Richtung der Spieler schaut. Ein Marker ohne angehakten Modus wird von
keinem Modus verwendet.

**Kartengrenzen.** Das **Map Bounds Volume** ist eine Box um den gesamten Spielbereich. Sobald es gebacken
ist, weiß das Spiel, wo Spieler sein können, und holt jeden zurück, der aus der Karte fällt. Lass die Box
alles abdecken, was ein Spieler erreichen kann, mit Platz nach oben für Sprünge und Dächer, dann
**Map Kit > Bake Map Bounds**. Backe sie neu, wann immer du Wände, Böden oder Spawnpunkte verschiebst. Wenn
der Validator meldet, dass der Spielbereich bei einer geschlossenen Karte bis an die Wand der Box reicht,
ist dort, wo er es angibt, ein Loch.

Das Backen füllt die Luft von deinen Spawnpunkten aus nach außen, in Zellen von einem halben Meter. Ein Raum,
den Spieler nur per Teleport erreichen, wird so nie gefunden: Füge in den **Seeds** des Volumes einen Punkt
in diesem Raum hinzu und einen weiteren für den Rest der Karte, denn sobald Seeds ausgefüllt sind, beginnt
die Füllung nur bei ihnen. Der Validator sagt es dir, wenn ein Spawnpunkt oder das Ziel eines Teleports
außerhalb landet. Eine Lücke, die nur eine Zelle breit ist (ein Schlitz über einer Wand, ein schmaler Spalt
neben einer Plattform), wird als mögliches Leck gemeldet: Mach Lücken entweder dicht oder mindestens einen
Meter breit.

**Leitern.** Eine **Map Ladder** ist eine Box, in der Spieler klettern, solange sie sich darin befinden, so
wie man in Half-Life und Counter-Strike an einer Leiter klettert: Bewegung zur Leiter hin klettert, nach
oben oder unten schauen bestimmt die Richtung, die Seitentasten bewegen dich an ihr entlang, Springen lässt
los. Füge sie einem leeren Objekt (oder dem Leitermodell) hinzu und passe die Größe der Box in der Scene
view mit ihren Griffen an. Die Box ist der Raum, in dem ein kletternder Spieler steht: die Sprossen und die
Luft davor. Ihr **blauer Pfeil (Z) ist die Richtung, in die der Kletternde schaut**, zur Leiter hin.
Geklettert wird entlang der Oben-Richtung des Objekts; für eine an eine Wand gelehnte Leiter kippst du das
Objekt also um X, mit der Oberseite zur Wand, höchstens 30 Grad. Lass die Oberseite der Box bis zu dem
Boden reichen, zu dem die Leiter führt, im Zweifel ein wenig darüber. Das Modell selbst darf massiv sein;
Spieler müssen es nicht berühren.

**Kartenvarianten.** Teile der Karte können in jeder Runde anders ausfallen, damit die Jäger die Karte nicht
auswendig lernen können. Setz eine **Map Variant Group** auf ein leeres Objekt und mach jede Version dieses
Teils zu einem **direkten Kind** davon: ein Gang offen, derselbe Gang versperrt, Kisten auf drei Arten
gestapelt. In jeder Runde schaltet das Spiel EIN Kind ein und die anderen aus; der Server wählt, und alle
sehen dasselbe. **Modes** legt fest, in welchen Modi gewählt wird (voreingestellt: Standard); in den anderen
bleiben die Kinder so, wie du sie in der Szene hinterlassen hast. Der Inspector der Gruppe listet die
Versionen mit je einem Knopf **Zeigen** auf, damit du sie einzeln bauen und prüfen kannst.
- **Gewicht:** Füge einem Kind eine **Map Variant Option** hinzu, um es wahrscheinlicher oder
  unwahrscheinlicher zu machen (ohne sie 1; 0 nie).
- **Avoid Repeat** (standardmäßig an): Die Version der letzten Runde wird nicht erneut gewählt, solange eine
  andere möglich ist.
- **Link Id:** Gruppen mit derselben Id wählen dieselbe Kindnummer, damit zwei Hälften einer Idee
  zusammenpassen: hier eine Tür zu, während sich dort eine Lücke öffnet.
- Gruppen können in den Versionen einer anderen Gruppe liegen.
- Spawnpunkte innerhalb einer Version werden nur in den Runden verwendet, in denen diese Version eingeschaltet
  ist.
- Markiere nichts innerhalb einer Version als **Occluder Static**: Occlusion Culling würde es so behandeln,
  als wäre es immer da, und alles dahinter würde in den Runden verschwinden, in denen es aus ist. Der
  Validator warnt davor.

**Beleuchtung.** Behalte die Lichteinstellungen bei, mit denen die neue Karte gekommen ist: Mixed Lighting im
Modus **Shadowmask**, **Directional** Lightmaps, **Realtime Global Illumination aus**. Das Spiel bringt Shader
nur für dieses Setup mit. Lichter sind **Mixed** oder **Baked**; mehr als vier Echtzeitlichter mit Schatten
bremsen das Spiel aus. Nebel muss, falls du welchen willst, **Exponential Squared** sein. Das **Adaptive Probe Volume**
beleuchtet Spieler und Modellierer so, dass sie zur Karte passen; backe es mit dem Rest (es ist
Teil von Generate Lighting). Backe nach jeder Änderung an Geometrie oder Lichtern neu, sonst zeigt das Spiel
das alte Licht.

**Post-Processing.** Ein **Volume** darf nur Farb- und Toneffekte tragen: Bloom, Tonemapping, Color
Adjustments, White Balance, Lift Gamma Gain, Shadows Midtones Highlights, Split Toning, Channel Mixer,
Color Curves, Vignette.

## Kartenlogik

Du kannst eine Karte interaktiv machen, so wie es Hammer tut: eine Plattform, die sich bewegt, wenn ein Knopf
gedrückt wird, und eine Weile später zurückkommt, ein Sound, eine Zone, die Schaden macht. Es wird kein Code
geschrieben; alles wird im Inspector verbunden, und das Spiel erledigt die Arbeit.

Füge diese über **Add Component > Map Kit > Logic** hinzu:

| Komponente | Was sie tut |
|---|---|
| Map Button | Ein Knopf, den Spieler mit der Interaktionstaste drücken. Braucht einen Collider. Wer ihn drücken darf, eine Abklingzeit, ein Hinweistext. |
| Map Mover | Bewegt und dreht das Objekt samt seinen Kindern entlang einer Liste von Haltepunkten und nimmt die Spieler darauf mit. Return After: kommt so viele Sekunden nach Erreichen des letzten Haltepunkts von selbst zurück. PingPong und Loop laufen von allein. |
| Map Trigger | Eine unsichtbare Box: Ereignisse, wenn ein Spieler hineingeht oder hinausgeht, und für den ersten, der hineingeht, und den letzten, der hinausgeht. |
| Map Teleport | Schickt jeden, der in seine Box läuft, zu einem Zielobjekt, mit Blick in dessen Richtung. |
| Map Hurt Zone | Fügt allen darin in jedem Intervall Schaden zu oder tötet sofort. Für einen Tod, den die Karte verursacht, bekommt niemand Punkte. |
| Map Sound | Ein Sound an der Stelle, an der er steht: einmalig oder in Schleife, mit der Entfernung leiser werdend oder überall gleich laut. |
| Map Effect | Einer der eigenen Effekte des Spiels: Rauch, Dampf, Feuer, Funken, Staub, Tropfen. Farbe, Größe, Menge. |
| Map Toggle | Zeigt oder versteckt das Objekt samt seinen Kindern (was gezeichnet wird, was fest ist, Lichter). |
| Map Relay | Gibt einen Eingang an mehrere Ausgänge weiter, jeden mit seiner eigenen Verzögerung. |
| Map Timer | Löst im aktivierten Zustand nach jedem Intervall ein Ereignis aus. |
| Map Counter | Zählt und löst ein Ereignis aus, wenn der Zählerstand sein Ziel erreicht. |
| Map Round Events | Runde begonnen, Jagd begonnen, Runde beendet. |

**Verbinden.** Jede Komponente hat eine Liste **Ausgänge**: ein *Ereignis* (Pressed, ArrivedAtEnd,
Entered...), ein *Ziel* (eine andere Logikkomponente, hineingezogen), einen *Eingang* (Forward, Back, Play,
Enable, Disable...), eine *Verzögerung* und, wo der Eingang eine nimmt, eine Zahl (ein Haltepunkt, eine
Menge). Der Inspector bietet nur die Ereignisse an, die die Komponente auslöst, und die Eingänge, die ihr
Ziel annimmt, markiert eine Verbindung, die nicht funktionieren kann, in Orange und listet darunter auf, wer
mit der Komponente verbunden ist. **Map Kit > Logic** zeigt jede Verbindung auf einer Seite.

Beispiel: das Ereignis *Pressed* des Knopfes an den Eingang *Forward* des Aufzugs. Der Aufzug hat einen
Haltepunkt (1,8 m nach oben) und Return After 4: drücken, er fährt hoch, vier Sekunden später kommt er
herunter. Der Beispielraum von **New Map** bringt genau das mit, dazu ein Motorgeräusch (das *Started* des
Aufzugs an das *Play* eines Map Sound) und im anderen Raum eine Heizung: Ein Knopf schaltet eine Map Hurt
Zone und einen Feuer-Map-Effect gleichzeitig um.

**Wer es ausgelöst hat.** Ein Ereignis, das ein Spieler verursacht (ein Drücken, das Betreten eines Triggers,
erlittener Schaden), merkt sich diesen Spieler entlang der ganzen Kette, Verzögerungen inklusive. Zwei
Eingänge wirken auf ihn: *HurtActivator* an einer Hurt Zone fügt ihm einmal ihren Schaden zu (oder
stattdessen die Zahl des Ausgangs), und *TeleportActivator* an einem Teleport schickt ihn zu dessen Ziel. Ein
Falltürknopf, der nur den Drückenden nach unten schickt, ein Trigger, der nur den verletzt, der
hineingetreten ist: Verbinde es, keine Box nötig. Ein Teleport, der nur so verwendet wird, sollte deaktiviert
starten, sonst schickt auch seine eigene Box Leute weg.

**Vor dem Packen ansehen.** In der Scene view ist die Linie jedes Ausgangs mit ihrem Ereignis und Eingang
beschriftet. Ein ausgewählter Map Mover hat an jedem Haltepunkt einen Griff und unter Vorschau eine
durchsichtige Kopie von sich, die du den Weg entlang schieben oder so abspielen kannst, wie das Spiel sie
abspielt, mit der Zeit, die die Fahrt dauert.

Zu Beginn jeder Runde wird alles wieder so, wie du es angelegt hast. Eine Komponente mit ausgeschaltetem
Start Enabled tut nichts, bis ein *Enable*-Eingang ankommt.

**Achte darauf.**

- Nichts, was sich bewegt, darf **Static** sein: Static Batching friert es an seinem Platz ein, und sein
  gebackenes Licht bleibt zurück. Was sich bewegt, wird von Probes beleuchtet.
- Eine bewegte Plattform schiebt einen Spieler, der ihr im Weg steht; wenn sie das nicht kann, tut sie, was
  **When Blocked** sagt: umkehren, zerquetschen oder weiterfahren.
- Ein Spieler, der von einer bewegten Plattform springt, behält ihre seitliche Geschwindigkeit, bis er
  landet; ein Sprung auf einer fahrenden Fähre endet also wieder auf der Fähre.
- Das Ziel eines Teleports wird wie ein Spawnpunkt geprüft: nichts Festes dort, ein Boden darunter. Ein Ziel
  innerhalb der eigenen Box eines Teleports schickt den Spieler eine Sekunde später gleich weiter; der
  Validator warnt.
- Sounds: höchstens 2 Minuten pro Stück, Mono oder Stereo, bis 48 kHz; alle Sounds einer Karte zusammen
  höchstens 32 MB im Speicher. Lade lange Sounds mit **Compressed In Memory** oder **Streaming**. Sounds
  laufen über die Effektlautstärke des Spielers.
- Eine Schleife ohne Verzögerung (A löst B aus und B sofort A) wird im Spiel nach 32 Schritten abgebrochen,
  und der Validator wertet sie als Fehler. Gib einem Ausgang in der Schleife eine Verzögerung. Nur Eingänge,
  die ihr Ziel auf der Stelle handeln lassen, schließen eine solche Schleife (ein Drücken, der Auslöser
  eines Relais, die Änderung eines Zählers, ein irgendwohin geschickter Mover): Ein Knopf, der sich selbst
  ausschaltet, oder ein Musikstück, das ein anderes startet, wenn es endet, ist in Ordnung.
- Grenzen: 256 Logikkomponenten, je 16 Ausgänge, 200 Eingänge pro Sekunde für die ganze Karte.

**Ausprobieren.** Pack and Test öffnet die Karte in Übung, wo es weder Jäger noch Modellierer gibt. In der
Entwicklerkonsole kannst du mit `maprole hunter` oder `maprole hider` Rollenfilter ausprobieren, `maplogic`
listet den Zustand jeder Komponente auf, und `mapfire <number> <input>` schickt einen Eingang von Hand.

### Die Beispielkarte: Logic Playground

**Map Kit > Examples > Logic Playground** baut in `Assets/Maps/LogicPlayground/` eine Greybox-Karte, in der
jede Logikkomponente im Einsatz ist. Backe ihr Licht, wenn das Kit danach fragt, und teste sie dann wie jede
Karte mit Pack and Test, oder öffne ihre Objekte, um zu sehen, wie sie verbunden sind (**Map Kit > Logic**
listet jede Verbindung auf). Ihre Farben sagen, was die Dinge sind: Grau ist die Welt, Orange ist etwas zum
Drücken, Gelb bewegt sich, Rot verletzt, Blau teleportiert, Lila erscheint und verschwindet. Die Decke ist
unsichtbar: ein Collider, von dem nichts gezeichnet wird und der den offenen Innenhof für das Backen der
Kartengrenzen schließt.

Neun Räume in einem 3-mal-3-Raster, 36 m breit. Alle starten im mittleren.

| Raum | Was er zeigt |
|---|---|
| 00 Hub (Mitte) | Map Round Events: ein Gong, wenn eine Runde beginnt; eine Sirene und ein rotes Rauchsignal, wenn die Jagd beginnt; Feuerwerk, wenn die Runde endet (ein Relais, das Funken und einen Sound mit Verzögerungen abspielt). Ein Generator, der von Anfang an in Schleife brummt. |
| 01 Doors (Süden) | Eine Schiebetür, die sich vier Sekunden nach dem Öffnen von selbst schließt (Return After), umkehrt, wenn jemand im Weg steht, und summt. Ein Trigger davor, der für Jäger ihren Knopf drückt (Press). Eine Schwingtür, die jeder der beiden Knöpfe auf- oder zudreht (Reverse). |
| 02 Elevator (Norden) | Drei Stockwerke: Rufknöpfe auf jedem, Knöpfe, die in der Kabine mitfahren (Go To Stop mit dem Stockwerk als Zahl), ein Motor beim Anfahren, ein Glockenton auf jedem Stockwerk. |
| 03 Platforms (Osten) | Eine Grube, die jeden tötet, der hineinfällt. Eine Fähre, die darüber hin- und herfährt (PingPong), mit Knöpfen zum Anhalten und Weiterfahren (Pause, Resume); ein Karussell, das sich in der Mitte dreht (Loop) und ein- und ausgeschaltet wird (Toggle). Spring von der Fähre: Du behältst ihre Geschwindigkeit. |
| 04 Hazards (Westen) | Eine Presse (When Blocked: Crush). Feuerdüsen, die zu zufälligen Zeiten zünden: Ein Timer löst ein Relais aus, das drei Hurt Zones und das Feuer einschaltet und anderthalb Sekunden später wieder aus. Ein Dampfrohr, das verbrüht. Ein Hauptschalter, der alles pausiert. |
| 05 Vault (Nordosten) | Drei Runen, jede einmal gezählt (Add, und die Rune schaltet sich selbst aus); bei drei wird der Knopf des Tresors entsperrt. Wird er im gesperrten Zustand gedrückt, summt er. Eine Köderrune nimmt eine zurück (Subtract); der Generalschlüssel der Jäger setzt den Zählerstand sofort (Set Value); ein Reset schließt alles wieder (Reset Count, Lock, Back, Enable). |
| 06 Teleports (Nordwesten) | Zwei Pads, die Spieler zueinander schicken. Ein Raum ohne Tür: Ein Pad bringt dich hinein, ein Knopf drinnen bringt nur dich hinaus (Teleport Activator an einem ausgeschalteten Teleport). Eine Abkürzung für Modellierer zum Vorsprung des Südostraums. |
| 07 Triggers and Toggles (Südosten) | Eine Druckplatte: Solange jemand darauf steht (Occupied, Emptied), gibt es eine Brücke zum Vorsprung. Das Geländer der Brücke ist ein Toggle im Toggle der Brücke. Ein Alarm, der einmal pro Runde klingelt, wenn ein Jäger hereinkommt. Eine versteckte Nische hinter einer Wandplatte. |
| 08 Sounds and Effects (Südwesten) | Jeder Effekt auf einem Sockel mit seinem Knopf. Eine Jukebox mit zwei Stücken, von denen jedes das andere startet, wenn es endet (Finished). Eine Durchsage, die auf der ganzen Karte zu hören ist, einmal pro Runde. Ein Knopf, der nur den verletzt, der ihn drückt (Hurt Activator). |

## Die Regeln

Das Spiel liest eine Karte, bevor es Unity sie öffnen lässt, und lehnt alles ab, was außerhalb dieser Regeln
liegt. **Map Kit > Validate** prüft sie alle im Editor; **Pack and Test** prüft die gepackten Dateien noch
einmal mit dem eigenen Code des Spiels.

- **Komponenten:** Mesh Filter, Mesh Renderer, LOD Group, Box, Sphere, Capsule und Mesh Collider, Light,
  Reflection Probe, Light Probe Group, Occlusion Area und Portal, Adaptive Probe Volume, Volume sowie die
  eigenen Komponenten des Kits (Map Marker, Map Mode Object, Map Bounds Volume, Penetrable Surface, Probe
  Volume Region, Kit Map Descriptor, Map Ladder, Map Variant Group, Map Variant Option) und die Komponenten der Kartenlogik. Keine eigenen Skripte
  und keine Kameras, Audio Sources, Partikelsysteme, UI, Videos, Terrains, Animationen oder Physikkörper.
  Sound und Effekte kommen über Map Sound und Map Effect.
- **Shader:** Universal Render Pipeline Lit, Simple Lit, Unlit, Baked Lit. Himmel: Skybox Procedural,
  Cubemap, 6 Sided oder Panoramic.
- **Layer:** Default, Ignore Raycast, MapBlocker (unsichtbare Wände), GuessMapBlocker.
- **Beleuchtung:** wie oben. Höchstens 64 Lichter.
- **Größe:** höchstens 256 MB für das Paket, 512 MB Texturen, 8192 Pixel an der längsten Seite einer Textur,
  3 Millionen Dreiecke, 6 Millionen Vertices und 10.000 Renderer.
- **Name:** Die ID der Karte ist ihr Szenenname in Kleinbuchstaben: Buchstaben, Ziffern, `-` und `_`, bis zu
  23 Zeichen. Szenennamen, die das Spiel selbst verwendet (Startup, Menu, Game, HunterRoom und andere sowie
  alles, was mit `Map_` oder `GameMode_` beginnt), sind nicht erlaubt.

Fehler bedeuten, dass das Spiel die Karte nicht öffnet. Warnungen sind einen Blick wert, halten sie aber
nicht auf.

## Mit Freunden spielen

Alle in der Sitzung brauchen genau dieselben Kartendateien: Das Spiel vergleicht sie, bevor es die Karte
lädt. Das Paket ist für jedes System gleich, also pack die Karte einmal und schick deinen Freunden den Ordner
aus `Builds/Maps/<id>/` im Kit (als ZIP). Jeder von ihnen legt den Ordner unverändert in den Kartenordner
seines Spiels:

| System | Ordner |
|---|---|
| Windows | `%USERPROFILE%\AppData\LocalLow\DevAdam\MimicBusters\Maps\Local\` |
| macOS | `~/Library/Application Support/DevAdam/MimicBusters/Maps/Local/` |
| Linux | `~/.config/unity3d/DevAdam/MimicBusters/Maps/Local/` |

Das Ergebnis sieht aus wie `...\Maps\Local\<id>\map.json` mit `map.any.bundle` daneben. Die Karte erscheint
dann in der Kartenauswahl des Spiels unter **Community**. Unterstützung für den Steam Workshop kommt später,
direkt aus dem Spiel heraus.

## Wenn etwas schiefgeht

- **Das Kit sagt, das Spiel würde die Karte ablehnen.** Die Gründe stehen im Fenster von Pack and Test. Der
  vollständige Bericht steht in der Console und auch in `Builds/Maps/<id>.report.txt`.
- **Das Spiel sagt, die Karte konnte nicht geöffnet werden.** Schalte im Spiel
  **Einstellungen > Gameplay > Entwicklerkonsole** ein, öffne die Konsole mit der Taste unter Esc und tippe
  `kitmaps`: Es listet deine Karten auf und warum eine davon nicht gespielt werden kann. `kitinspect <id>`
  liest eine Karte, ohne sie zu laden.
- **Die Karte ist zu dunkel oder hat das falsche Licht.** Backe das Licht neu und pack dann neu. Wenn der
  Validator meldet, dass die Szene die Lichtdaten einer anderen Szene verwendet (das passiert bei kopierten
  Szenen), verwende **Map Kit > Tools > Detach Copied Lighting** und backe dann.
- **Spieler fallen durch den Boden oder spawnen in Wänden.** Prüfe die Collider, verschiebe den Spawnpunkt
  und backe die Kartengrenzen neu.
- **Das Spiel meldet, dass die Karte mit einem älteren Kit erstellt wurde.** Lade das neue Kit herunter,
  öffne deine Karte darin und pack sie neu.

## Lizenz

Siehe [LICENSE.md](LICENSE.md). Mimic Busters und sein Logo gehören DevAdam und sind nicht Teil der
Lizenz.

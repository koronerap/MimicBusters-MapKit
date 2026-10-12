# Mimic Busters Map Kit

Crea le tue mappe per **Mimic Busters** in Unity, poi esplorale nel gioco con un clic.

<!-- languages -->
🌐 [English](README.md) · [Türkçe](README.tr.md) · [Deutsch](README.de.md) · [Español](README.es.md) · [Français](README.fr.md) · **Italiano** · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [Українська](README.uk.md) · [Ελληνικά](README.el.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [简体中文](README.zh-Hans.md) · [繁體中文](README.zh-Hant.md) · [العربية](README.ar.md)

Il kit è un progetto Unity con lo stesso codice delle mappe che usa il gioco. Costruisci una mappa come una
normale scena Unity, fai il bake della sua illuminazione in Unity, e il kit la controlla secondo le regole del
gioco, la impacchetta in un unico file che Windows, Linux e macOS aprono allo stesso modo, la installa nel tuo
gioco e avvia il gioco su di essa.

Le mappe create con questo kit si possono giocare in **Standard** (nel gioco Classic: Cacciatori e
Modellatori, comprese le sue varianti Compagno e Infestation, che usano gli stessi marcatori),
**Allenamento** (nel kit Practice: costruzione libera, niente round), **Deathmatch** (tutti armati, nessuna
fazione) e **Indovina l'imitazione** (a turno un giocatore modella e gli altri indovinano).

## Cosa ti serve

- **Unity 6000.3.8f1**, esattamente questa versione, da Unity Hub. Le mappe sono asset bundle di Unity, e il
  gioco può leggere solo i bundle creati dalla sua stessa versione di Unity. Nessun modulo aggiuntivo: il kit
  impacchetta con ciò che arriva insieme all'editor.
- **Mimic Busters**, installato tramite Steam, per provare le tue mappe.

## Per iniziare

1. Scarica il kit: **Code > Download ZIP** su GitHub, oppure uno zip da **Releases**. Estrailo in una cartella
   con un percorso breve.
2. In Unity Hub: **Add > Add project from disk**, scegli la cartella estratta e aprila con 6000.3.8f1. La prima
   importazione richiede qualche minuto.
   Al termine si apre la finestra **Benvenuto**: scegli lì la tua lingua (il kit parla inglese, turco,
   tedesco, spagnolo, francese, italiano, portoghese brasiliano, russo, ucraino, greco, giapponese, coreano e
   cinese) e controlla che abbia trovato il gioco. Puoi riaprirla da **Map Kit > Welcome** e cambiare la lingua
   più tardi in **Map Kit > Settings** o con l'ingranaggio in qualsiasi finestra del kit.
3. **Map Kit > New Map**. Dai un nome alla mappa, spunta le sue modalità e lascia attivo
   **Aggiungi una stanza di esempio** per la tua prima mappa. Il kit crea in `Assets/Maps/<Name>/` una scena con
   tutto ciò che serve al gioco già al suo posto: le info della mappa, i punti di spawn, i confini della mappa,
   un Probe Volume e le impostazioni di illuminazione giuste.
4. **Window > Rendering > Lighting > Generate Lighting**. Il primo bake richiede un po' di tempo.
5. **Map Kit > Validate**. Dovrebbe dire che il gioco aprirà la mappa.
6. **Map Kit > Pack and Test**. La prima volta controlla la sezione **Gioco**: il kit cerca il gioco nelle tue
   librerie di Steam; se si trova altrove, premi **Scegli...** e seleziona l'eseguibile del gioco. Poi premi
   **Impacchetta e prova**. Il gioco si avvia direttamente in una sessione privata di Allenamento sulla tua
   mappa.

Poi rendila tua: elimina la stanza di esempio, porta dentro i tuoi modelli e ripeti i passi da 4 a 6.

## Costruire una mappa

**Geometria.** Importa modelli (FBX, OBJ, file di Blender) e texture come al solito e posizionali nella scena.
Usa gli shader della Universal Render Pipeline: **Lit**, **Simple Lit**, **Unlit** o **Baked Lit**. Dai un
collider a tutto ciò su cui i giocatori camminano o contro cui urtano. Segna la geometria statica come
**Static** perché riceva la luce del bake.

**Info della mappa.** L'oggetto `Map` nella nuova scena porta un **Kit Map Descriptor**: nome, descrizione,
autore e modalità della mappa, così come li mostra il gioco. Deve essercene esattamente uno.

**Punti di spawn.** Un punto di spawn è un oggetto vuoto con un **Map Marker**. Il suo Inspector chiede due
cose, in ordine: **1. Modalità**, quali modalità di gioco lo usano, e **2. Tipi**, a cosa serve. Sotto indica
quale modalità usa il marcatore e come cosa, e avvisa se c'è un tipo che nessuna delle modalità spuntate
legge:

| Tipo | A cosa serve |
|---|---|
| HiderSpawn | Dove i Modellatori iniziano un round (Standard). È anche dove compaiono i giocatori in Allenamento. |
| HunterSpawn | Dove escono i Cacciatori quando inizia la caccia (Standard). Passano la preparazione nella stanza dei Cacciatori del gioco. |
| LobbySpawn | Dove tutti aspettano mentre la lobby si riempie. Facoltativo: senza, la lobby usa gli HiderSpawn. |
| HunterDoor | Facoltativo. Un oggetto che resta chiuso durante la lobby e la preparazione e si apre quando inizia la caccia. Conta solo il primo. |
| ModelerSpawn | Allenamento: dove compaiono i giocatori, come HiderSpawn e LobbySpawn. «Indovina l'imitazione»: dove modella il giocatore di turno. |
| ArenaSpawn | Facoltativo. Una posizione per un duello 1v1 (Standard). La stanza dei Cacciatori del gioco ha le sue. |
| GuessComputer | Facoltativo («Indovina l'imitazione»). Dove il gioco mette la sua scrivania con il computer; il giocatore di turno ci si siede mentre il suo modello sta sul ModelerSpawn. Conta solo il primo. Senza, il giocatore è nascosto dentro il suo modello. |

Standard richiede almeno un HunterSpawn e un HiderSpawn. La modalità Allenamento richiede un HiderSpawn, un
LobbySpawn o un ModelerSpawn con Allenamento spuntato. Deathmatch usa ogni HunterSpawn, HiderSpawn e
LobbySpawn con Deathmatch spuntato e mette un giocatore su quello più lontano da tutti, quindi dagliene almeno
sei, ben distribuiti. La modalità «Indovina l'imitazione» mette chi indovina sui LobbySpawn (sugli HiderSpawn
se non ce ne sono) e il giocatore di turno su un solo ModelerSpawn, tutti con «Indovina l'imitazione» spuntato:
metti quest'ultimo dove tutti possono vederlo. Mettine qualcuno di ciascun tipo, ben distribuiti, sul pavimento, rivolti verso l'interno della mappa. La linea che parte dal marcatore mostra in che direzione
guarda il giocatore. Un marcatore senza modalità spuntate non viene usato da nessuna modalità.

**Confini della mappa.** Il **Map Bounds Volume** è un box attorno all'intera area di gioco. Dopo il bake, il
gioco sa dove possono trovarsi i giocatori e riporta indietro chiunque cada fuori dalla mappa. Fai in modo che
il box copra ogni punto che un giocatore può raggiungere, con spazio sopra per salti e tetti, poi
**Map Kit > Bake Map Bounds**. Rifai il bake ogni volta che sposti muri, pavimenti o punti di spawn. Se il
validatore dice che su una mappa chiusa l'area di gioco arriva alla parete del box, c'è un buco nel punto che
indica.

Il bake riempie l'aria a partire dai tuoi punti di spawn verso l'esterno, in celle di mezzo metro. Una stanza
che i giocatori raggiungono solo con il teletrasporto non viene mai trovata così: aggiungi un punto al suo
interno nei **Seeds** del volume, e uno anche per il resto della mappa, perché quando Seeds è compilato il
riempimento parte solo da quei punti. Il validatore lo segnala se un punto di spawn o la destinazione di un
teletrasporto finisce fuori. Un varco largo una sola cella (una fessura sopra un muro, una crepa stretta
accanto a una piattaforma) viene segnalato come possibile perdita: fai i varchi o ben chiusi o larghi almeno
un metro.

**Scale.** Una **Map Ladder** è un box in cui i giocatori si arrampicano finché ci sono dentro, come si sale
una scala in Half-Life e Counter-Strike: andare verso la scala fa salire, guardare in alto o in basso decide
la direzione, i tasti laterali spostano lungo la scala, il salto fa staccare. Aggiungila a un oggetto vuoto (o
al modello della scala) e dimensiona il box nella Scene view con le sue maniglie. Il box è lo spazio in cui
sta un giocatore che si arrampica: i pioli e l'aria davanti a essi. La sua
**freccia blu (Z) è la direzione in cui guarda chi sale**, verso la scala. La salita segue l'alto
dell'oggetto, quindi per una scala appoggiata a un muro inclina l'oggetto sull'asse X, con la cima verso il muro, al massimo di 30 gradi. Fai arrivare la
cima del box al pavimento a cui porta la scala, semmai un po' più in alto. Il modello stesso può essere
solido; i giocatori non hanno bisogno di toccarlo.

**Varianti della mappa.** Alcune parti della mappa possono cambiare a ogni round, così i Cacciatori non
possono impararla a memoria. Metti un **Map Variant Group** su un oggetto vuoto e rendi ogni versione di quella
parte un suo **figlio diretto**: un corridoio aperto, lo stesso corridoio bloccato, casse impilate in tre
modi. A ogni round il gioco attiva UNO SOLO dei figli e disattiva gli altri; sceglie il server e tutti vedono
la stessa cosa. **Modes** indica in quali modalità avviene la scelta (Standard per impostazione predefinita);
nelle altre i figli restano come li hai lasciati nella scena. L'Inspector del gruppo elenca le versioni,
ognuna con un pulsante **Mostra**, per costruirle e controllarle una alla volta.
- **Peso:** aggiungi una **Map Variant Option** a un figlio per renderlo più o meno probabile (1 senza; 0
  mai).
- **Avoid Repeat** (attivo per impostazione predefinita): la versione del round precedente non viene scelta di
  nuovo finché ce n'è un'altra possibile.
- **Link Id:** i gruppi con lo stesso id scelgono lo stesso numero di figlio, così le due metà di un'idea
  vanno d'accordo: una porta chiusa qui mentre là si apre un varco.
- I gruppi possono stare dentro le versioni di un altro gruppo.
- I punti di spawn dentro una versione vengono usati solo nei round in cui quella versione è attiva.
- Non segnare nulla dentro una versione come **Occluder Static**: l'occlusion culling lo tratterebbe come
  sempre presente, e ciò che sta dietro sparirebbe nei round in cui è disattivato. Il validatore ti avvisa.

**Illuminazione.** Mantieni le impostazioni di illuminazione con cui è arrivata la nuova mappa: illuminazione
Mixed in modalità **Shadowmask**, lightmap **Directional**, **Realtime Global Illumination disattivata**. Il
gioco include gli shader solo per questa configurazione. Le luci sono **Mixed** o **Baked**; più di quattro
luci in tempo reale con ombre rallentano il gioco. La nebbia, se la vuoi, deve essere
**Exponential Squared**. L'**Adaptive Probe Volume** illumina i giocatori e i Modellatori in modo che si
intonino alla mappa; fanne il bake insieme al resto (fa parte di Generate Lighting). Rifai il bake dopo ogni
modifica alla geometria o alle luci, altrimenti il gioco mostra la luce vecchia.

**Post-processing.** Un **Volume** può avere solo effetti di colore e di tono: Bloom, Tonemapping, Color
Adjustments, White Balance, Lift Gamma Gain, Shadows Midtones Highlights, Split Toning, Channel Mixer, Color
Curves, Vignette.

## Logica della mappa

Puoi rendere interattiva una mappa come si fa con Hammer: una piattaforma che si muove quando si preme un
pulsante e torna indietro dopo un po', un suono, una zona che fa danno. Non si scrive codice; tutto si collega
nell'Inspector, e il lavoro lo fa il gioco.

Aggiungili da **Add Component > Map Kit > Logic**:

| Componente | Cosa fa |
|---|---|
| Map Button | Un pulsante che i giocatori premono con il tasto di interazione. Serve un collider. Chi può premerlo, un tempo di ricarica, un messaggio. |
| Map Mover | Muove e ruota l'oggetto e i suoi figli lungo una lista di fermate, trasportando i giocatori che ci stanno sopra. Return After: torna da solo quel numero di secondi dopo aver raggiunto l'ultima fermata. PingPong e Loop vanno avanti da soli. |
| Map Trigger | Un box invisibile: eventi quando un giocatore entra o esce, e per il primo che entra e l'ultimo che esce. |
| Map Teleport | Manda chiunque entri nel suo box verso un oggetto di destinazione, rivolto dove guarda quell'oggetto. |
| Map Hurt Zone | Infligge danno a chiunque sia dentro a ogni intervallo, oppure uccide subito. Nessuno fa punti per una morte causata dalla mappa. |
| Map Sound | Un suono nel punto in cui si trova: una volta o in loop, che si attenua con la distanza o uguale ovunque. |
| Map Effect | Uno degli effetti del gioco: fumo, vapore, fuoco, scintille, polvere, gocce. Colore, dimensione, quantità. |
| Map Toggle | Mostra o nasconde l'oggetto e i suoi figli (ciò che viene disegnato, ciò che è solido, le luci). |
| Map Relay | Passa un ingresso a più uscite, ognuna con il proprio ritardo. |
| Map Timer | Genera un evento a ogni intervallo finché è attivo. |
| Map Counter | Conta e genera un evento quando il conteggio raggiunge il suo obiettivo. |
| Map Round Events | Round iniziato, caccia iniziata, round finito. |

**Collegamenti.** Ogni componente ha una lista di **Uscite**: un *evento* (Pressed, ArrivedAtEnd, Entered...),
un *destinatario* (un altro componente di logica, trascinato dentro), un *ingresso* (Forward, Back, Play,
Enable, Disable...), un *ritardo* e, quando l'ingresso lo prevede, un numero (una fermata, una quantità).
L'Inspector propone solo gli eventi che il componente genera e gli ingressi che il suo destinatario accetta,
segna in arancione un collegamento che non può funzionare, ed elenca sotto chi è collegato al componente.
**Map Kit > Logic** mostra tutti i collegamenti in una sola pagina.

Esempio: l'evento *Pressed* del pulsante verso l'ingresso *Forward* dell'ascensore. L'ascensore ha una fermata
(1,8 m più in alto) e Return After 4: premi, sale, quattro secondi dopo scende. La stanza di esempio di
**New Map** arriva proprio con questo, più il rumore di un motore (lo *Started* dell'ascensore verso il *Play*
di un Map Sound) e, nell'altra stanza, una stufa: un solo pulsante accende e spegne insieme una Map Hurt Zone e
un Map Effect di fuoco.

**Chi l'ha fatto scattare.** Un evento causato da un giocatore (una pressione, l'ingresso in un trigger, un
danno subito) si ricorda di quel giocatore lungo tutta la catena, ritardi compresi. Due ingressi agiscono su
di lui: *HurtActivator* su una zona di danno gli infligge il suo danno una volta (o invece il numero
dell'uscita), e *TeleportActivator* su un teletrasporto lo manda alla sua destinazione. Un pulsante di botola
che fa cadere solo chi lo preme, un trigger che ferisce solo chi ci è entrato: basta collegarlo, nessun box
necessario. Un teletrasporto usato solo in questo modo dovrebbe partire disattivato, altrimenti anche il suo
box manda via le persone.

**Vederlo prima di impacchettare.** Nella Scene view la linea di ogni uscita è etichettata con il suo evento e
il suo ingresso. Un Map Mover selezionato ha una maniglia su ogni fermata e, sotto Anteprima, una sua copia
trasparente che puoi far scorrere lungo il percorso o riprodurre come la riproduce il gioco, con il tempo che
impiega il tragitto.

All'inizio di ogni round tutto torna come l'hai scritto. Un componente con Start Enabled disattivato non fa
nulla finché non arriva un ingresso *Enable*.

**Fai attenzione a questo.**

- Niente di ciò che si muove può essere **Static**: lo static batching lo blocca sul posto e la sua luce del
  bake resta indietro. Ciò che si muove viene illuminato dalle probe.
- Una piattaforma in movimento spinge un giocatore che si trova sulla sua strada; quando non ci riesce, fa ciò
  che dice **When Blocked**: torna indietro, schiaccia o prosegue.
- Un giocatore che salta da una piattaforma in movimento mantiene la sua velocità laterale finché non atterra,
  quindi un salto su un traghetto in movimento ricade sul traghetto.
- La destinazione di un teletrasporto viene controllata come un punto di spawn: niente di solido lì, un
  pavimento sotto. Una destinazione dentro il box dello stesso teletrasporto rimanda via il giocatore un
  secondo dopo; il validatore ti avvisa.
- Suoni: al massimo 2 minuti ciascuno, mono o stereo, fino a 48 kHz; tutti i suoni di una mappa al massimo
  32 MB in memoria. Carica quelli lunghi con **Compressed In Memory** o **Streaming**. I suoni passano per il
  Volume effetti del giocatore.
- Un loop senza ritardo (A fa scattare B e B fa scattare A all'istante) viene interrotto a 32 passi nel gioco,
  e il validatore lo conta come errore. Dai un ritardo a un'uscita del loop. Chiudono un loop del genere solo
  gli ingressi che fanno agire il loro destinatario all'istante (una pressione, lo scatto di un relè, il
  cambio di un contatore, un mover mandato da qualche parte): un pulsante che si spegne da solo, o un brano che
  ne avvia un altro quando finisce, va bene.
- Limiti: 256 componenti di logica, 16 uscite ciascuno, 200 ingressi al secondo per tutta la mappa.

**Provarlo.** Pack and Test apre la mappa in Allenamento, dove non ci sono né Cacciatori né Modellatori. Nella
console sviluppatore, `maprole hunter` o `maprole hider` ti permette di provare i filtri per ruolo, `maplogic`
elenca lo stato di ogni componente e `mapfire <number> <input>` invia un ingresso a mano.

### La mappa di esempio: Logic Playground

**Map Kit > Examples > Logic Playground** costruisce in `Assets/Maps/LogicPlayground/` una mappa greybox in
cui ogni componente di logica è all'opera. Fai il bake della sua illuminazione quando il kit te lo chiede, poi
provala con Pack and Test come qualsiasi mappa, o apri i suoi oggetti per vedere come sono collegati
(**Map Kit > Logic** elenca tutti i collegamenti). I suoi colori dicono cosa sono le cose: il grigio è il
mondo, l'arancione è qualcosa da premere, il giallo si muove, il rosso ferisce, il blu teletrasporta, il viola
appare e scompare. Il soffitto è invisibile: un collider senza nulla di disegnato, che chiude il cortile
aperto per il bake dei confini della mappa.

Nove stanze su una griglia 3 per 3, larga 36 m. Tutti iniziano in quella centrale.

| Stanza | Cosa mostra |
|---|---|
| 00 Hub (centro) | Map Round Events: un rintocco quando inizia un round; una sirena e un segnale di fumo rosso quando inizia la caccia; fuochi d'artificio quando il round finisce (un relè che riproduce scintille e un suono con dei ritardi). Un generatore che ronza in loop fin dall'inizio. |
| 01 Doors (sud) | Una porta scorrevole che si chiude da sola quattro secondi dopo essersi aperta (Return After), torna indietro se qualcuno è in mezzo, e fa un ronzio. Un trigger davanti che preme il suo pulsante per i Cacciatori (Press). Una porta a battente che ciascuno dei due pulsanti apre o chiude (Reverse). |
| 02 Elevator (nord) | Tre piani: pulsanti di chiamata su ciascuno, pulsanti che viaggiano con la cabina (Go To Stop con il piano come numero), un motore alla partenza, un rintocco a ogni piano. |
| 03 Platforms (est) | Una fossa che uccide chi ci cade. Un traghetto che va avanti e indietro sopra di essa (PingPong) con pulsanti per fermarlo e farlo ripartire (Pause, Resume); una giostra che gira al centro (Loop), accesa e spenta (Toggle). Salta giù dal traghetto: mantieni la sua velocità. |
| 04 Hazards (ovest) | Una pressa (When Blocked: Crush). Bocchette di fuoco che si accendono in momenti casuali: un timer fa scattare un relè che accende tre zone di danno e il fuoco, e li spegne di nuovo un secondo e mezzo dopo. Un tubo del vapore che ustiona. Un interruttore generale che mette tutto in pausa. |
| 05 Vault (nord-est) | Tre rune, ognuna contata una sola volta (Add, e la runa si disattiva da sola); a tre, il pulsante del caveau si sblocca. Se lo premi mentre è bloccato, fa un ronzio. Una runa esca ne toglie una (Subtract); il passe-partout dei Cacciatori imposta il conteggio in un colpo (Set Value); un reset richiude tutto (Reset Count, Lock, Back, Enable). |
| 06 Teleports (nord-ovest) | Due pedane che mandano i giocatori l'una verso l'altra. Una stanza senza porta: una pedana ti porta dentro, un pulsante all'interno porta fuori solo te (Teleport Activator su un teletrasporto disattivato). Una scorciatoia per i Modellatori verso la sporgenza della stanza a sud-est. |
| 07 Triggers and Toggles (sud-est) | Una piastra a pressione: finché qualcuno ci sta sopra (Occupied, Emptied) c'è un ponte verso la sporgenza. La ringhiera del ponte è un toggle dentro il toggle del ponte. Un allarme che suona una volta per round quando entra un Cacciatore. Un angolo nascosto dietro un pannello. |
| 08 Sounds and Effects (sud-ovest) | Ogni effetto su un piedistallo con il suo pulsante. Un jukebox di due brani, ognuno dei quali avvia l'altro quando finisce (Finished). Un annuncio che si sente in tutta la mappa, una volta per round. Un pulsante che ferisce solo chi lo preme (Hurt Activator). |

## Le regole

Il gioco legge una mappa prima di lasciare che Unity la apra, e rifiuta tutto ciò che esce da queste regole.
**Map Kit > Validate** le controlla tutte nell'editor; **Pack and Test** ricontrolla i file impacchettati con il
codice stesso del gioco.

- **Componenti:** Mesh Filter, Mesh Renderer, LOD Group, Box, Sphere, Capsule e Mesh Collider, Light,
  Reflection Probe, Light Probe Group, Occlusion Area e Portal, Adaptive Probe Volume, Volume, i componenti
  propri del kit (Map Marker, Map Mode Object, Map Bounds Volume, Penetrable Surface, Probe Volume Region, Kit
  Map Descriptor, Map Ladder, Map Variant Group, Map Variant Option) e i componenti della logica della mappa. Niente script tuoi, e niente
  telecamere, audio source, sistemi di particelle, UI, video, terrain, animazioni o corpi fisici. Suoni ed
  effetti arrivano tramite Map Sound e Map Effect.
- **Shader:** Universal Render Pipeline Lit, Simple Lit, Unlit, Baked Lit. Cielo: Skybox Procedural, Cubemap,
  6 Sided o Panoramic.
- **Layer:** Default, Ignore Raycast, MapBlocker (muri invisibili), GuessMapBlocker.
- **Illuminazione:** come sopra. Al massimo 64 luci.
- **Dimensioni:** al massimo 256 MB per il pacchetto, 512 MB di texture, 8192 pixel sul lato più lungo di una
  texture, 3 milioni di triangoli, 6 milioni di vertici e 10.000 renderer.
- **Nome:** l'id della mappa è il nome della sua scena in minuscolo: lettere, cifre, `-` e `_`, fino a 23
  caratteri. I nomi di scena che usa il gioco stesso (Startup, Menu, Game, HunterRoom e altri, e qualsiasi nome
  che inizi con `Map_` o `GameMode_`) non sono ammessi.

Gli errori significano che il gioco non aprirà la mappa. Gli avvisi meritano un'occhiata ma non la bloccano.

## Giocare con gli amici

Tutti nella sessione devono avere esattamente gli stessi file della mappa: il gioco li confronta prima di
caricarla. Il pacchetto è lo stesso per ogni sistema, quindi impacchetta la mappa una volta e manda ai tuoi
amici la cartella `Builds/Maps/<id>/` del kit (compressa in zip). Ognuno di loro mette la cartella, senza
modificarla, nella cartella delle mappe del proprio gioco:

| Sistema | Cartella |
|---|---|
| Windows | `%USERPROFILE%\AppData\LocalLow\DevAdam\MimicBusters\Maps\Local\` |
| macOS | `~/Library/Application Support/DevAdam/MimicBusters/Maps/Local/` |
| Linux | `~/.config/unity3d/DevAdam/MimicBusters/Maps/Local/` |

Il risultato è `...\Maps\Local\<id>\map.json` con accanto `map.any.bundle`. La mappa compare poi nella scelta
della mappa del gioco, sotto **Community**. Il supporto al Workshop di Steam arriverà più avanti, dall'interno
del gioco.

## Se qualcosa va storto

- **Il kit dice che il gioco rifiuterebbe la mappa.** I motivi sono elencati nella finestra di Pack and Test.
  Il report completo è nella Console, e anche in `Builds/Maps/<id>.report.txt`.
- **Il gioco dice che non è stato possibile aprire la mappa.** Nel gioco attiva
  **Impostazioni > Gioco > Console sviluppatore**, apri la console con il tasto sotto Esc e scrivi `kitmaps`:
  elenca le tue mappe e il motivo per cui una non si può giocare. `kitinspect <id>` legge una mappa senza
  caricarla.
- **La mappa è troppo scura o ha la luce sbagliata.** Rifai il bake dell'illuminazione, poi impacchetta di
  nuovo. Se il validatore dice che la scena usa i dati di illuminazione di un'altra scena (succede con le scene
  copiate), usa **Map Kit > Tools > Detach Copied Lighting**, poi fai il bake.
- **I giocatori cadono attraverso il pavimento o compaiono dentro i muri.** Controlla i collider, sposta il
  punto di spawn e rifai il bake dei confini della mappa.
- **Il gioco segnala che la mappa è stata creata con un kit più vecchio.** Scarica il nuovo kit, apri la tua
  mappa al suo interno e impacchettala di nuovo.

## Licenza

Vedi [LICENSE.md](LICENSE.md). Mimic Busters e il suo logo appartengono a DevAdam e non fanno parte della
licenza.

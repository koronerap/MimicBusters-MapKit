# Mimic Busters Map Kit

Crea tus propios mapas para **Mimic Busters** en Unity y luego recórrelos en el juego con un solo clic.

<!-- languages -->
🌐 [English](README.md) · [Türkçe](README.tr.md) · [Deutsch](README.de.md) · **Español** · [Français](README.fr.md) · [Italiano](README.it.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [Українська](README.uk.md) · [Ελληνικά](README.el.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [简体中文](README.zh-Hans.md) · [繁體中文](README.zh-Hant.md) · [العربية](README.ar.md)

El kit es un proyecto de Unity con el mismo código de mapas que usa el juego. Construyes el mapa como una escena
normal de Unity, haces el bake de su iluminación en Unity, y el kit lo revisa según las reglas del juego, lo
empaqueta en un solo archivo que abren Windows, Linux y macOS, lo instala en tu juego e inicia el juego en él.

Los mapas hechos con este kit se pueden jugar en **Standard** (Classic en el juego: Cazadores y Modeladores, y sus
variantes Companion e Infestation, que usan los mismos marcadores), **Práctica** (construcción libre, sin rondas),
**Deathmatch** (todos armados, sin bandos) y **Adivina la imitación** (por turnos, un jugador modela y los demás
adivinan).

## Qué necesitas

- **Unity 6000.3.8f1**, exactamente esta versión, desde Unity Hub. Los mapas son asset bundles de Unity, y el juego
  solo puede leer bundles hechos con su propia versión de Unity. No hacen falta módulos extra: el kit empaqueta con
  lo que trae el editor.
- **Mimic Busters**, instalado desde Steam, para probar tus mapas.

## Primeros pasos

1. Descarga el kit: **Code > Download ZIP** en GitHub, o un zip de **Releases**. Descomprímelo en algún sitio con
   una ruta corta.
2. En Unity Hub: **Add > Add project from disk**, elige la carpeta descomprimida y ábrela con 6000.3.8f1. La
   primera importación tarda unos minutos.
   Cuando termine, se abre la ventana de **Bienvenida**: elige allí tu idioma (el kit habla inglés, turco, alemán,
   español, francés, italiano, portugués de Brasil, ruso, ucraniano, griego, japonés, coreano y chino) y comprueba
   que haya encontrado el juego. Puedes volver a abrirla desde **Map Kit > Welcome**, y cambiar el idioma más
   tarde en **Map Kit > Settings** o con el engranaje de cualquier ventana del kit.
3. **Map Kit > New Map**. Ponle un nombre al mapa, marca sus modos y deja activado **Añadir una sala de ejemplo**
   en tu primer mapa. El kit crea una escena en `Assets/Maps/<Name>/` con todo lo que el juego necesita ya en su
   sitio: la información del mapa, los puntos de aparición, los límites del mapa, un probe volume y los ajustes de
   iluminación correctos.
4. **Window > Rendering > Lighting > Generate Lighting**. El primer bake tarda un rato.
5. **Map Kit > Validate**. Debería decir «El juego abrirá este mapa.»
6. **Map Kit > Pack and Test**. La primera vez, revisa la sección **Juego**: el kit busca el juego en tus
   bibliotecas de Steam; si está en otro sitio, pulsa **Elegir...** y selecciona el ejecutable del juego.
   Después pulsa **Empaquetar y probar**. El juego arranca directamente en una sesión privada de Práctica en tu
   mapa.

Luego hazlo tuyo: borra la sala de ejemplo, trae tus propios modelos y repite los pasos 4 a 6.

## Construir un mapa

**Geometría.** Importa modelos (FBX, OBJ, archivos de Blender) y texturas como siempre y colócalos en la escena.
Usa los shaders de Universal Render Pipeline: **Lit**, **Simple Lit**, **Unlit** o **Baked Lit**. Ponle un collider
a todo lo que los jugadores pisan o con lo que chocan. Marca la geometría estática como **Static** para que reciba
la luz del bake.

**Información del mapa.** El objeto `Map` de la escena nueva lleva un **Kit Map Descriptor**: el nombre, la
descripción, el autor y los modos del mapa, tal como los muestra el juego. Tiene que haber exactamente uno.

**Puntos de aparición.** Un punto de aparición es un objeto vacío con un **Map Marker**. Su Inspector pregunta dos
cosas, en orden: **1. Modos**, qué modos de juego lo usan, y **2. Tipos**, para qué sirve. Debajo indica qué modo
usa el marcador y como qué, y avisa si hay un tipo que no lee ninguno de los modos marcados:

| Tipo | Para qué sirve |
|---|---|
| HiderSpawn | Donde empiezan la ronda los Modeladores (Standard). También donde aparecen los jugadores en Práctica. |
| HunterSpawn | Donde salen los Cazadores cuando empieza la caza (Standard). Pasan el tiempo de preparación en la habitación de los Cazadores del propio juego. |
| LobbySpawn | Donde todos esperan mientras se llena la sala. Opcional: sin él, la sala usa los HiderSpawn. |
| HunterDoor | Opcional. Un objeto que se queda cerrado mientras se espera en la sala y durante la preparación, y se abre cuando empieza la caza. Solo cuenta el primero. |
| ModelerSpawn | Práctica: donde aparecen los jugadores, como HiderSpawn y LobbySpawn. Adivina la imitación: donde modela el jugador al que le toca. |
| ArenaSpawn | Opcional. Una posición de duelo 1v1 (Standard). La habitación de los Cazadores del juego tiene la suya. |
| GuessComputer | Opcional (Adivina la imitación). Donde el juego coloca su escritorio con ordenador; el jugador al que le toca se sienta en él mientras su modelo está sobre el ModelerSpawn. Solo cuenta el primero. Sin él, el jugador queda oculto dentro de su modelo. |

Standard necesita al menos un HunterSpawn y un HiderSpawn. Práctica necesita un HiderSpawn, LobbySpawn o
ModelerSpawn con Práctica marcado. Deathmatch usa todos los HunterSpawn, HiderSpawn y LobbySpawn con Deathmatch
marcado y pone a cada jugador en el que está más lejos de todos, así que ponle al menos seis, repartidos. Adivina la
imitación coloca a los que adivinan en los LobbySpawn (en los HiderSpawn si no hay ninguno) y al jugador al que le
toca en un ModelerSpawn, todos con Adivina la imitación marcado: pon ese donde todos puedan verlo. Pon unos cuantos de cada uno, repartidos, sobre el suelo y mirando hacia el mapa. La línea que sale
del marcador muestra hacia dónde mira el jugador. Un marcador sin ningún modo marcado no lo usa ningún modo.

**Límites del mapa.** El **Map Bounds Volume** es una caja alrededor de toda la zona de juego. Una vez hecho su
bake, el juego sabe dónde pueden estar los jugadores y devuelve al mapa a quien se caiga de él. Haz que la caja
cubra todos los sitios a los que puede llegar un jugador, con espacio por encima para saltos y tejados, y luego
**Map Kit > Bake Map Bounds**. Vuelve a hacer el bake cada vez que muevas paredes, suelos o puntos de aparición. Si
el validador dice que la zona de juego llega a la pared de la caja en un mapa cerrado, hay un agujero donde indica.

El bake llena el aire desde tus puntos de aparición hacia fuera, en celdas de medio metro. Una sala a la que los
jugadores solo llegan por teletransporte nunca se encuentra así: añade un punto dentro de ella a los **Seeds** del
volumen, y otro para el resto del mapa, porque con Seeds rellenado el llenado empieza solo desde esos puntos. El
validador te avisa si un punto de aparición o el destino de un teletransporte queda fuera. Un hueco de una sola
celda de ancho (una rendija sobre una pared, una grieta estrecha junto a una plataforma) se marca como posible
fuga: haz los huecos o bien ajustados o bien de al menos un metro.

**Escaleras.** Un **Map Ladder** es una caja por la que los jugadores trepan mientras están dentro, como en
Half-Life y Counter-Strike: avanzar hacia la escalera la sube, mirar arriba o abajo decide la dirección, las teclas
laterales te desplazan a lo largo de ella y saltar la suelta. Añádelo a un objeto vacío (o al modelo de la
escalera) y ajusta el tamaño de la caja en la vista Scene con sus tiradores. La caja es el espacio donde está el
jugador que trepa: los peldaños y el aire que hay delante. Su **flecha azul (Z) indica hacia dónde mira quien
trepa**, hacia la escalera. La subida sigue el eje hacia arriba del objeto, así que para una escalera apoyada en
una pared inclina el objeto en X, con la parte de arriba hacia la pared, 30 grados como máximo. Haz que la parte de
arriba de la caja llegue hasta el suelo al que lleva la escalera, o un poco por encima si acaso. El modelo en sí
puede ser sólido; los jugadores no necesitan tocarlo.

**Variantes del mapa.** Algunas partes del mapa pueden salir distintas en cada ronda, para que los Cazadores no
puedan aprenderse el mapa de memoria. Pon un **Map Variant Group** en un objeto vacío y haz que cada versión de esa
parte sea un **hijo directo** suyo: un pasillo abierto, el mismo pasillo bloqueado, cajas apiladas de tres maneras.
En cada ronda el juego activa UN hijo y desactiva los demás; el servidor elige y todos ven lo mismo. **Modes**
indica en qué modos se elige (Standard por defecto); en los demás, los hijos se quedan como los dejaste en la
escena. El Inspector del grupo lista las versiones con un botón **Mostrar** en cada una, para construirlas y
revisarlas de una en una.
- **Peso:** añade un **Map Variant Option** a un hijo para que salga más o menos a menudo (1 sin él; 0 nunca).
- **Avoid Repeat** (activado por defecto): la versión de la ronda anterior no se vuelve a elegir mientras pueda
  salir otra.
- **Link Id:** los grupos con el mismo ID eligen el mismo número de hijo, para que las dos mitades de una idea
  coincidan: una puerta cerrada aquí mientras se abre un hueco allí.
- Los grupos pueden estar dentro de las versiones de otro grupo.
- Los puntos de aparición dentro de una versión solo se usan en las rondas en que esa versión está activa.
- No marques nada dentro de una versión como **Occluder Static**: el occlusion culling lo trataría como si
  siempre estuviera ahí, y lo que haya detrás desaparecería en las rondas en que está desactivado. El validador
  avisa de ello.

**Iluminación.** Mantén los ajustes de iluminación con los que vino el mapa nuevo: iluminación Mixed en modo
**Shadowmask**, lightmaps **Directional** y **Realtime Global Illumination desactivada**. El juego incluye shaders
solo para esta configuración. Las luces son **Mixed** o **Baked**; más de cuatro luces en tiempo real con sombras
ralentizan el juego. La niebla, si la quieres, debe ser **Exponential Squared**. El **Adaptive Probe Volume**
ilumina a los jugadores y a los Modeladores para que encajen con el mapa; haz su bake con todo lo demás (forma parte
de Generate Lighting). Vuelve a hacer el bake después de cada cambio en la geometría o en las luces; si no, el
juego muestra la luz antigua.

**Posprocesado.** Un **Volume** solo puede llevar efectos de color y tono: Bloom, Tonemapping, Color
Adjustments, White Balance, Lift Gamma Gain, Shadows Midtones Highlights, Split Toning, Channel Mixer,
Color Curves, Vignette.

## Lógica del mapa

Puedes hacer un mapa interactivo como en Hammer: una plataforma que se mueve al pulsar un botón y vuelve un rato
después, un sonido, una zona que hace daño. No se escribe código; todo se conecta en el Inspector y el juego hace
el trabajo.

Añade estos desde **Add Component > Map Kit > Logic**:

| Componente | Qué hace |
|---|---|
| Map Button | Un botón que los jugadores pulsan con la tecla de interactuar. Necesita un collider. Quién puede pulsarlo, un tiempo de espera, un texto de aviso. |
| Map Mover | Mueve y gira el objeto y sus hijos a lo largo de una lista de paradas, llevando a los jugadores que estén encima. Return After: vuelve solo esa cantidad de segundos después de llegar a la última parada. PingPong y Loop funcionan por su cuenta. |
| Map Trigger | Una caja invisible: eventos cuando un jugador entra o sale, y para el primero en entrar y el último en salir. |
| Map Teleport | Envía a quien entre en su caja a un objeto de destino, mirando hacia donde mira ese objeto. |
| Map Hurt Zone | Hace daño a quien esté dentro en cada intervalo, o mata al instante. Nadie puntúa por una muerte que causa el mapa. |
| Map Sound | Un sonido donde está: una vez o en bucle, atenuándose con la distancia o igual en todas partes. |
| Map Effect | Uno de los efectos propios del juego: humo, vapor, fuego, chispas, polvo, goteo. Color, tamaño, cantidad. |
| Map Toggle | Muestra u oculta el objeto y sus hijos (lo que se dibuja, lo que es sólido, las luces). |
| Map Relay | Pasa una entrada a varias salidas, cada una con su propio retardo. |
| Map Timer | Genera un evento en cada intervalo mientras está habilitado. |
| Map Counter | Cuenta y genera un evento cuando la cuenta llega a su objetivo. |
| Map Round Events | Empezó la ronda, empezó la caza, terminó la ronda. |

**Conexiones.** Cada componente tiene una lista de **Salidas**: un *evento* (Pressed, ArrivedAtEnd, Entered...), un
*objetivo* (otro componente de lógica, arrastrado), una *entrada* (Forward, Back, Play, Enable, Disable...), un
*retardo* y, cuando la entrada lo admite, un número (una parada, una cantidad). El Inspector solo ofrece los
eventos que genera el componente y las entradas que acepta su objetivo, marca en naranja una conexión que no puede
funcionar y lista debajo quién está conectado al componente. **Map Kit > Logic** muestra todas las conexiones en
una sola página.

Ejemplo: el evento *Pressed* del botón a la entrada *Forward* del ascensor. El ascensor tiene una parada (1,8 m
hacia arriba) y Return After 4: pulsas, sube, y cuatro segundos después baja. La sala de ejemplo de **New Map**
trae exactamente esto, más un sonido de motor (el *Started* del ascensor al *Play* de un Map Sound) y, en la otra
sala, un calefactor: un botón activa y desactiva a la vez un Map Hurt Zone y un Map Effect de fuego.

**Quien lo puso en marcha.** Un evento causado por un jugador (una pulsación, entrar en un activador, recibir daño)
recuerda a ese jugador a lo largo de toda la cadena, retardos incluidos. Dos entradas actúan sobre él:
*HurtActivator* en una zona de daño le aplica su daño una vez (o el número de la salida en su lugar), y
*TeleportActivator* en un teletransporte lo envía a su destino. Un botón de trampilla que solo tira abajo a quien
lo pulsa, un activador que solo hace daño a quien lo pisó: conéctalo, no hace falta ninguna caja. Un teletransporte
que se use solo así debería empezar deshabilitado; si no, su propia caja también enviará a la gente.

**Verlo antes de empaquetar.** En la vista Scene, la línea de cada salida lleva el nombre de su evento y su entrada.
Un Map Mover seleccionado tiene un tirador en cada parada y, en Vista previa, una copia semitransparente que puedes
deslizar por el recorrido o reproducir como lo hace el juego, con el tiempo que tarda el trayecto.

Al empezar cada ronda, todo vuelve a como lo configuraste. Un componente con Start Enabled desactivado no hace nada
hasta que llega una entrada *Enable*.

**Ten esto en cuenta.**

- Nada que se mueva puede ser **Static**: el static batching lo congela en su sitio y su luz del bake se queda
  atrás. Lo que se mueve lo iluminan las probes.
- Una plataforma móvil empuja al jugador que esté en su camino; cuando no puede, hace lo que diga **When Blocked**:
  da la vuelta, aplasta o sigue adelante.
- Un jugador que salta desde una plataforma móvil conserva su velocidad lateral hasta que aterriza, así que un
  salto sobre un transbordador en marcha cae sobre el transbordador.
- El destino de un teletransporte se revisa como un punto de aparición: nada sólido ahí y un suelo debajo. Uno que
  esté dentro de la propia caja de un teletransporte vuelve a enviar al jugador un segundo después; el validador
  avisa.
- Sonidos: como máximo 2 minutos cada uno, mono o estéreo, hasta 48 kHz; todos los sonidos de un mapa, como máximo
  32 MB en memoria. Carga los largos como **Compressed In Memory** o **Streaming**. Los sonidos pasan por el
  volumen de efectos del jugador.
- Un bucle sin retardo (A activa B y B activa A al instante) se corta a los 32 pasos en el juego, y el validador lo
  cuenta como error. Ponle un retardo a una salida del bucle. Solo cierran un bucle así las entradas que hacen
  actuar a su objetivo en el acto (una pulsación, el disparo de un relé, el cambio de un contador, un mover enviado
  a algún sitio): un botón que se desactiva a sí mismo, o una melodía que inicia otra al terminar, no da problemas.
- Límites: 256 componentes de lógica, 16 salidas cada uno, 200 entradas por segundo para todo el mapa.

**Probarlo.** Empaquetar y probar abre el mapa en Práctica, donde no hay Cazadores ni Modeladores. En la consola
de desarrollador, `maprole hunter` o `maprole hider` te permite probar los filtros de rol, `maplogic` lista el
estado de cada componente y `mapfire <number> <input>` envía una entrada a mano.

### El mapa de ejemplo: Logic Playground

**Map Kit > Examples > Logic Playground** construye un mapa greybox en el que todos los componentes de lógica están
en uso, en `Assets/Maps/LogicPlayground/`. Haz el bake de su iluminación cuando te lo pida y luego pásalo por
Empaquetar y probar como cualquier mapa, o abre sus objetos para ver cómo están conectados (**Map Kit > Logic**
lista todas las conexiones). Sus colores dicen qué es cada cosa: el gris es el mundo, el naranja es algo que se
pulsa, el amarillo se mueve, el rojo hace daño, el azul teletransporta y el morado aparece y desaparece. El techo es
invisible: un collider sin nada dibujado, que cierra el patio abierto para el bake de los límites del mapa.

Nueve salas en una cuadrícula de 3 por 3, de 36 m de lado. Todos empiezan en la del centro.

| Sala | Qué muestra |
|---|---|
| 00 Hub (centro) | Map Round Events: una campanilla al empezar la ronda; una sirena y una baliza de humo rojo al empezar la caza; fuegos artificiales al terminar la ronda (un relé que reproduce chispas y un sonido con retardos). Un generador que zumba en bucle desde el principio. |
| 01 Doors (sur) | Una puerta corredera que se cierra sola cuatro segundos después de abrirse (Return After), da la vuelta si alguien está en medio y zumba. Un activador delante de ella que pulsa su botón para los Cazadores (Press). Una puerta batiente que cualquiera de los dos botones abre o cierra (Reverse). |
| 02 Elevator (norte) | Tres pisos: botones de llamada en cada uno, botones que viajan en la cabina (Go To Stop con el piso como número), un motor al arrancar, una campanilla en cada piso. |
| 03 Platforms (este) | Un foso que mata a quien cae dentro. Un transbordador que va y viene por encima (PingPong) con botones para detenerlo y ponerlo en marcha (Pause, Resume); un carrusel que gira en el centro (Loop), que se enciende y se apaga (Toggle). Salta desde el transbordador: conservas su velocidad. |
| 04 Hazards (oeste) | Una prensa (When Blocked: Crush). Respiraderos de fuego que se encienden en momentos aleatorios: un temporizador activa un relé que enciende tres zonas de daño y el fuego, y los vuelve a apagar un segundo y medio después. Una tubería de vapor que quema. Un interruptor de corriente que lo pausa todo. |
| 05 Vault (noreste) | Tres runas, cada una contada una sola vez (Add, y la runa se desactiva a sí misma); con tres, el botón de la cámara acorazada se desbloquea. Si se pulsa bloqueado, zumba. Una runa trampa resta una (Subtract); la llave maestra de los Cazadores fija la cuenta de golpe (Set Value); un reinicio vuelve a cerrarlo todo (Reset Count, Lock, Back, Enable). |
| 06 Teleports (noroeste) | Dos plataformas que envían a los jugadores de una a otra. Una sala sin puerta: una plataforma te lleva dentro y un botón interior solo te saca a ti (Teleport Activator en un teletransporte desactivado). Un atajo de los Modeladores a la cornisa de la sala sureste. |
| 07 Triggers and Toggles (sureste) | Una placa de presión: mientras alguien está encima (Occupied, Emptied) hay un puente hasta la cornisa. La barandilla del puente es un toggle dentro del toggle del puente. Una alarma que suena una vez por ronda cuando entra un Cazador. Un rincón oculto detrás de un panel. |
| 08 Sounds and Effects (suroeste) | Cada efecto en un pedestal con su botón. Una gramola de dos melodías, cada una inicia la otra al terminar (Finished). Un anuncio que se oye en todo el mapa, una vez por ronda. Un botón que solo hace daño a quien lo pulsa (Hurt Activator). |

## Las reglas

El juego lee un mapa antes de dejar que Unity lo abra, y rechaza todo lo que se salga de estas reglas. **Map Kit >
Validate** las revisa todas en el editor; **Empaquetar y probar** vuelve a revisar los archivos empaquetados con el
propio código del juego.

- **Componentes:** Mesh Filter, Mesh Renderer, LOD Group, Box, Sphere, Capsule y Mesh Colliders, Light,
  Reflection Probe, Light Probe Group, Occlusion Area y Portal, Adaptive Probe Volume, Volume, y los componentes
  propios del kit (Map Marker, Map Mode Object, Map Bounds Volume, Penetrable Surface, Probe Volume
  Region, Kit Map Descriptor, Map Ladder, Map Variant Group, Map Variant Option) y los componentes de lógica del mapa. Nada de scripts propios, ni cámaras, audio
  sources, sistemas de partículas, UI, vídeo, terrain, animación o cuerpos físicos. El sonido y los efectos llegan
  a través de Map Sound y Map Effect.
- **Shaders:** Universal Render Pipeline Lit, Simple Lit, Unlit, Baked Lit. Cielo: Skybox Procedural,
  Cubemap, 6 Sided o Panoramic.
- **Layers:** Default, Ignore Raycast, MapBlocker (paredes invisibles), GuessMapBlocker.
- **Iluminación:** como arriba. Como máximo 64 luces.
- **Tamaño:** como máximo 256 MB para el paquete, 512 MB de texturas, 8192 píxeles en el lado más largo de una
  textura, 3 millones de triángulos, 6 millones de vértices y 10 000 renderers.
- **Nombre:** el ID del mapa es el nombre de su escena en minúsculas: letras, dígitos, `-` y `_`, hasta 23
  caracteres. No se permiten los nombres de escena que usa el propio juego (Startup, Menu, Game, HunterRoom y
  otros, y cualquiera que empiece por `Map_` o `GameMode_`).

Los errores significan que el juego no abrirá el mapa. Vale la pena mirar las advertencias, pero no lo impiden.

## Jugar con amigos

Todos los de la sesión necesitan exactamente los mismos archivos del mapa: el juego los compara antes de cargar el
mapa. El paquete es el mismo para todos los sistemas, así que empaqueta el mapa una vez y envía a tus amigos la
carpeta `Builds/Maps/<id>/` del kit (comprimida en zip). Cada uno pone la carpeta, sin cambios, en la carpeta de
mapas de su juego:

| Sistema | Carpeta |
|---|---|
| Windows | `%USERPROFILE%\AppData\LocalLow\DevAdam\MimicBusters\Maps\Local\` |
| macOS | `~/Library/Application Support/DevAdam/MimicBusters/Maps/Local/` |
| Linux | `~/.config/unity3d/DevAdam/MimicBusters/Maps/Local/` |

El resultado queda como `...\Maps\Local\<id>\map.json` con `map.any.bundle` al lado. Luego el mapa aparece en el
selector de mapas del juego, en **Comunidad**. La compatibilidad con Steam Workshop llegará más adelante, desde
dentro del juego.

## Cuando algo sale mal

- **El kit dice que el juego rechazaría el mapa.** Los motivos aparecen en la ventana Empaquetar y probar. La
  Console tiene el informe completo, y también `Builds/Maps/<id>.report.txt`.
- **El juego dice que no se pudo abrir el mapa.** Activa **Ajustes > Jugabilidad > Consola de desarrollador** en el
  juego, abre la consola con la tecla que está debajo de Esc y escribe `kitmaps`: lista tus mapas y por qué uno no
  se puede jugar. `kitinspect <id>` lee un mapa sin cargarlo.
- **El mapa se ve demasiado oscuro o tiene la luz equivocada.** Vuelve a hacer el bake de la iluminación y luego
  vuelve a empaquetar. Si el validador dice que la escena usa los datos de iluminación de otra escena (pasa con las
  escenas copiadas), usa **Map Kit > Tools > Detach Copied Lighting** y luego haz el bake.
- **Los jugadores atraviesan el suelo o aparecen dentro de las paredes.** Revisa los colliders, mueve el punto de
  aparición y vuelve a hacer el bake de los límites del mapa.
- **El juego avisa de que el mapa se hizo con un kit antiguo.** Descarga el kit nuevo, abre tu mapa en él y vuelve
  a empaquetar.

## Licencia

Consulta [LICENSE.md](LICENSE.md). Mimic Busters y su logotipo pertenecen a DevAdam y no forman parte de la
licencia.

# Mimic Busters Map Kit

Crie seus próprios mapas para **Mimic Busters** no Unity e depois percorra cada um no jogo com um clique.

<!-- languages -->
🌐 [English](README.md) · [Türkçe](README.tr.md) · [Deutsch](README.de.md) · [Español](README.es.md) · [Français](README.fr.md) · [Italiano](README.it.md) · **Português (Brasil)** · [Русский](README.ru.md) · [Українська](README.uk.md) · [Ελληνικά](README.el.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [简体中文](README.zh-Hans.md) · [繁體中文](README.zh-Hant.md) · [العربية](README.ar.md)

O kit é um projeto do Unity com o mesmo código de mapas que o jogo usa. Você monta o mapa como uma cena comum do
Unity, faz o bake da iluminação no Unity, e o kit confere o mapa com as regras do jogo, empacota tudo em um só
arquivo que Windows, Linux e macOS abrem, instala no seu jogo e inicia o jogo nele.

Os mapas feitos com este kit podem ser jogados em **Standard** (Classic no jogo: Caçadores e Modeladores, e suas
variantes Companion e Infestation, que usam os mesmos marcadores), **Prática** (construção livre, sem rodadas),
**Deathmatch** (todos armados, sem lados) e **Adivinhe a imitação** (em turnos, um jogador modela e os outros
adivinham).

## Do que você precisa

- **Unity 6000.3.8f1**, exatamente esta versão, pelo Unity Hub. Os mapas são asset bundles do Unity, e o jogo só
  consegue ler bundles feitos com a própria versão do Unity dele. Não precisa de módulos extras: o kit empacota com
  o que já vem no editor.
- **Mimic Busters**, instalado pela Steam, para testar seus mapas.

## Primeiros passos

1. Baixe o kit: **Code > Download ZIP** no GitHub, ou um zip em **Releases**. Descompacte em algum lugar com um
   caminho curto.
2. No Unity Hub: **Add > Add project from disk**, escolha a pasta descompactada e abra com o 6000.3.8f1. A
   primeira importação leva alguns minutos.
   Quando terminar, a janela de **Boas-vindas** se abre: escolha seu idioma ali (o kit fala inglês, turco, alemão,
   espanhol, francês, italiano, português do Brasil, russo, ucraniano, grego, japonês, coreano e chinês) e confira
   se ele encontrou o jogo. Você pode abrir essa janela de novo em **Map Kit > Welcome** e mudar o idioma depois em
   **Map Kit > Settings** ou na engrenagem de qualquer janela do kit.
3. **Map Kit > New Map**. Dê um nome ao mapa, marque os modos dele e deixe **Adicionar uma sala de exemplo** ligado
   no seu primeiro mapa. O kit cria uma cena em `Assets/Maps/<Name>/` com tudo o que o jogo precisa já no lugar: as
   informações do mapa, os pontos de surgimento, os limites do mapa, um probe volume e as configurações de
   iluminação certas.
4. **Window > Rendering > Lighting > Generate Lighting**. O primeiro bake demora um pouco.
5. **Map Kit > Validate**. Deve aparecer “O jogo vai abrir este mapa.”
6. **Map Kit > Pack and Test**. Na primeira vez, confira a seção **Jogo**: o kit procura o jogo nas suas
   bibliotecas da Steam; se ele estiver em outro lugar, clique em **Escolher...** e selecione o executável do jogo.
   Depois clique em **Empacotar e testar**. O jogo abre direto em uma sessão privada de Prática no seu mapa.

Depois, deixe o mapa com a sua cara: apague a sala de exemplo, traga seus próprios modelos e repita os passos 4 a 6.

## Montando um mapa

**Geometria.** Importe modelos (FBX, OBJ, arquivos do Blender) e texturas como de costume e posicione tudo na
cena. Use os shaders do Universal Render Pipeline: **Lit**, **Simple Lit**, **Unlit** ou **Baked Lit**. Coloque
um collider em tudo em que os jogadores pisam ou esbarram. Marque a geometria estática como **Static** para que
ela receba a luz do bake.

**Informações do mapa.** O objeto `Map` da cena nova tem um **Kit Map Descriptor**: o nome, a descrição, o autor e
os modos do mapa, do jeito que o jogo mostra. Precisa haver exatamente um.

**Pontos de surgimento.** Um ponto de surgimento é um objeto vazio com um **Map Marker**. O Inspector dele pergunta
duas coisas, nesta ordem: **1. Modos**, quais modos de jogo usam o ponto, e **2. Tipos**, para que ele serve. Logo
abaixo, mostra qual modo usa o marcador e como, e avisa sobre um tipo que nenhum dos modos marcados lê:

| Tipo | Para que serve |
|---|---|
| HiderSpawn | Onde os Modeladores começam a rodada (Standard). Também é onde os jogadores aparecem na Prática. |
| HunterSpawn | Onde os Caçadores saem quando a caçada começa (Standard). Eles passam o tempo de preparação na sala dos Caçadores do próprio jogo. |
| LobbySpawn | Onde todos esperam enquanto a sala enche. Opcional: sem ele, a sala usa os HiderSpawns. |
| HunterDoor | Opcional. Um objeto que fica fechado enquanto todos esperam na sala e durante a preparação, e se abre quando a caçada começa. Só o primeiro conta. |
| ModelerSpawn | Prática: onde os jogadores aparecem, como HiderSpawn e LobbySpawn. Adivinhe a imitação: onde o jogador da vez modela. |
| ArenaSpawn | Opcional. Uma posição de duelo 1v1 (Standard). A sala dos Caçadores do jogo tem a sua própria. |
| GuessComputer | Opcional (Adivinhe a imitação). Onde o jogo coloca a mesa do computador; o jogador da vez se senta nela enquanto o modelo dele fica no ModelerSpawn. Só o primeiro conta. Sem ele, o jogador fica escondido dentro do próprio modelo. |

Standard precisa de pelo menos um HunterSpawn e um HiderSpawn. Prática precisa de um HiderSpawn, LobbySpawn ou
ModelerSpawn com Prática marcado. Deathmatch usa todos os HunterSpawns, HiderSpawns e LobbySpawns com Deathmatch
marcado e coloca o jogador naquele que está mais longe de todos, então coloque pelo menos seis, espalhados. Adivinhe
a imitação coloca quem adivinha nos LobbySpawns (nos HiderSpawns quando não houver nenhum) e o jogador da vez em um
ModelerSpawn, todos com Adivinhe a imitação marcado: ponha esse onde todos possam ver. Coloque alguns de cada, espalhados, no chão, virados para dentro do mapa. A linha que sai
do marcador mostra para onde o jogador fica virado. Um marcador sem nenhum modo marcado não é usado por nenhum modo.

**Limites do mapa.** O **Map Bounds Volume** é uma caixa em volta de toda a área de jogo. Depois do bake, o jogo
sabe onde os jogadores podem estar e traz de volta quem cair para fora do mapa. Faça a caixa cobrir todo lugar que
um jogador consegue alcançar, com espaço em cima para pulos e telhados, e depois use **Map Kit > Bake Map Bounds**.
Faça o bake de novo sempre que mover paredes, pisos ou pontos de surgimento. Se o validador disser que a área de
jogo chega à parede da caixa em um mapa fechado, há um buraco onde ele indica.

O bake preenche o ar a partir dos seus pontos de surgimento para fora, em células de meio metro. Uma sala aonde os
jogadores só chegam por teleporte nunca é encontrada assim: adicione um ponto dentro dela aos **Seeds** do volume, e
mais um para o resto do mapa, porque com Seeds preenchido o preenchimento começa só a partir deles. O validador
avisa se um ponto de surgimento ou o destino de um teleporte ficar de fora. Uma fresta de uma célula só de largura
(uma fenda acima de uma parede, uma rachadura estreita ao lado de uma plataforma) é apontada como possível
vazamento: deixe as frestas bem fechadas ou com pelo menos um metro.

**Escadas.** Um **Map Ladder** é uma caixa que os jogadores escalam enquanto estão dentro dela, do jeito que se
sobe uma escada em Half-Life e Counter-Strike: andar em direção à escada faz subir, olhar para cima ou para baixo
decide o sentido, as teclas laterais movem para o lado ao longo dela e pular solta a escada. Adicione a um objeto
vazio (ou ao modelo da escada) e ajuste o tamanho da caixa na janela Scene com as alças dela. A caixa é o espaço
onde fica o jogador que está subindo: os degraus e o ar na frente deles. A **seta azul (Z) é para onde quem sobe
fica virado**, em direção à escada. A subida segue o eixo para cima do objeto, então, para uma escada apoiada na
parede, incline o objeto em X, com o topo para a parede, no máximo 30 graus. Deixe o topo da caixa chegar até o
piso ao qual a escada leva, ou um pouco acima, se for o caso. O modelo em si pode ser sólido; os jogadores não
precisam encostar nele.

**Variações do mapa.** Partes do mapa podem sair diferentes a cada rodada, para que os Caçadores não consigam
decorar o mapa. Coloque um **Map Variant Group** em um objeto vazio e faça de cada versão dessa parte um
**filho direto** dele: um corredor aberto, o mesmo corredor bloqueado, caixas empilhadas de três jeitos. A cada rodada o
jogo liga UM filho e desliga os outros; o servidor escolhe e todos veem o mesmo. **Modes** diz em quais modos a
escolha acontece (Standard por padrão); nos outros, os filhos ficam como você os deixou na cena. O Inspector do
grupo lista as versões com um botão **Mostrar** em cada uma, para montar e conferir uma de cada vez.
- **Peso:** adicione um **Map Variant Option** a um filho para que ele saia mais ou menos vezes (1 sem ele; 0 nunca).
- **Avoid Repeat** (ligado por padrão): a versão da rodada anterior não é escolhida de novo enquanto outra puder
  sair.
- **Link Id:** grupos com o mesmo ID escolhem o mesmo número de filho, para que as duas metades de uma ideia
  combinem: uma porta fechada aqui enquanto uma passagem se abre ali.
- Grupos podem ficar dentro das versões de outro grupo.
- Pontos de surgimento dentro de uma versão só são usados nas rodadas em que essa versão está ligada.
- Não marque nada dentro de uma versão como **Occluder Static**: o occlusion culling trataria aquilo como se
  estivesse sempre ali, e o que estiver atrás sumiria nas rodadas em que a versão está desligada. O validador avisa
  sobre isso.

**Iluminação.** Mantenha as configurações de iluminação que vieram com o mapa novo: iluminação Mixed no modo
**Shadowmask**, lightmaps **Directional**, **Realtime Global Illumination desligada**. O jogo traz shaders só para
essa configuração. As luzes são **Mixed** ou **Baked**; mais de quatro luzes em tempo real com sombras deixam o
jogo lento. A neblina, se você quiser, precisa ser **Exponential Squared**. O **Adaptive Probe Volume** ilumina os
jogadores e os Modeladores para que combinem com o mapa; faça o bake dele junto com o resto (ele faz parte do
Generate Lighting). Faça o bake de novo depois de cada mudança na geometria ou nas luzes, senão o jogo mostra a luz
antiga.

**Pós-processamento.** Um **Volume** só pode ter efeitos de cor e tom: Bloom, Tonemapping, Color
Adjustments, White Balance, Lift Gamma Gain, Shadows Midtones Highlights, Split Toning, Channel Mixer,
Color Curves, Vignette.

## Lógica do mapa

Você pode deixar um mapa interativo do jeito que o Hammer faz: uma plataforma que se move quando um botão é
apertado e volta um tempo depois, um som, uma zona que causa dano. Não se escreve código; tudo é conectado no
Inspector, e o jogo faz o trabalho.

Adicione estes em **Add Component > Map Kit > Logic**:

| Componente | O que faz |
|---|---|
| Map Button | Um botão que os jogadores apertam com a tecla de interação. Precisa de um collider. Quem pode apertar, um tempo de espera, um texto de aviso. |
| Map Mover | Move e gira o objeto e seus filhos ao longo de uma lista de paradas, levando os jogadores que estão em cima. Return After: volta sozinho essa quantidade de segundos depois de chegar à última parada. PingPong e Loop funcionam sozinhos. |
| Map Trigger | Uma caixa invisível: eventos quando um jogador entra ou sai, e para o primeiro a entrar e o último a sair. |
| Map Teleport | Envia quem entra na caixa dele para um objeto de destino, virado para onde esse objeto está virado. |
| Map Hurt Zone | Causa dano a quem estiver dentro a cada intervalo, ou mata na hora. Ninguém pontua por uma morte causada pelo mapa. |
| Map Sound | Um som onde está: uma vez ou em loop, diminuindo com a distância ou igual em todo lugar. |
| Map Effect | Um dos efeitos do próprio jogo: fumaça, vapor, fogo, faíscas, poeira, gotas. Cor, tamanho, quantidade. |
| Map Toggle | Mostra ou oculta o objeto e seus filhos (o que é desenhado, o que é sólido, as luzes). |
| Map Relay | Repassa uma entrada para várias saídas, cada uma com seu próprio atraso. |
| Map Timer | Dispara um evento a cada intervalo enquanto está ativado. |
| Map Counter | Conta e dispara um evento quando a contagem chega ao alvo. |
| Map Round Events | A rodada começou, a caçada começou, a rodada terminou. |

**Conexões.** Todo componente tem uma lista de **Saídas**: um *evento* (Pressed, ArrivedAtEnd, Entered...), um
*alvo* (outro componente de lógica, arrastado para lá), uma *entrada* (Forward, Back, Play, Enable, Disable...), um
*atraso* e, quando a entrada aceita, um número (uma parada, uma quantidade). O Inspector só oferece os eventos que o
componente dispara e as entradas que o alvo aceita, marca em laranja uma conexão que não tem como funcionar e lista
embaixo quem está conectado ao componente. **Map Kit > Logic** mostra todas as conexões em uma página só.

Exemplo: o evento *Pressed* do botão na entrada *Forward* do elevador. O elevador tem uma parada (1,8 m acima) e
Return After 4: você aperta, ele sobe, e quatro segundos depois ele desce. A sala de exemplo do **New Map** vem
exatamente com isso, mais um som de motor (o *Started* do elevador no *Play* de um Map Sound) e, na outra sala, um
aquecedor: um botão liga e desliga ao mesmo tempo um Map Hurt Zone e um Map Effect de fogo.

**Quem disparou a cadeia.** Um evento causado por um jogador (um aperto de botão, entrar em um gatilho, levar dano)
lembra desse jogador ao longo de toda a cadeia, inclusive nos atrasos. Duas entradas agem sobre ele:
*HurtActivator* em uma zona de dano aplica nele o dano dela uma vez (ou o número da saída no lugar), e
*TeleportActivator* em um teleporte envia o jogador para o destino. Um botão de alçapão que só derruba quem apertou,
um gatilho que só causa dano a quem pisou nele: é só conectar, sem precisar de caixa. Um teleporte usado só desse
jeito deve começar desativado, senão a própria caixa dele também envia as pessoas.

**Ver antes de empacotar.** Na janela Scene, a linha de cada saída mostra o evento e a entrada dela. Um Map Mover
selecionado tem uma alça em cada parada e, em Prévia, uma cópia transparente que você pode deslizar ao longo do
caminho ou reproduzir do jeito que o jogo reproduz, com o tempo que o trajeto leva.

No começo de cada rodada, tudo volta a ser como você configurou. Um componente com Start Enabled desligado não faz
nada até chegar uma entrada *Enable*.

**Atenção a estes pontos.**

- Nada que se move pode ser **Static**: o static batching congela o objeto no lugar e a luz do bake dele fica para
  trás. O que se move é iluminado pelas probes.
- Uma plataforma em movimento empurra o jogador que estiver no caminho; quando não consegue, faz o que o
  **When Blocked** mandar: volta, esmaga ou segue em frente.
- Um jogador que pula de uma plataforma em movimento mantém a velocidade lateral dela até pousar, então um pulo em
  uma balsa em movimento cai de volta na balsa.
- O destino de um teleporte é verificado como um ponto de surgimento: nada sólido ali, um piso embaixo. Um destino
  dentro da própria caixa de um teleporte envia o jogador de novo um segundo depois; o validador avisa.
- Sons: no máximo 2 minutos cada, mono ou estéreo, até 48 kHz; todos os sons de um mapa somam no máximo 32 MB na
  memória. Carregue os longos como **Compressed In Memory** ou **Streaming**. Os sons passam pelo volume de efeitos
  do jogador.
- Um loop sem atraso (A aciona B e B aciona A na hora) é cortado em 32 passos no jogo, e o validador conta isso como
  erro. Dê um atraso a uma saída do loop. Só fecham um loop assim as entradas que fazem o alvo agir na hora (um
  aperto de botão, o acionamento de um relé, a mudança de um contador, um mover enviado para algum lugar): um botão
  que se desliga sozinho, ou uma música que começa outra quando termina, não tem problema.
- Limites: 256 componentes de lógica, 16 saídas cada, 200 entradas por segundo no mapa inteiro.

**Testando.** Empacotar e testar abre o mapa na Prática, que não tem Caçadores nem Modeladores. No console do
desenvolvedor, `maprole hunter` ou `maprole hider` permite testar filtros de função, `maplogic` lista o estado de
cada componente e `mapfire <number> <input>` envia uma entrada manualmente.

### O mapa de exemplo: Logic Playground

**Map Kit > Examples > Logic Playground** monta um mapa greybox em que todos os componentes de lógica estão em ação,
em `Assets/Maps/LogicPlayground/`. Faça o bake da iluminação quando ele pedir e depois use Empacotar e testar como
em qualquer mapa, ou abra os objetos dele para ver como estão conectados (**Map Kit > Logic** lista todas as
conexões). As cores dizem o que cada coisa é: cinza é o mundo, laranja é algo que você aperta, amarelo se move,
vermelho causa dano, azul teleporta, roxo aparece e desaparece. O teto é invisível: um collider sem nada
desenhado, que fecha o pátio aberto para o bake dos limites do mapa.

Nove salas em uma grade de 3 por 3, com 36 m de lado. Todo mundo começa na do meio.

| Sala | O que mostra |
|---|---|
| 00 Hub (meio) | Map Round Events: um sino quando a rodada começa; uma sirene e um sinalizador de fumaça vermelha quando a caçada começa; fogos de artifício quando a rodada termina (um relé que toca faíscas e um som com atrasos). Um gerador zumbindo em loop desde o começo. |
| 01 Doors (sul) | Uma porta de correr que fecha sozinha quatro segundos depois de abrir (Return After), volta se alguém estiver no caminho e faz um zumbido. Um gatilho na frente dela que aperta o botão dela para os Caçadores (Press). Uma porta de abrir que qualquer um dos dois botões abre ou fecha (Reverse). |
| 02 Elevator (norte) | Três andares: botões de chamada em cada um, botões que viajam na cabine (Go To Stop com o andar como número), um motor quando ele parte, um sino em cada andar. |
| 03 Platforms (leste) | Um poço que mata quem cai nele. Uma balsa indo e voltando por cima dele (PingPong) com botões para parar e retomar (Pause, Resume); um carrossel girando no meio (Loop), ligado e desligado (Toggle). Pule da balsa: você mantém a velocidade dela. |
| 04 Hazards (oeste) | Um esmagador (When Blocked: Crush). Saídas de fogo acesas em momentos aleatórios: um temporizador aciona um relé que liga três zonas de dano e o fogo, e desliga tudo de novo um segundo e meio depois. Um cano de vapor que escalda. Um interruptor de energia que pausa tudo. |
| 05 Vault (nordeste) | Três runas, cada uma contada uma vez (Add, e a runa se desliga sozinha); com três, o botão do cofre é destravado. Apertado enquanto travado, faz um zumbido. Uma runa falsa tira uma (Subtract); a chave mestra dos Caçadores define a contagem de uma vez (Set Value); um reset fecha tudo de novo (Reset Count, Lock, Back, Enable). |
| 06 Teleports (noroeste) | Duas plataformas que enviam os jogadores de uma para a outra. Uma sala sem porta: uma plataforma leva você para dentro, um botão lá dentro tira só você (Teleport Activator em um teleporte desligado). Um atalho dos Modeladores para a borda da sala sudeste. |
| 07 Triggers and Toggles (sudeste) | Uma placa de pressão: enquanto alguém está em cima dela (Occupied, Emptied) existe uma ponte até a borda. O corrimão da ponte é um toggle dentro do toggle da ponte. Um alarme que toca uma vez por rodada quando um Caçador entra. Um cantinho escondido atrás de um painel. |
| 08 Sounds and Effects (sudoeste) | Cada efeito em um pedestal com seu botão. Uma jukebox com duas músicas, cada uma começando a outra quando termina (Finished). Um anúncio ouvido no mapa inteiro, uma vez por rodada. Um botão que só causa dano a quem aperta (Hurt Activator). |

## As regras

O jogo lê um mapa antes de deixar o Unity abri-lo e recusa tudo o que estiver fora destas regras. **Map Kit >
Validate** confere todas elas no editor; **Empacotar e testar** confere de novo os arquivos empacotados com o
próprio código do jogo.

- **Componentes:** Mesh Filter, Mesh Renderer, LOD Group, Box, Sphere, Capsule e Mesh Colliders, Light,
  Reflection Probe, Light Probe Group, Occlusion Area e Portal, Adaptive Probe Volume, Volume, e os componentes
  do próprio kit (Map Marker, Map Mode Object, Map Bounds Volume, Penetrable Surface, Probe Volume
  Region, Kit Map Descriptor, Map Ladder, Map Variant Group, Map Variant Option) e os componentes de lógica do mapa. Nada de scripts seus, nem câmeras, audio
  sources, sistemas de partículas, UI, vídeo, terrain, animação ou corpos físicos. Som e efeitos entram por meio de
  Map Sound e Map Effect.
- **Shaders:** Universal Render Pipeline Lit, Simple Lit, Unlit, Baked Lit. Céu: Skybox Procedural,
  Cubemap, 6 Sided ou Panoramic.
- **Layers:** Default, Ignore Raycast, MapBlocker (paredes invisíveis), GuessMapBlocker.
- **Iluminação:** como acima. No máximo 64 luzes.
- **Tamanho:** no máximo 256 MB para o pacote, 512 MB de texturas, 8192 pixels no lado mais longo de uma textura,
  3 milhões de triângulos, 6 milhões de vértices e 10.000 renderers.
- **Nome:** o ID do mapa é o nome da cena em minúsculas: letras, dígitos, `-` e `_`, até 23 caracteres. Nomes de
  cena que o próprio jogo usa (Startup, Menu, Game, HunterRoom e outros, e qualquer coisa que comece com `Map_` ou
  `GameMode_`) não são permitidos.

Erros significam que o jogo não vai abrir o mapa. Vale a pena olhar os avisos, mas eles não impedem o mapa.

## Jogando com os amigos

Todo mundo na sessão precisa ter exatamente os mesmos arquivos do mapa: o jogo compara os arquivos antes de
carregar o mapa. O pacote é o mesmo para todos os sistemas, então empacote o mapa uma vez e mande para os seus
amigos a pasta `Builds/Maps/<id>/` do kit (compactada em zip). Cada um coloca a pasta, sem mudar nada, na pasta de
mapas do seu jogo:

| Sistema | Pasta |
|---|---|
| Windows | `%USERPROFILE%\AppData\LocalLow\DevAdam\MimicBusters\Maps\Local\` |
| macOS | `~/Library/Application Support/DevAdam/MimicBusters/Maps/Local/` |
| Linux | `~/.config/unity3d/DevAdam/MimicBusters/Maps/Local/` |

O resultado fica assim: `...\Maps\Local\<id>\map.json` com `map.any.bundle` ao lado. O mapa então aparece no
seletor de mapas do jogo, em **Comunidade**. O suporte à Oficina Steam virá mais tarde, de dentro do jogo.

## Quando algo dá errado

- **O kit diz que o jogo recusaria o mapa.** Os motivos aparecem na janela Empacotar e testar. O Console tem o
  relatório completo, e o `Builds/Maps/<id>.report.txt` também.
- **O jogo diz que não foi possível abrir o mapa.** Ative **Configurações > Jogabilidade > Console do desenvolvedor**
  no jogo, abra o console com a tecla abaixo do Esc e digite `kitmaps`: ele lista seus mapas e o motivo de um deles
  não poder ser jogado. `kitinspect <id>` lê um mapa sem carregá-lo.
- **O mapa está escuro demais ou com a luz errada.** Faça o bake da iluminação de novo e depois empacote de novo.
  Se o validador disser que a cena usa os dados de iluminação de outra cena (isso acontece com cenas copiadas), use
  **Map Kit > Tools > Detach Copied Lighting** e depois faça o bake.
- **Os jogadores caem através do chão ou surgem dentro das paredes.** Confira os colliders, mova o ponto de
  surgimento e faça o bake dos limites do mapa de novo.
- **O jogo informa que o mapa foi feito com um kit mais antigo.** Baixe o kit novo, abra seu mapa nele e empacote
  de novo.

## Licença

Veja [LICENSE.md](LICENSE.md). Mimic Busters e seu logotipo pertencem à DevAdam e não fazem parte da
licença.

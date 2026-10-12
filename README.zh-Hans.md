# Mimic Busters Map Kit

在 Unity 中为 **Mimic Busters** 制作你自己的地图，然后一键进入游戏，亲自在地图里走一走。

<!-- languages -->
🌐 [English](README.md) · [Türkçe](README.tr.md) · [Deutsch](README.de.md) · [Español](README.es.md) · [Français](README.fr.md) · [Italiano](README.it.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [Українська](README.uk.md) · [Ελληνικά](README.el.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · **简体中文** · [繁體中文](README.zh-Hant.md) · [العربية](README.ar.md)

Map Kit 是一个 Unity 项目，使用的地图代码与游戏完全相同。你把地图当作普通的 Unity 场景来搭建，在 Unity 中 Bake 光照；之后 Map Kit 会按照游戏的规则检查它，把它打包成一个 Windows、Linux 和 macOS 都能打开的文件，安装到你的游戏中，并直接在这张地图上启动游戏。

用 Map Kit 制作的地图可以在以下模式中游玩：**Standard**（游戏中显示为 Classic；猎人对建模者，也包括使用相同标记的两个变体：伙伴和 Infestation）、**练习**（Map Kit 中显示为 Practice；自由建造，没有回合）、**Deathmatch**（人人持枪，不分阵营）和**猜猜拟态**（轮流进行，一名玩家建模，其他人来猜）。

## 需要准备什么

- 从 Unity Hub 安装的 **Unity 6000.3.8f1**，必须正好是这个版本。地图是 Unity 的 asset bundle，而游戏只能读取由它自己所用的 Unity 版本生成的 bundle。不需要额外的模块：Map Kit 只用编辑器自带的功能打包。
- 通过 Steam 安装的 **Mimic Busters**，用来测试你的地图。

## 快速上手

1. 下载 Map Kit：在 GitHub 上点击 **Code > Download ZIP**，或者从 **Releases** 下载 zip。把它解压到路径较短的位置。
2. 在 Unity Hub 中选择 **Add > Add project from disk**，选中解压出来的文件夹，用 6000.3.8f1 打开。第一次导入需要几分钟。完成后会打开**欢迎**窗口：在这里选择你的语言（Map Kit 支持英语、土耳其语、德语、西班牙语、法语、意大利语、巴西葡萄牙语、俄语、乌克兰语、希腊语、日语、韩语和中文），并确认它已经找到了游戏。之后可以通过 **Map Kit > Welcome** 再次打开这个窗口，也可以随时在 **Map Kit > Settings** 中，或者点击任意 Map Kit 窗口里的齿轮图标来更改语言。
3. **Map Kit > New Map**。给地图起个名字，勾选它的模式；如果这是你的第一张地图，请保持勾选**添加示例房间**。Map Kit 会在 `Assets/Maps/<Name>/` 中创建一个场景，游戏需要的一切都已经就位：地图信息、出生点、地图边界、一个 probe volume 以及正确的光照设置。
4. **Window > Rendering > Lighting > Generate Lighting**。第一次 Bake 需要一些时间。
5. **Map Kit > Validate**。结果应当显示“游戏会打开此地图。”
6. **Map Kit > Pack and Test**。第一次使用时，先看一下**游戏**部分：Map Kit 会在你的 Steam 库中查找游戏；如果游戏装在别处，点击 **选择...** 并选中游戏的可执行文件。然后点击**打包并测试**。游戏会直接启动，在你的地图上开启一场只有你自己的练习对局。

接下来，把它变成你自己的地图：删除示例房间，导入你自己的模型，然后重复第 4 到第 6 步。

## 搭建地图

**几何体**。像平常一样导入模型（FBX、OBJ、Blender 文件）和纹理，并把它们放进场景。使用 Universal Render Pipeline 的着色器：**Lit**、**Simple Lit**、**Unlit** 或 **Baked Lit**。玩家会踩到或撞到的所有东西都要加上 Collider。把不会移动的几何体标记为 **Static**，这样它才能获得 Bake 的光照。

**地图信息**。新场景中的 `Map` 物体上有一个 **Kit Map Descriptor**：它保存地图的名称、描述、作者和模式，游戏中会照此显示。场景中必须正好有一个。

**出生点**。出生点是一个带有 **Map Marker** 的空物体。它的 Inspector 会依次询问两件事：**1. 模式**，哪些游戏模式使用它；**2. 类型**，它的用途是什么。下方会显示哪个模式把这个标记用作什么；如果某个类型没有任何已勾选的模式会读取，也会给出警告：

| 类型 | 用途 |
|---|---|
| HiderSpawn | 建模者在回合开始时所在的位置（Standard）。练习模式中的玩家也出现在这里。 |
| HunterSpawn | 狩猎开始时猎人出来的位置（Standard）。准备阶段他们待在游戏自带的猎人房间里。 |
| LobbySpawn | 大厅等人期间所有人等待的位置。可选：没有的话，大厅会使用 HiderSpawn。 |
| HunterDoor | 可选。在大厅和准备阶段保持关闭、狩猎开始时打开的物体。只有第一个有效。 |
| ModelerSpawn | 练习：玩家出现的位置，与 HiderSpawn 和 LobbySpawn 相同。猜猜拟态：轮到的玩家进行建模的位置。 |
| ArenaSpawn | 可选。1v1 决斗位置（Standard）。游戏的猎人房间有它自己的决斗位置。 |
| GuessComputer | 可选（猜猜拟态）。游戏摆放电脑桌的位置；轮到的玩家坐在桌前，他的模型则站在 ModelerSpawn 上。只有第一个有效。没有的话，玩家会藏在自己的模型里。 |

Standard 至少需要一个 HunterSpawn 和一个 HiderSpawn。练习模式需要一个勾选了 Practice 的 HiderSpawn、LobbySpawn 或 ModelerSpawn。Deathmatch 会使用所有勾选了 Deathmatch 的 HunterSpawn、HiderSpawn 和 LobbySpawn，并把玩家放在离所有人最远的那个点上，所以至少放六个，并且要分散开。猜猜拟态把猜的玩家放在 LobbySpawn 上（没有 LobbySpawn 时用 HiderSpawn），把轮到的玩家放在一个 ModelerSpawn 上，这些都要勾选猜猜拟态：请把那个 ModelerSpawn 放在所有人都能看到的地方。每种都放几个，分散开，放在地面上，面朝地图内部。从标记引出的线表示玩家面朝的方向。没有勾选任何模式的标记不会被任何模式使用。

**地图边界**。**Map Bounds Volume** 是一个包住整个游玩区域的范围框。Bake 之后，游戏就知道玩家可以待在哪里，并会把掉出地图的人带回来。让这个框覆盖玩家能到达的所有地方，上方为跳跃和屋顶留出空间，然后执行 **Map Kit > Bake Map Bounds**。每次移动墙壁、地面或出生点之后，都要重新 Bake。如果在一张封闭的地图上，验证提示游玩区域碰到了框的边缘，那么它所指的位置就有一个洞。

Bake 会从你的出生点开始，以半米大小的格子向外填充空气。只能通过传送到达的房间用这种方式永远找不到：在该 Volume 的 **Seeds** 中添加一个位于这个房间里的点，也为地图的其余部分添加一个，因为填写了 Seeds 之后，填充只会从这些点开始。如果某个出生点或传送目标落在了外面，验证会提示。只有一格宽的缝隙（墙顶上的一道缝、平台边上的一条窄缝）会被报告为可能的泄漏：缝隙要么完全封死，要么至少留一米宽。

**梯子**。**Map Ladder** 是一个范围框，玩家待在框里时就能攀爬，方式和 Half-Life、Counter-Strike 里一样：朝梯子移动就会攀爬，向上或向下看决定往哪个方向爬，左右移动键沿梯子横向移动，跳跃会松手离开。把它添加到一个空物体（或梯子模型）上，然后在 Scene 视图中用手柄调整框的大小。这个框就是攀爬中的玩家所占的空间：梯级以及梯级前方的空气。它的**蓝色箭头（Z）是攀爬者面朝的方向**，也就是朝向梯子。攀爬沿着物体的上方向进行，所以对于斜靠在墙上的梯子，请将物体绕 X 轴倾斜，顶端朝向墙壁，最多 30 度。让框的顶部到达梯子通往的那层地面，宁可稍微高出一点。模型本身可以是实心的；玩家不需要碰到它。

**地图变体**。地图的某些部分可以每回合都不一样，这样猎人就无法背下整张地图。在一个空物体上添加 **Map Variant Group**，并把这一部分的每个版本都设为它的**直接子物体**：一条敞开的走廊、同一条被堵住的走廊、三种不同堆法的箱子。每回合游戏只开启其中一个子物体，其余全部关闭；由服务器选择，所有人看到的都一样。**Modes** 决定哪些模式会进行选择（默认为 Standard）；在其他模式中，子物体保持你在场景中摆放的状态。该组的 Inspector 会列出所有版本，每个版本旁都有一个**显示**按钮，方便你一次搭建并检查一个。
- **权重**：给子物体添加 **Map Variant Option**，可以让它更常或更少出现（没有时为 1；为 0 时永远不会出现）。
- **Avoid Repeat**（默认开启）：只要还有其他版本可选，上一回合的版本就不会再次被选中。
- **Link Id**：Link Id 相同的组会选择相同序号的子物体，让同一个设计的两部分保持一致：这里的门关上时，那里的缺口就打开。
- 组可以放在另一个组的版本里面。
- 版本内的出生点只在该版本开启的回合中使用。
- 不要把版本内的任何东西标记为 **Occluder Static**：Occlusion Culling 会认为它一直都在，在它关闭的回合里，它后面的东西就会消失不见。验证会对此给出警告。

**光照**。保留新地图自带的光照设置：Mixed 光照使用 **Shadowmask** 模式，**Directional** Lightmap，**关闭 Realtime Global Illumination**。游戏只带有这套设置所需的着色器。灯光使用 **Mixed** 或 **Baked**；超过四个带阴影的实时灯光会拖慢游戏。如果想要雾效，必须使用 **Exponential Squared**。**Adaptive Probe Volume** 会照亮玩家和建模者，让他们与地图的光照相协调；它和其余部分一起 Bake（它是 Generate Lighting 的一部分）。每次改动几何体或灯光后都要重新 Bake，否则游戏会显示旧的光照。

**后期处理**。**Volume** 只能带有颜色和色调效果：Bloom、Tonemapping、Color Adjustments、White Balance、Lift Gamma Gain、Shadows Midtones Highlights、Split Toning、Channel Mixer、Color Curves、Vignette。

## 地图逻辑

你可以像在 Hammer 中那样让地图具有互动性：按下按钮后开始移动、过一会儿又自己回来的平台，一段声音，一个会造成伤害的区域。不需要写代码；所有东西都在 Inspector 中连接，具体工作由游戏完成。

从 **Add Component > Map Kit > Logic** 中添加这些组件：

| 组件 | 作用 |
|---|---|
| Map Button | 玩家用互动键按下的按钮。需要 Collider。可以设置谁能按、冷却时间和提示文字。 |
| Map Mover | 让物体及其子物体沿着一系列站点移动和旋转，并带动站在上面的玩家。Return After：到达最后一站后经过这么多秒，自己返回。PingPong 和 Loop 会自行运转。 |
| Map Trigger | 一个不可见的范围框：玩家进入或离开时触发事件，第一个进入和最后一个离开时也会触发。 |
| Map Teleport | 把走进其范围框的人传送到一个目标物体处，并让他面朝目标物体所朝的方向。 |
| Map Hurt Zone | 每隔一个间隔对框内的人造成伤害，或者直接击杀。由地图造成的死亡不给任何人计分。 |
| Map Sound | 在所在位置播放的声音：播放一次或循环播放，随距离衰减或在各处音量相同。 |
| Map Effect | 游戏自带的一种特效：烟雾、蒸汽、火焰、火花、灰尘、水滴。可以调整颜色、大小和数量。 |
| Map Toggle | 显示或隐藏物体及其子物体（渲染、碰撞、灯光）。 |
| Map Relay | 将一个输入传递给多个输出，每个输出都有自己的延迟。 |
| Map Timer | 启用时每隔一个间隔触发一次事件。 |
| Map Counter | 计数，计数达到目标值时触发事件。 |
| Map Round Events | 回合开始、狩猎开始、回合结束。 |

**连接**。每个组件都有一个**输出**列表：一个*事件*（Pressed、ArrivedAtEnd、Entered...），一个*目标*（另一个逻辑组件，拖进来即可），一个*输入*（Forward、Back、Play、Enable、Disable...），一个*延迟*，如果该输入需要，还有一个数字（站点编号、数量）。Inspector 只提供该组件会触发的事件和目标能接收的输入，把无法生效的连接标成橙色，并在下方列出有哪些连接接入了这个组件。**Map Kit > Logic** 会在一页中显示所有连接。

示例：把按钮的 *Pressed* 事件连到电梯的 *Forward* 输入。电梯只有一个站点（向上 1.8 米），Return After 设为 4：一按，它就升起，四秒后再降下来。**New Map** 的示例房间里正好带有这个例子，另外还有电机声（电梯的 *Started* 连到一个 Map Sound 的 *Play*），以及另一个房间里的取暖器：一个按钮同时开关一个 Map Hurt Zone 和一个火焰 Map Effect。

**引发事件链的玩家**。由玩家引起的事件（按下按钮、走进触发器、受到伤害）会在整条事件链中记住这个玩家，包括延迟期间。有两个输入会作用于这个玩家：伤害区域的 *HurtActivator* 会对他造成一次该区域的伤害（或者改用输出中的数字），传送器的 *TeleportActivator* 会把他送到传送目标。只让按下的人掉下去的陷阱门按钮、只伤害踩进去那个人的触发器：连上线就行，不需要范围框。只以这种方式使用的传送器应该在开始时处于禁用状态，否则它自己的范围框也会传送玩家。

**打包前预览**。在 Scene 视图中，每个输出的连线上都标注了它的事件和输入。选中的 Map Mover 在每个站点上都有一个手柄；在“预览”下还有一个半透明的副本，你可以沿着路径拖动它，或者像游戏中那样播放它，同时会显示走完全程需要的时间。

每回合开始时，一切都会恢复到你设定的样子。关闭了 Start Enabled 的组件在收到 *Enable* 输入之前不会做任何事。

**注意事项**。

- 任何会移动的东西都不能是 **Static**：static batching 会把它固定在原地，它被 Bake 的光照也会留在原处。会移动的物体由 Probe 照明。
- 移动平台会推开挡路的玩家；推不动时，它会按照 **When Blocked** 的设置行事：掉头返回、压扁玩家，或者继续前进。
- 从移动平台上跳起的玩家在落地前会保持平台的水平速度，所以在行进中的渡船上起跳，仍会落回渡船上。
- 传送目标会像出生点一样接受检查：那里不能有实心物体，下方要有地面。位于传送器自身范围框内的目标会在一秒后把玩家再传送一次；验证会给出警告。
- 声音：每段最长 2 分钟，单声道或立体声，最高 48 kHz；一张地图所有声音在内存中合计最多 32 MB。较长的声音请用 **Compressed In Memory** 或 **Streaming** 加载。声音受玩家的音效音量控制。
- 没有延迟的循环（A 触发 B，B 又立即触发 A）在游戏中会在第 32 步被截断，验证也会把它算作错误。给循环中的某个输出加上延迟即可。只有会让目标立刻行动的输入才会构成这种循环（按下、中继的触发、计数器的变化、被派往某处的 Mover）：关闭自己的按钮，或者播放完毕后启动另一首的曲子，都没有问题。
- 上限：256 个逻辑组件，每个组件 16 个输出，整张地图每秒 200 个输入。

**试玩**。打包并测试会在练习模式中打开地图，练习模式里没有猎人和建模者。在开发者控制台中，`maprole hunter` 或 `maprole hider` 可以让你试验角色筛选，`maplogic` 会列出每个组件的状态，`mapfire <number> <input>` 可以手动发送一个输入。

### 示例地图：Logic Playground

**Map Kit > Examples > Logic Playground** 会在 `Assets/Maps/LogicPlayground/` 中生成一张灰盒地图，每种逻辑组件都在其中发挥作用。在它询问时 Bake 光照，然后像其他地图一样打包并测试，或者打开它的物体，看看它们是如何连接的（**Map Kit > Logic** 会列出所有连接）。颜色表示物体的用途：灰色是场景本身，橙色是可以按的东西，黄色会移动，红色会造成伤害，蓝色会传送，紫色会出现和消失。天花板是不可见的：它是一个不渲染任何东西的 Collider，在 Bake 地图边界时把开放的庭院封闭起来。

九个房间排成 3×3 的网格，总宽 36 米。所有人都从中间的房间开始。

| 房间 | 展示内容 |
|---|---|
| 00 Hub（中间） | Map Round Events：回合开始时响起提示音；狩猎开始时响起警报并冒出红色烟雾信标；回合结束时放烟花（一个按延迟播放火花和声音的中继）。一台从一开始就循环嗡嗡作响的发电机。 |
| 01 Doors（南） | 一扇打开四秒后自动关上的滑动门（Return After），有人挡路时会退回去，并发出蜂鸣声。门前有一个触发器，会为猎人按下门的按钮（Press）。一扇平开门，两个按钮中任意一个都能把它打开或关上（Reverse）。 |
| 02 Elevator（北） | 三层楼：每层都有呼叫按钮，轿厢里有随之移动的按钮（Go To Stop，数字为楼层），启动时有电机声，每到一层响一声提示音。 |
| 03 Platforms（东） | 一个掉进去就会死的深坑。坑上方来回行驶的渡船（PingPong），以及让它停下和继续的按钮（Pause、Resume）；中间有一个旋转的转盘（Loop），可以开关（Toggle）。从渡船上跳起来：你会保持它的速度。 |
| 04 Hazards（西） | 一台压碎机（When Blocked: Crush）。在随机时间喷火的火焰口：计时器触发一个中继，中继打开三个伤害区域和火焰，一秒半后再关上。一根会烫伤人的蒸汽管。一个能让这一切暂停的电源开关。 |
| 05 Vault（东北） | 三个符文，每个只计一次（Add，符文随后关闭自己）；集齐三个时，金库的按钮解锁。锁定时按下会发出蜂鸣声。一个诱饵符文会扣掉一个（Subtract）；猎人的万能钥匙会直接设定计数（Set Value）；重置会把一切再次关闭（Reset Count、Lock、Back、Enable）。 |
| 06 Teleports（西北） | 两个互相传送玩家的传送台。一个没有门的房间：传送台把你送进去，里面的按钮只把你一个人送出来（已禁用的传送器上的 Teleport Activator）。一条供建模者使用的捷径，通往东南房间的高台。 |
| 07 Triggers and Toggles（东南） | 一块压力板：只要有人站在上面（Occupied、Emptied），就会出现一座通往高台的桥。桥的栏杆是桥的 Toggle 里面的另一个 Toggle。一个每回合在猎人进来时响一次的警报。一块面板后面的隐藏角落。 |
| 08 Sounds and Effects（西南） | 每种特效都放在一个底座上，配有各自的按钮。一台有两首曲子的点唱机，每首播放完毕时会启动另一首（Finished）。一段整张地图都能听到的广播，每回合播放一次。一个只伤害按下它的人的按钮（Hurt Activator）。 |

## 规则

游戏在让 Unity 打开地图之前会先读取它，并拒绝任何不符合这些规则的内容。**Map Kit > Validate** 会在编辑器中检查所有规则；**Pack and Test** 会用游戏自己的代码再检查一遍打包好的文件。

- **组件**：Mesh Filter、Mesh Renderer、LOD Group、Box、Sphere、Capsule 和 Mesh Collider、Light、Reflection Probe、Light Probe Group、Occlusion Area 和 Portal、Adaptive Probe Volume、Volume，以及 Map Kit 自己的组件（Map Marker、Map Mode Object、Map Bounds Volume、Penetrable Surface、Probe Volume Region、Kit Map Descriptor、Map Ladder、Map Variant Group、Map Variant Option）和地图逻辑组件。不能有你自己的脚本，也不能有摄像机、音频源、粒子系统、UI、视频、地形、动画或物理刚体。声音和特效通过 Map Sound 和 Map Effect 实现。
- **着色器**：Universal Render Pipeline Lit、Simple Lit、Unlit、Baked Lit。天空：Skybox Procedural、Cubemap、6 Sided 或 Panoramic。
- **Layer**：Default、Ignore Raycast、MapBlocker（不可见的墙）、GuessMapBlocker。
- **光照**：同上。最多 64 个灯光。
- **大小**：地图包最大 256 MB，纹理最多 512 MB，单张纹理的最长边最多 8192 像素，最多 300 万个三角形、600 万个顶点和 10,000 个 Renderer。
- **名称**：地图的 ID 是场景名称的小写形式：只能包含字母、数字、`-` 和 `_`，最多 23 个字符。游戏自己使用的场景名称（Startup、Menu、Game、HunterRoom 等，以及任何以 `Map_` 或 `GameMode_` 开头的名称）不能使用。

出现错误意味着游戏不会打开这张地图。警告值得看一看，但不会阻止游戏打开地图。

## 和朋友一起玩

同一场游戏中的每个人都必须拥有完全相同的地图文件：游戏会在加载地图之前比对这些文件。地图包在所有系统上都是同一个，所以只需打包一次，然后把 Map Kit 中 `Builds/Maps/<id>/` 这个文件夹发给朋友（压缩成 zip）。每个人都把这个文件夹原封不动地放进自己游戏的地图文件夹：

| 系统 | 文件夹 |
|---|---|
| Windows | `%USERPROFILE%\AppData\LocalLow\DevAdam\MimicBusters\Maps\Local\` |
| macOS | `~/Library/Application Support/DevAdam/MimicBusters/Maps/Local/` |
| Linux | `~/.config/unity3d/DevAdam/MimicBusters/Maps/Local/` |

放好之后应该是 `...\Maps\Local\<id>\map.json`，旁边还有 `map.any.bundle`。之后这张地图就会出现在游戏地图选择的**社区**分类下。Steam 创意工坊支持会在以后推出，届时可以直接在游戏内使用。

## 遇到问题时

- **Map Kit 提示游戏会拒绝这张地图**。原因列在打包并测试窗口中。完整报告在 Console 中，也在 `Builds/Maps/<id>.report.txt` 里。
- **游戏提示地图无法打开**。在游戏中开启**设置 > 游戏 > 开发者控制台**，用 Esc 下方的按键打开控制台，然后输入 `kitmaps`：它会列出你的地图，以及某张地图无法游玩的原因。`kitinspect <id>` 可以在不加载地图的情况下读取它。
- **地图看起来太暗或光照不对**。重新 Bake 光照，然后重新打包。如果验证提示该场景使用了另一个场景的光照数据（复制的场景会出现这种情况），请使用 **Map Kit > Tools > Detach Copied Lighting**，然后再 Bake。
- **玩家穿过地面掉下去，或者在墙里出生**。检查 Collider，移动出生点，然后重新 Bake 地图边界。
- **游戏提示这张地图是用旧版 Map Kit 制作的**。下载新版 Map Kit，在其中打开你的地图，然后重新打包。

## 许可协议

请参阅 [LICENSE.md](LICENSE.md)。Mimic Busters 及其标志归 DevAdam 所有，不在该许可范围内。

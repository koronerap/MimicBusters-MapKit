# Mimic Busters Map Kit

在 Unity 裡為 **Mimic Busters** 製作你自己的地圖，然後一鍵進入遊戲，親自在地圖裡走走看。

<!-- languages -->
🌐 [English](README.md) · [Türkçe](README.tr.md) · [Deutsch](README.de.md) · [Español](README.es.md) · [Français](README.fr.md) · [Italiano](README.it.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [Українська](README.uk.md) · [Ελληνικά](README.el.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [简体中文](README.zh-Hans.md) · **繁體中文** · [العربية](README.ar.md)

Map Kit 是一個 Unity 專案，使用的地圖程式碼和遊戲完全相同。你把地圖當成一般的 Unity 場景來搭建，在 Unity 中 Bake 光照；接著 Map Kit 會依照遊戲的規則檢查它，把它打包成 Windows、Linux 和 macOS 都能開啟的單一檔案，安裝到你的遊戲裡，並直接在這張地圖上啟動遊戲。

用 Map Kit 做出來的地圖可以在這些模式中遊玩：**Standard**（遊戲中顯示為 Classic；獵人對建模者，也包含使用相同標記的兩種變體：夥伴與 Infestation）、**練習**（Map Kit 中顯示為 Practice；自由建造，沒有回合）、**Deathmatch**（人人有槍，不分陣營）以及**猜猜擬態**（輪流進行，一位玩家建模，其他人來猜）。

## 需要準備的東西

- 從 Unity Hub 安裝的 **Unity 6000.3.8f1**，一定要是這個版本。地圖是 Unity 的 asset bundle，而遊戲只能讀取由它自己所用的 Unity 版本建置的 bundle。不需要額外的模組：Map Kit 只用編輯器內建的功能打包。
- 透過 Steam 安裝的 **Mimic Busters**，用來測試你的地圖。

## 開始使用

1. 下載 Map Kit：在 GitHub 上點選 **Code > Download ZIP**，或是從 **Releases** 下載 zip 檔。把它解壓縮到路徑較短的位置。
2. 在 Unity Hub 中選擇 **Add > Add project from disk**，選取解壓縮出來的資料夾，用 6000.3.8f1 開啟。第一次匯入需要幾分鐘。完成後會開啟**歡迎**視窗：在這裡選擇你的語言（Map Kit 支援英文、土耳其文、德文、西班牙文、法文、義大利文、巴西葡萄牙文、俄文、烏克蘭文、希臘文、日文、韓文和中文），並確認它已經找到遊戲。之後可以從 **Map Kit > Welcome** 再次開啟這個視窗，也可以隨時在 **Map Kit > Settings** 中，或點選任何 Map Kit 視窗裡的齒輪圖示來變更語言。
3. **Map Kit > New Map**。替地圖取個名字，勾選它的模式；如果這是你的第一張地圖，請保持勾選**加入範例房間**。Map Kit 會在 `Assets/Maps/<Name>/` 中建立一個場景，遊戲需要的一切都已經準備好了：地圖資訊、出生點、地圖邊界、一個 probe volume，以及正確的光照設定。
4. **Window > Rendering > Lighting > Generate Lighting**。第一次 Bake 會花一些時間。
5. **Map Kit > Validate**。結果應該會顯示「遊戲會開啟這張地圖。」
6. **Map Kit > Pack and Test**。第一次使用時，先看看**遊戲**區塊：Map Kit 會在你的 Steam 收藏庫中尋找遊戲；如果遊戲裝在其他地方，請按 **選擇...** 並選取遊戲的執行檔。接著按下**打包並測試**。遊戲會直接啟動，在你的地圖上開一場只有你自己的練習。

接下來就把它變成你自己的地圖：刪除範例房間，匯入你自己的模型，然後重複第 4 到第 6 步。

## 搭建地圖

**幾何物件**。像平常一樣匯入模型（FBX、OBJ、Blender 檔案）和貼圖，並把它們放進場景。請使用 Universal Render Pipeline 的著色器：**Lit**、**Simple Lit**、**Unlit** 或 **Baked Lit**。玩家會踩到或撞到的所有東西都要加上 Collider。把不會移動的幾何物件標記為 **Static**，這樣它才會得到 Bake 好的光照。

**地圖資訊**。新場景中的 `Map` 物件上有一個 **Kit Map Descriptor**：裡面是地圖的名稱、說明、作者和模式，遊戲會照這些內容顯示。場景中必須剛好有一個。

**出生點**。出生點是一個帶有 **Map Marker** 的空物件。它的 Inspector 會依序問兩件事：**1. 模式**，哪些遊戲模式會使用它；**2. 類型**，它是做什麼用的。下方會顯示哪個模式把這個標記當成什麼來用；如果某個類型沒有任何已勾選的模式會讀取，也會提出警告：

| 類型 | 用途 |
|---|---|
| HiderSpawn | 建模者在回合開始時所在的位置（Standard）。練習模式中的玩家也會出現在這裡。 |
| HunterSpawn | 狩獵開始時獵人出來的位置（Standard）。準備階段他們會待在遊戲本身的獵人房間裡。 |
| LobbySpawn | 大廳等待玩家到齊時，所有人等候的位置。可選用：沒有的話，大廳會使用 HiderSpawn。 |
| HunterDoor | 可選用。在大廳和準備階段保持關閉、狩獵開始時打開的物件。只有第一個有效。 |
| ModelerSpawn | 練習：玩家出現的位置，和 HiderSpawn、LobbySpawn 一樣。猜猜擬態：輪到的玩家進行建模的位置。 |
| ArenaSpawn | 可選用。1v1 決鬥位置（Standard）。遊戲的獵人房間有自己的決鬥位置。 |
| GuessComputer | 可選用（猜猜擬態）。遊戲擺放電腦桌的位置；輪到的玩家坐在桌前，他的模型則站在 ModelerSpawn 上。只有第一個有效。沒有的話，玩家會藏在自己的模型裡。 |

Standard 至少需要一個 HunterSpawn 和一個 HiderSpawn。練習模式需要一個勾選了 Practice 的 HiderSpawn、LobbySpawn 或 ModelerSpawn。Deathmatch 會使用所有勾選了 Deathmatch 的 HunterSpawn、HiderSpawn 和 LobbySpawn，並把玩家放在離所有人最遠的那一點，所以至少要放六個，而且要分散開來。猜猜擬態會把負責猜的玩家放在 LobbySpawn 上（沒有 LobbySpawn 時改用 HiderSpawn），把輪到的玩家放在一個 ModelerSpawn 上，這些全都要勾選猜猜擬態：請把那個 ModelerSpawn 放在所有人都看得到的地方。每一種都放幾個，分散開來，放在地面上，面向地圖內部。從標記延伸出來的線代表玩家面對的方向。沒有勾選任何模式的標記，不會被任何模式使用。

**地圖邊界**。**Map Bounds Volume** 是一個包住整個遊玩區域的範圍框。Bake 之後，遊戲就知道玩家可以待在哪些地方，也會把掉出地圖的人帶回來。讓這個框涵蓋玩家能到達的每個地方，上方要替跳躍和屋頂留空間，然後執行 **Map Kit > Bake Map Bounds**。每次移動牆壁、地板或出生點之後，都要重新 Bake。如果在一張封閉的地圖上，驗證說遊玩區域碰到了框的邊緣，就代表它指出的位置有一個破洞。

Bake 會從你的出生點開始，以半公尺大小的格子往外填滿空氣。只能靠傳送進入的房間，用這種方式永遠找不到：在該 Volume 的 **Seeds** 裡加入一個位於這個房間內的點，也替地圖的其餘部分加一個，因為填了 Seeds 之後，填充只會從這些點開始。如果某個出生點或傳送目的地落在外面，驗證會告訴你。只有一格寬的縫隙（牆頂上的一道縫、平台旁的一條窄縫）會被回報為可能的漏洞：縫隙要嘛完全封死，要嘛至少留一公尺寬。

**梯子**。**Map Ladder** 是一個範圍框，玩家待在框裡時就能攀爬，方式就像 Half-Life 和 Counter-Strike 一樣：朝梯子前進就會往上爬，往上或往下看決定攀爬方向，左右移動鍵可沿著梯子橫移，跳躍會放手離開。把它加到一個空物件（或梯子模型）上，然後在 Scene 視窗中用控制點調整框的大小。這個框就是攀爬中的玩家所佔的空間：梯階以及梯階前方的空氣。它的**藍色箭頭（Z）是攀爬者面對的方向**，也就是朝向梯子。攀爬方向沿著物件的上方，所以如果梯子斜靠在牆上，請將物件沿 X 軸傾斜，頂端朝向牆壁，最多 30 度。讓框的頂端碰到梯子通往的那層地板，寧可稍微高一點。模型本身可以是實心的；玩家不需要碰到它。

**地圖變體**。地圖的某些部分可以每回合都不一樣，讓獵人無法把整張地圖背起來。在一個空物件上加入 **Map Variant Group**，並把這個部分的每個版本都設為它的**直接子物件**：一條暢通的走廊、同一條被堵住的走廊、三種不同堆法的箱子。每回合遊戲只會開啟其中一個子物件，其餘全部關閉；由伺服器決定，所有人看到的都一樣。**Modes** 決定哪些模式會進行選擇（預設為 Standard）；在其他模式中，子物件會維持你在場景中擺放的樣子。這個群組的 Inspector 會列出所有版本，每個版本旁都有一個**顯示**按鈕，讓你一次搭建並檢查一個。
- **權重**：在子物件上加入 **Map Variant Option**，就能讓它出現得更頻繁或更少（沒有時為 1；設為 0 則永遠不會出現）。
- **Avoid Repeat**（預設開啟）：只要還有其他版本可選，上一回合的版本就不會再次被選中。
- **Link Id**：Link Id 相同的群組會選擇相同順序的子物件，讓同一個設計的兩個部分互相配合：這裡的門關上時，那裡的通道就會打開。
- 群組可以放在另一個群組的版本裡。
- 版本裡的出生點只會在該版本開啟的回合中使用。
- 不要把版本裡的任何東西標記為 **Occluder Static**：Occlusion Culling 會把它當成一直都在，在它關閉的回合裡，它後面的東西就會跟著消失。驗證會對此提出警告。

**光照**。保留新地圖附帶的光照設定：Mixed 光照使用 **Shadowmask** 模式、**Directional** Lightmap、**關閉 Realtime Global Illumination**。遊戲只帶有這套設定所需的著色器。燈光請使用 **Mixed** 或 **Baked**；超過四盞會產生陰影的即時燈光會讓遊戲變慢。如果想要霧，必須使用 **Exponential Squared**。**Adaptive Probe Volume** 會照亮玩家和建模者，讓他們和地圖的光線一致；它會和其餘部分一起 Bake（它是 Generate Lighting 的一部分）。每次修改幾何物件或燈光之後都要重新 Bake，否則遊戲會顯示舊的光照。

**後製處理**。**Volume** 只能帶有色彩和色調效果：Bloom、Tonemapping、Color Adjustments、White Balance、Lift Gamma Gain、Shadows Midtones Highlights、Split Toning、Channel Mixer、Color Curves、Vignette。

## 地圖邏輯

你可以像在 Hammer 裡那樣，讓地圖變得可以互動：按下按鈕就開始移動、過一陣子又自己回來的平台，一段聲音，一個會造成傷害的區域。完全不用寫程式；所有東西都在 Inspector 中連接，實際運作交給遊戲處理。

從 **Add Component > Map Kit > Logic** 加入這些元件：

| 元件 | 功能 |
|---|---|
| Map Button | 玩家用互動鍵按下的按鈕。需要 Collider。可設定誰能按、冷卻時間和提示文字。 |
| Map Mover | 讓物件及其子物件沿著一連串站點移動和旋轉，並帶著站在上面的玩家一起移動。Return After：抵達最後一站後，經過這麼多秒會自己返回。PingPong 和 Loop 會自行運轉。 |
| Map Trigger | 一個看不見的範圍框：玩家進入或離開時觸發事件，第一位進入和最後一位離開時也會觸發。 |
| Map Teleport | 把走進範圍框的人傳送到一個目的地物件，並讓他面向目的地物件所朝的方向。 |
| Map Hurt Zone | 每隔一段間隔對框內的人造成傷害，或是直接擊殺。由地圖造成的死亡不會讓任何人得分。 |
| Map Sound | 在所在位置播放的聲音：播放一次或循環播放，隨距離變小聲或在每個地方都一樣大聲。 |
| Map Effect | 遊戲內建的一種特效：煙霧、蒸氣、火焰、火花、灰塵、水滴。可調整顏色、大小和數量。 |
| Map Toggle | 顯示或隱藏物件及其子物件（渲染、碰撞、燈光）。 |
| Map Relay | 將一個輸入傳遞給多個輸出，每個輸出都有自己的延遲。 |
| Map Timer | 啟用時每隔一段間隔觸發一次事件。 |
| Map Counter | 計數，計數達到目標值時觸發事件。 |
| Map Round Events | 回合開始、狩獵開始、回合結束。 |

**連接**。每個元件都有一份**輸出**清單：一個*事件*（Pressed、ArrivedAtEnd、Entered...）、一個*目標*（另一個邏輯元件，拖曳進來即可）、一個*輸入*（Forward、Back、Play、Enable、Disable...）、一個*延遲*，以及在輸入需要時的一個數字（站點編號、數量）。Inspector 只會列出這個元件會觸發的事件和目標能接收的輸入，把無法運作的連線標成橘色，並在下方列出有哪些連線接入這個元件。**Map Kit > Logic** 會在同一頁顯示所有連線。

範例：把按鈕的 *Pressed* 事件連到電梯的 *Forward* 輸入。電梯只有一個站點（往上 1.8 公尺），Return After 設為 4：一按下去它就升起，四秒後再降下來。**New Map** 的範例房間裡就附有這個例子，另外還有馬達聲（電梯的 *Started* 連到一個 Map Sound 的 *Play*），以及另一個房間裡的暖爐：一個按鈕同時切換一個 Map Hurt Zone 和一個火焰 Map Effect。

**引發事件鏈的玩家**。由玩家引起的事件（按下按鈕、走進觸發器、受到傷害）會在整條事件鏈中記住這位玩家，延遲期間也一樣。有兩個輸入會作用在這位玩家身上：傷害區域的 *HurtActivator* 會對他造成一次該區域的傷害（或改用輸出中的數字），傳送器的 *TeleportActivator* 會把他送到傳送器的目的地。只讓按下的人掉下去的陷阱門按鈕、只傷害踩進去那個人的觸發器：只要連好線就行，不需要範圍框。只用這種方式的傳送器，開始時應該設為停用，否則它自己的範圍框也會把人傳走。

**打包前先預覽**。在 Scene 視窗中，每個輸出的連線上都會標示它的事件和輸入。選取的 Map Mover 在每個站點上都有一個控制點；在「預覽」底下還有一個半透明的副本，你可以沿著路徑拖曳它，或像遊戲裡那樣播放它，同時也會顯示走完全程需要多少時間。

每回合開始時，一切都會回到你設定的樣子。關閉 Start Enabled 的元件，在收到 *Enable* 輸入之前不會有任何動作。

**注意事項**。

- 任何會移動的東西都不能是 **Static**：static batching 會把它固定在原地，Bake 在它身上的光照也會留在原處。會移動的物件由 Probe 照明。
- 移動中的平台會推開擋路的玩家；推不動時，就會依照 **When Blocked** 的設定處理：掉頭、壓扁玩家，或是繼續前進。
- 從移動平台上跳起來的玩家，在落地前會保持平台的水平速度，所以在行進中的渡船上跳起來，還是會落回渡船上。
- 傳送目的地會像出生點一樣接受檢查：那裡不能有實心的東西，下方要有地板。位於傳送器自己範圍框內的目的地，會在一秒後把玩家再傳送一次；驗證會提出警告。
- 聲音：每段最長 2 分鐘，單聲道或立體聲，最高 48 kHz；一張地圖的所有聲音在記憶體中合計最多 32 MB。較長的聲音請用 **Compressed In Memory** 或 **Streaming** 載入。聲音會受玩家的音效音量控制。
- 沒有延遲的循環（A 觸發 B，B 又立刻觸發 A）在遊戲中會在第 32 步被截斷，驗證也會把它算成錯誤。只要替循環中的某個輸出加上延遲就好。只有會讓目標當下立刻動作的輸入才會構成這種循環（按下、中繼的觸發、計數器的變化、被派往某處的 Mover）：會關閉自己的按鈕，或播放完畢後啟動另一首的曲子，都沒有問題。
- 上限：256 個邏輯元件，每個元件 16 個輸出，整張地圖每秒 200 個輸入。

**試玩**。打包並測試會在練習模式中開啟地圖，練習模式裡沒有獵人和建模者。在開發者主控台中，`maprole hunter` 或 `maprole hider` 可以讓你試試角色篩選，`maplogic` 會列出每個元件的狀態，`mapfire <number> <input>` 可以手動送出一個輸入。

### 範例地圖：Logic Playground

**Map Kit > Examples > Logic Playground** 會在 `Assets/Maps/LogicPlayground/` 中建立一張灰盒地圖，每一種邏輯元件都在裡面運作。在它詢問時 Bake 光照，然後像其他地圖一樣打包並測試，或打開它的物件，看看它們是怎麼連接的（**Map Kit > Logic** 會列出所有連線）。顏色代表東西的用途：灰色是場景本身，橘色是可以按的東西，黃色會移動，紅色會造成傷害，藍色會傳送，紫色會出現又消失。天花板是看不見的：它是一個不繪製任何東西的 Collider，在 Bake 地圖邊界時把露天的中庭封起來。

九個房間排成 3×3 的格子，總寬 36 公尺。所有人都從中間的房間開始。

| 房間 | 展示內容 |
|---|---|
| 00 Hub（中間） | Map Round Events：回合開始時響起鈴聲；狩獵開始時響起警報，並冒出紅色煙霧信號；回合結束時放煙火（一個依延遲播放火花和聲音的中繼）。一台從一開始就循環發出嗡嗡聲的發電機。 |
| 01 Doors（南） | 一扇打開四秒後會自己關上的滑門（Return After），有人擋路時會退回去，並發出蜂鳴聲。門前有一個觸發器，會替獵人按下門的按鈕（Press）。一扇平開門，兩個按鈕中任何一個都能把它打開或關上（Reverse）。 |
| 02 Elevator（北） | 三層樓：每層都有呼叫按鈕，車廂裡有跟著移動的按鈕（Go To Stop，數字為樓層），啟動時有馬達聲，每到一層響一次鈴聲。 |
| 03 Platforms（東） | 一個掉進去就會死的深坑。在坑上來回行駛的渡船（PingPong），以及讓它停下和繼續的按鈕（Pause、Resume）；中間有一座旋轉的轉盤（Loop），可以開關（Toggle）。從渡船上跳起來：你會保持它的速度。 |
| 04 Hazards（西） | 一台壓碎機（When Blocked: Crush）。在隨機時間噴火的火焰孔：計時器觸發一個中繼，中繼開啟三個傷害區域和火焰，一秒半後再關掉。一根會燙傷人的蒸氣管。一個能讓這一切暫停的電源開關。 |
| 05 Vault（東北） | 三個符文，每個只算一次（Add，符文接著會關閉自己）；三個都到齊時，金庫的按鈕就會解鎖。鎖住時按下會發出蜂鳴聲。一個誘餌符文會扣掉一個（Subtract）；獵人的萬能鑰匙會直接設定計數（Set Value）；重設會把一切再次關上（Reset Count、Lock、Back、Enable）。 |
| 06 Teleports（西北） | 兩個互相傳送玩家的傳送台。一個沒有門的房間：傳送台把你送進去，裡面的按鈕只把你一個人送出來（停用中傳送器上的 Teleport Activator）。一條給建模者用的捷徑，通往東南房間的高台。 |
| 07 Triggers and Toggles（東南） | 一塊壓力板：只要有人站在上面（Occupied、Emptied），就會出現一座通往高台的橋。橋的欄杆是橋的 Toggle 裡面的另一個 Toggle。每回合有獵人進來時會響一次的警報。一塊面板後面的隱藏角落。 |
| 08 Sounds and Effects（西南） | 每種特效都放在一個展示台上，各有自己的按鈕。一台有兩首曲子的點唱機，每首播完時會啟動另一首（Finished）。整張地圖都聽得到的廣播，每回合播放一次。一個只傷害按下它的人的按鈕（Hurt Activator）。 |

## 規則

遊戲在讓 Unity 開啟地圖之前會先讀取它，並拒絕任何不符合這些規則的內容。**Map Kit > Validate** 會在編輯器中檢查所有規則；**Pack and Test** 會用遊戲本身的程式碼再檢查一次打包好的檔案。

- **元件**：Mesh Filter、Mesh Renderer、LOD Group、Box、Sphere、Capsule 和 Mesh Collider、Light、Reflection Probe、Light Probe Group、Occlusion Area 和 Portal、Adaptive Probe Volume、Volume，以及 Map Kit 本身的元件（Map Marker、Map Mode Object、Map Bounds Volume、Penetrable Surface、Probe Volume Region、Kit Map Descriptor、Map Ladder、Map Variant Group、Map Variant Option）和地圖邏輯元件。不能有你自己的腳本，也不能有攝影機、音訊來源、粒子系統、UI、影片、地形、動畫或物理剛體。聲音和特效要透過 Map Sound 和 Map Effect 加入。
- **著色器**：Universal Render Pipeline Lit、Simple Lit、Unlit、Baked Lit。天空：Skybox Procedural、Cubemap、6 Sided 或 Panoramic。
- **Layer**：Default、Ignore Raycast、MapBlocker（隱形牆）、GuessMapBlocker。
- **光照**：如上所述。最多 64 盞燈光。
- **大小**：地圖包最大 256 MB，貼圖最多 512 MB，單張貼圖最長邊最多 8192 像素，最多 300 萬個三角形、600 萬個頂點和 10,000 個 Renderer。
- **名稱**：地圖的 ID 是場景名稱的小寫形式：只能使用字母、數字、`-` 和 `_`，最多 23 個字元。遊戲本身使用的場景名稱（Startup、Menu、Game、HunterRoom 等等，以及任何以 `Map_` 或 `GameMode_` 開頭的名稱）都不能使用。

出現錯誤代表遊戲不會開啟這張地圖。警告值得看一下，但不會讓遊戲拒絕開啟地圖。

## 和朋友一起玩

同一場遊戲中的每個人都必須擁有完全相同的地圖檔案：遊戲在載入地圖之前會先比對這些檔案。地圖包在每個系統上都是同一份，所以只要打包一次，再把 Map Kit 裡的 `Builds/Maps/<id>/` 資料夾傳給朋友（壓縮成 zip）。每個人都把這個資料夾原封不動地放進自己遊戲的地圖資料夾：

| 系統 | 資料夾 |
|---|---|
| Windows | `%USERPROFILE%\AppData\LocalLow\DevAdam\MimicBusters\Maps\Local\` |
| macOS | `~/Library/Application Support/DevAdam/MimicBusters/Maps/Local/` |
| Linux | `~/.config/unity3d/DevAdam/MimicBusters/Maps/Local/` |

放好之後應該會是 `...\Maps\Local\<id>\map.json`，旁邊還有 `map.any.bundle`。接著這張地圖就會出現在遊戲地圖選擇的**社群**分類下。Steam 工作坊支援之後才會推出，屆時可以直接在遊戲內使用。

## 遇到問題時

- **Map Kit 說遊戲會拒絕這張地圖**。原因會列在打包並測試視窗中。完整報告在 Console 中，也在 `Builds/Maps/<id>.report.txt` 裡。
- **遊戲說地圖無法開啟**。在遊戲中開啟**設定 > 遊戲 > 開發者主控台**，用 Esc 下方的按鍵打開主控台，然後輸入 `kitmaps`：它會列出你的地圖，以及某張地圖為什麼無法遊玩。`kitinspect <id>` 可以在不載入地圖的情況下讀取它。
- **地圖看起來太暗或光線不對**。重新 Bake 光照，然後重新打包。如果驗證說場景使用了另一個場景的光照資料（複製的場景會發生這種情況），請使用 **Map Kit > Tools > Detach Copied Lighting**，然後再 Bake。
- **玩家穿過地板掉下去，或在牆裡出生**。檢查 Collider，移動出生點，然後重新 Bake 地圖邊界。
- **遊戲回報這張地圖是用舊版 Map Kit 製作的**。下載新版 Map Kit，用它開啟你的地圖，然後重新打包。

## 授權

請參閱 [LICENSE.md](LICENSE.md)。Mimic Busters 及其標誌屬於 DevAdam 所有，不在本授權範圍內。

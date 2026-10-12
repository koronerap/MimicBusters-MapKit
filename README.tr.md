# Mimic Busters Harita Kiti

**Mimic Busters** için Unity'de kendi haritanı yap, sonra tek tıkla oyunda gez.

<!-- languages -->
🌐 [English](README.md) · **Türkçe** · [Deutsch](README.de.md) · [Español](README.es.md) · [Français](README.fr.md) · [Italiano](README.it.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [Українська](README.uk.md) · [Ελληνικά](README.el.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [简体中文](README.zh-Hans.md) · [繁體中文](README.zh-Hant.md) · [العربية](README.ar.md)

Kit, oyunun kullandığı harita koduyla gelen bir Unity projesi. Haritayı sıradan bir Unity sahnesi olarak
kurarsın, ışığını Unity'de bake edersin; kit onu oyunun kurallarıyla denetler, Windows, Linux ve macOS'un
aynı şekilde açtığı tek bir dosyaya paketler, oyununa kurar ve oyunu o haritada başlatır.

Bu kitle yapılan haritalar **Standard** (Avcılar ve Modelciler, aynı işaretçileri kullanan Companion ve
Infestation dahil), **Practice** (serbest yapım, round yok), **Deathmatch** (herkes silahlı, taraf yok) ve
**Taklidi Tahmin Et** (sırayla biri modeller, diğerleri tahmin eder) modlarında oynanır.

## Gerekenler

- **Unity 6000.3.8f1**, tam olarak bu sürüm, Unity Hub'dan. Haritalar Unity asset bundle'ıdır ve oyun
  yalnızca kendi Unity sürümünün yaptığı bundle'ları okuyabilir. Ek modül gerekmez: kit, editörle gelenle
  paketler.
- Haritaları denemek için Steam'den kurulu **Mimic Busters**.

## Başlarken

1. Kiti indir: GitHub'da **Code > Download ZIP** ya da **Releases** altındaki zip. Kısa bir yola aç.
2. Unity Hub'da **Add > Add project from disk**, açtığın klasörü seç, 6000.3.8f1 ile aç. İlk içe aktarma
   birkaç dakika sürer.
   Bitince **Hoş geldin** penceresi açılır: dilini orada seç (kit Türkçe, İngilizce, Almanca, İspanyolca,
   Fransızca, İtalyanca, Brezilya Portekizcesi, Rusça, Ukraynaca, Yunanca, Japonca, Korece ve Çince
   konuşur) ve oyunu bulup bulmadığına bak. Pencereyi **Map Kit > Welcome** ile yeniden açabilir, dili sonra
   **Map Kit > Settings**'ten ya da kit pencerelerindeki dişli simgesinden değiştirebilirsin.
3. **Map Kit > New Map**. Haritaya bir ad ver, modlarını seç, ilk haritanda **Örnek oda ekle** açık kalsın.
   Kit `Assets/Maps/<Ad>/` altına oyunun istediği her şeyi hazır bir sahne kurar: harita bilgisi, doğma
   noktaları, harita sınırı, probe volume ve doğru ışık ayarları.
4. **Window > Rendering > Lighting > Generate Lighting**. İlk bake biraz sürer.
5. **Map Kit > Validate**. "Oyun bu haritayı açar" demeli.
6. **Map Kit > Pack and Test**. İlk seferde **Oyun** bölümüne bak: kit oyunu Steam kütüphanelerinde
   arar; başka bir yerdeyse **Seç...** ile oyunun exe'sini göster. Sonra **Paketle ve Test Et**. Oyun
   doğrudan haritanda, sana özel bir Practice oturumunda açılır.

Sonra haritayı kendin yap: örnek odayı sil, kendi modellerini getir, 4-6. adımları tekrarla.

## Harita kurmak

**Geometri.** Modelleri (FBX, OBJ, Blender dosyaları) ve dokuları her zamanki gibi içeri al, sahneye
yerleştir. Universal Render Pipeline shader'larını kullan: **Lit**, **Simple Lit**, **Unlit** ya da
**Baked Lit**. Oyuncunun üstünde yürüdüğü ya da çarptığı her şeye collider ver. Hareket etmeyen
geometriyi **Static** işaretle ki bake edilmiş ışık alsın.

**Harita bilgisi.** Yeni sahnedeki `Map` objesinde bir **Kit Map Descriptor** var: haritanın oyunda
görünen adı, açıklaması, yapımcısı ve modları. Tam olarak bir tane olmalı.

**Doğma noktaları.** Doğma noktası, üzerinde **Map Marker** olan boş bir obje. Inspector'ı iki şeyi sırayla
sorar: **1. Modlar**, hangi modlar kullanır; **2. Türler**, ne için. Altında hangi modun işaretçiyi ne
olarak kullandığını yazar ve seçili modların hiçbirinin okumadığı bir tür varsa uyarır:

| Tür | Ne için |
|---|---|
| HiderSpawn | Modelcilerin round başında konduğu yer (Standard). Practice'te oyuncular da burada belirir. |
| HunterSpawn | Av başlayınca Avcıların çıktığı yer (Standard). Hazırlığı oyunun kendi Avcı odasında geçirirler. |
| LobbySpawn | Lobi dolarken herkesin beklediği yer. İsteğe bağlı: yoksa lobi HiderSpawn'ları kullanır. |
| HunterDoor | İsteğe bağlı. Lobide ve hazırlıkta kapalı duran, av başlayınca açılan obje. Yalnızca ilki sayılır. |
| ModelerSpawn | Practice: oyuncuların belirdiği yer, HiderSpawn ve LobbySpawn gibi. Taklidi Tahmin Et: sırası gelen oyuncunun modellediği yer. |
| ArenaSpawn | İsteğe bağlı. 1v1 düello noktası (Standard). Oyunun Avcı odasında kendi noktaları var. |
| GuessComputer | İsteğe bağlı (Taklidi Tahmin Et). Oyunun bilgisayar masasını koyduğu yer; sırası gelen oyuncu, modeli ModelerSpawn'da dururken bu masada oturur. Yalnızca ilki sayılır. Yoksa oyuncu modelinin içinde gizlidir. |

Standard için en az bir HunterSpawn ve bir HiderSpawn gerekir. Practice için Practice işaretli bir
HiderSpawn, LobbySpawn ya da ModelerSpawn gerekir. Deathmatch, Deathmatch işaretli bütün HunterSpawn,
HiderSpawn ve LobbySpawn noktalarını kullanır ve oyuncuyu herkesten en uzak olana koyar; en az altı,
dağınık koy. Taklidi Tahmin Et modu tahmin edenleri LobbySpawn'lara (yoksa HiderSpawn'lara), sırası gelen oyuncuyu tek bir
ModelerSpawn'a koyar, hepsi Taklidi Tahmin Et işaretli; o noktayı herkesin görebileceği bir yere koy. Her birinden birkaç tane koy: dağınık, zeminde, yüzü haritaya dönük. İşaretçiden çıkan
çizgi oyuncunun bakacağı yönü gösterir. Hiçbir modu seçili olmayan işaretçiyi hiçbir mod kullanmaz.

**Harita sınırı.** **Map Bounds Volume** bütün oyun alanını saran bir kutu. Bake edilince oyun,
oyuncuların nerede olabileceğini bilir ve haritadan düşeni geri alır. Kutu, oyuncunun ulaşabildiği her
yeri, zıplamalar ve çatılar için üstte pay bırakarak kaplasın; sonra **Map Kit > Bake Map Bounds**. Duvar,
zemin ya da doğma noktası her değiştiğinde yeniden bake et. Kapalı bir haritada doğrulayıcı "oyun alanı
kutunun duvarına ulaşıyor" diyorsa, söylediği yerde bir delik var.

Bake, havayı doğma noktalarından dışarı doğru yarım metrelik hücrelerle doldurur. Oyuncuların yalnızca
ışınlanarak girdiği bir oda böyle hiç bulunmaz: hacmin **Seeds** listesine o odada bir nokta ekle, haritanın
geri kalanı için de bir tane; çünkü Seeds doluyken dolgu yalnızca onlardan başlar. Bir doğma noktası ya da
ışınlayıcı hedefi dışarıda kalırsa doğrulayıcı söyler. Tek hücre genişliğindeki boşluk (duvar üstünde bir
yarık, platformun yanında dar bir çatlak) sızıntı adayı sayılır: boşlukları ya sıkı ya en az bir metre yap.

**Merdivenler.** **Map Ladder**, içindeki oyuncunun tırmandığı bir kutudur; Half-Life ve Counter-Strike'taki
gibi: merdivene doğru ilerlemek tırmandırır, yukarı ya da aşağı bakmak yönü seçer, yan tuşlar yana kaydırır,
zıplamak bırakır. Boş bir objeye (ya da merdiven modeline) ekle, kutuyu Scene görünümünde tutamaçlarla
boyutlandır. Kutu tırmanan oyuncunun durduğu boşluktur: basamaklar ve önlerindeki hava. **Mavi ok (Z)
tırmananın baktığı yöndür**, merdivene doğru. Tırmanış objenin yukarısı boyuncadır; duvara yaslı merdiven
için objeyi X'te, tepesi duvara doğru en çok 30 derece yatır. Kutunun üstü merdivenin çıktığı katın zeminine
kadar, hatta biraz yukarısına uzansın. Modelin kendisi katı olabilir, oyuncunun ona değmesi gerekmez.

**Harita varyasyonları.** Haritanın bazı parçaları her round farklı gelebilir; Avcılar haritayı ezberleyemez.
Boş bir objeye **Map Variant Group** koy, o parçanın her versiyonunu onun **doğrudan çocuğu** yap: koridor
açık, aynı koridor kapalı, kasalar üç ayrı dizilişte. Oyun her round çocuklardan BİRİNİ açar, diğerlerini
kapatır; seçimi sunucu yapar, herkes aynısını görür. **Modes** hangi modlarda seçileceğini söyler
(varsayılan Standard); diğer modlarda çocuklar sahnede bıraktığın gibi kalır. Grubun Inspector'ı versiyonları
listeler, her birinde tek tek kurup kontrol etmek için **Göster** butonu vardır.
- **Ağırlık:** bir çocuğa **Map Variant Option** ekleyerek daha sık ya da seyrek gelmesini sağla (yoksa 1;
  0 = hiç gelmez).
- **Avoid Repeat** (varsayılan açık): geçen roundun versiyonu, başkası varken yeniden seçilmez.
- **Link Id:** aynı kimliği taşıyan gruplar aynı sıradaki çocuğu seçer; bir fikrin iki yarısı uyuşur:
  burada kapı kapalıyken orada geçit açık.
- Gruplar başka bir grubun versiyonlarının içinde olabilir.
- Bir versiyonun içindeki doğma noktaları yalnızca o versiyonun açık olduğu roundlarda kullanılır.
- Bir versiyonun içindeki hiçbir şeyi **Occluder Static** işaretleme: occlusion culling onu hep orada sayar,
  kapalı olduğu roundlarda arkasındaki şeyler görünmez olur. Doğrulayıcı bunu uyarır.

**Işık.** Yeni haritanın geldiği ışık ayarlarını koru: Mixed ışık **Shadowmask** modunda,
**Directional** lightmap, **Realtime Global Illumination kapalı**. Oyun yalnızca bu düzenin
shader'larını taşıyor. Işıklar **Mixed** ya da **Baked** olsun; gölgeli dörtten fazla gerçek zamanlı ışık
oyunu yavaşlatır. Sis istersen **Exponential Squared** olmalı. **Adaptive Probe Volume** oyuncuları ve
Modelcileri haritaya uygun aydınlatır; geri kalanla birlikte bake edilir (Generate Lighting'in parçası).
Geometriye ya da ışıklara her dokunuşta yeniden bake et, yoksa oyun eski ışığı gösterir.

**Post-processing.** Bir **Volume** yalnızca renk ve ton efektleri taşıyabilir: Bloom, Tonemapping, Color
Adjustments, White Balance, Lift Gamma Gain, Shadows Midtones Highlights, Split Toning, Channel Mixer,
Color Curves, Vignette.

## Harita mantığı

Haritana Hammer'daki gibi etkileşim ekleyebilirsin: basılınca giden ve bir süre sonra geri dönen bir
platform, çalan bir ses, hasar veren bir bölge. Kod yazılmaz; her şey Inspector'da bağlanır, işi oyun
yapar.

Bir objeye **Add Component > Map Kit > Logic** altından ekle:

| Bileşen | Ne yapar |
|---|---|
| Map Button | Oyuncunun etkileşim tuşuyla bastığı düğme. Collider gerekir. Kim basabilir, bekleme süresi, istem. |
| Map Mover | Objeyi çocuklarıyla bir durak listesi boyunca taşır ve döndürür, üstündeki oyuncuları da. Return After: son durağa vardıktan o kadar saniye sonra kendiliğinden döner. PingPong ve Loop kendi kendine gider gelir. |
| Map Trigger | Görünmez bir kutu: oyuncu girince, çıkınca, ilk giren ve son çıkan için olay verir. |
| Map Teleport | Kutusuna gireni bir hedef objenin yerine, onun baktığı yöne ışınlar. |
| Map Hurt Zone | Kutunun içindekilere her aralıkta hasar verir, istersen anında öldürür. Haritanın öldürdüğü oyuncu için kimse puan almaz. |
| Map Sound | Kendi yerinde bir ses: bir kez ya da döngü, uzaklaştıkça kısılan ya da her yerde aynı. |
| Map Effect | Oyunun hazır efektlerinden biri: duman, buhar, ateş, kıvılcım, toz, damla. Renk, boyut, yoğunluk. |
| Map Toggle | Objeyi çocuklarıyla (görüntü, collider, ışık) gösterir ya da gizler. |
| Map Relay | Bir girişi, her biri kendi gecikmesiyle, birkaç çıkışa dağıtır. |
| Map Timer | Açıkken her aralıkta olay verir. |
| Map Counter | Sayar; hedefe varınca olay verir. |
| Map Round Events | Round başladı, av başladı, round bitti. |

**Bağlamak.** Her bileşenin bir **Çıkışlar** listesi var: *olay* (Pressed, ArrivedAtEnd, Entered...),
*hedef* (başka bir mantık bileşeni, sürükleyip bırak), *giriş* (Forward, Back, Play, Enable, Disable...),
bir *gecikme* ve gerekirse bir sayı (durak numarası, miktar). Inspector yalnızca bileşenin verdiği
olayları ve hedefin aldığı girişleri sunar, çalışmayacak bir bağlantıyı turuncuyla yazar ve altında o
bileşene gelenleri listeler. **Map Kit > Logic** bütün bağlantıları tek sayfada gösterir.

Örnek: düğmenin *Pressed* olayı, asansörün *Forward* girişine. Asansörde tek durak (1,8 m yukarı) ve
Return After 4: basınca çıkar, dört saniye sonra iner. **New Map**'in örnek odasında bu hazır gelir;
yanında bir motor sesi (asansörün *Started* olayı bir Map Sound'un *Play* girişine) ve öbür odada bir
ısıtıcı: tek düğme bir Map Hurt Zone'u ve ateşli bir Map Effect'i birlikte açıp kapatır.

**Zinciri başlatan oyuncu.** Bir oyuncunun yol açtığı olay (basmak, bir kutuya girmek, hasar almak) o
oyuncuyu bütün zincir boyunca, gecikmeler dahil, hatırlar. İki giriş ona etki eder: hasar bölgesinin
*HurtActivator* girişi ona bölgenin hasarını bir kez verir (ya da çıkıştaki sayıyı), ışınlayıcının
*TeleportActivator* girişi onu hedefe gönderir. Yalnızca basanı aşağı atan bir tuzak kapı düğmesi, yalnızca
basanı yakan bir tetik: kutu gerekmez, bağlaman yeter. Yalnızca böyle kullanılacak bir ışınlayıcıyı kapalı
başlat, yoksa kendi kutusu da ışınlar.

**Paketlemeden görmek.** Scene görünümünde her çıkışın çizgisinde olayı ve girişi yazar. Seçili bir Map
Mover'ın her durağında bir tutamaç, Önizleme altında da yarı saydam bir kopyası var: yol boyunca
kaydırabilir ya da oyundaki gibi oynatabilirsin; yolculuğun kaç saniye sürdüğünü de yazar.

Her round başında her şey yazdığın hâline döner. Start Enabled kapalı bir bileşen, bir *Enable* girişi
gelene kadar tepki vermez.

**Dikkat.**

- Hareket eden hiçbir şey **Static** olamaz: static batching onu yerinde dondurur, bake edilen ışığı da
  geride kalır. Hareket edenleri probe'lar aydınlatır.
- Hareket eden bir platform yolundaki oyuncuyu iter; itemezse **When Blocked** ne diyorsa onu yapar:
  geri döner, ezer ya da aldırmaz.
- Hareket eden bir platformdan zıplayan oyuncu, yere inene kadar platformun yandan hızını korur; giden
  bir salın üstünde zıplayan yine salın üstüne düşer.
- Işınlayıcının hedefi bir doğma noktası gibi denetlenir: içinde katı bir şey olmamalı, altında zemin
  olmalı. Bir ışınlayıcının kutusundaki hedef, oyuncuyu bir saniye sonra yine ışınlar; doğrulayıcı uyarır.
- Sesler: tek ses en çok 2 dakika, mono ya da stereo, en çok 48 kHz; bütün sesler bellekte en çok 32 MB.
  Uzun sesleri **Compressed In Memory** ya da **Streaming** yükle. Sesler oyuncunun efekt sesi ayarından
  geçer.
- Gecikmesiz bir döngü (A B'yi, B A'yı aynı anda tetikliyor) oyunda 32 adımda kesilir; doğrulayıcı onu
  hata sayar. Döngüdeki bir çıkışa gecikme ver. Döngüyü yalnızca hedefini o an harekete geçiren girişler
  kurar (basış, rölenin tetiği, sayacın değişmesi, bir yere gönderilen hareketli): kendini kapatan düğme ya
  da bitince ötekini başlatan müzik sorun değil.
- Sınırlar: en çok 256 mantık bileşeni, bileşen başına 16 çıkış, bütün harita için saniyede 200 giriş.

**Denemek.** Paketle ve Test Et haritayı Practice'te açar, orada Avcı ve Modelci yok. Geliştirici
konsolunda `maprole hunter` ya da `maprole hider` rol süzgeçlerini denemeni sağlar, `maplogic` her
bileşenin durumunu listeler, `mapfire <numara> <giriş>` bir girişi elle gönderir.

### Örnek harita: Logic Playground

**Map Kit > Examples > Logic Playground**, her mantık bileşeninin kullanıldığı greybox bir haritayı
`Assets/Maps/LogicPlayground/` altına kurar. Sorduğunda ışığını bake et, sonra her harita gibi Paketle ve
Test Et, ya da objelerini açıp nasıl bağlandıklarına bak (**Map Kit > Logic** bütün bağlantıları
listeler). Renkler bir şeyin ne olduğunu söyler: gri dünya, turuncu basılan, sarı hareket eden, kırmızı
yakan, mavi ışınlayan, mor görünüp kaybolan. Tavan görünmez: hiçbir şey çizmeyen bir collider, açık
avluyu harita sınırı bake'i için kapatır.

3'e 3 dizilmiş dokuz oda, 36 m genişlik. Herkes ortadakinde başlar.

| Oda | Ne gösterir |
|---|---|
| 00 Hub (orta) | Map Round Events: round başlayınca bir melodi; av başlayınca siren ve kırmızı duman; round bitince havai fişek (kıvılcımları ve sesi gecikmelerle çalan bir röle). Baştan beri döngüde uğuldayan bir jeneratör. |
| 01 Doors (güney) | Açıldıktan dört saniye sonra kendiliğinden kapanan (Return After), yolda biri varsa geri dönüp vızlayan sürgülü kapı. Önündeki tetik, Avcılar için düğmesine basar (Press). İki düğmeden biriyle açılıp kapanan menteşeli kapı (Reverse). |
| 02 Elevator (kuzey) | Üç kat: her katta çağırma düğmesi, kabinle birlikte giden düğmeler (Go To Stop, sayı kat numarası), kalkarken motor sesi, her katta zil. |
| 03 Platforms (doğu) | Düşeni öldüren bir çukur. Üstünde gidip gelen sal (PingPong) ve onu durdurup yürüten düğmeler (Pause, Resume); ortada dönen atlıkarınca (Loop), düğmeyle açılıp kapanır (Toggle). Saldan zıpla: hızını korursun. |
| 04 Hazards (batı) | Ezici (When Blocked: Crush). Rastgele aralıklarla yanan ateş delikleri: bir zamanlayıcı bir röleyi tetikler, röle üç hasar bölgesiyle ateşi açar ve bir buçuk saniye sonra kapatır. Yakan buhar borusu. Hepsini duraklatan güç düğmesi. |
| 05 Vault (kuzeydoğu) | Her biri bir kez sayılan üç rün (Add, ve rün kendini kapatır); üçte kasanın düğmesinin kilidi açılır. Kilitliyken basılırsa vızlar. Tuzak rün birini geri alır (Subtract); Avcıların ana anahtarı sayıyı bir anda yazar (Set Value); sıfırlama her şeyi kapatır (Reset Count, Lock, Back, Enable). |
| 06 Teleports (kuzeybatı) | Oyuncuları birbirine gönderen iki ped. Kapısı olmayan bir oda: ped içeri alır, içerideki düğme yalnızca basanı dışarı çıkarır (kapalı bir ışınlayıcının Teleport Activator girişi). Modelcilere güneydoğu odasının çıkıntısına kısayol. |
| 07 Triggers and Toggles (güneydoğu) | Basınç plakası: üstünde biri durdukça (Occupied, Emptied) çıkıntıya bir köprü var. Köprünün korkuluğu, köprünün toggle'ının içinde bir toggle. Bir Avcı girince round'da bir kez çalan alarm. Bir panelin arkasında gizli bir köşe. |
| 08 Sounds and Effects (güneybatı) | Her efekt kendi kaidesinde, kendi düğmesiyle. Biri bitince ötekini başlatan iki parçalı müzik kutusu (Finished). Bütün haritada duyulan, round'da bir kez çalınan anons. Yalnızca basanı yakan düğme (Hurt Activator). |

## Kurallar

Oyun bir haritayı Unity'nin açmasına izin vermeden önce okur ve bu kuralların dışındaki her şeyi
reddeder. **Map Kit > Validate** hepsini editörde denetler; **Pack and Test** paketlenmiş dosyaları
oyunun kendi koduyla bir kez daha denetler.

- **Bileşenler:** Mesh Filter, Mesh Renderer, LOD Group, Box, Sphere, Capsule ve Mesh Collider, Light,
  Reflection Probe, Light Probe Group, Occlusion Area ve Portal, Adaptive Probe Volume, Volume ve kitin
  kendi bileşenleri (Map Marker, Map Mode Object, Map Bounds Volume, Penetrable Surface, Probe Volume
  Region, Kit Map Descriptor, Map Ladder, Map Variant Group, Map Variant Option) ve harita mantığı bileşenleri. Kendi script'in olamaz; kamera, ses kaynağı,
  parçacık sistemi, UI, video, terrain, animasyon ya da fizik gövdesi de olamaz. Ses ve efekt Map Sound ve
  Map Effect ile gelir.
- **Shader'lar:** Universal Render Pipeline Lit, Simple Lit, Unlit, Baked Lit. Gökyüzü: Skybox
  Procedural, Cubemap, 6 Sided ya da Panoramic.
- **Katmanlar:** Default, Ignore Raycast, MapBlocker (görünmez duvarlar), GuessMapBlocker.
- **Işık:** yukarıdaki gibi. En çok 64 ışık.
- **Boyut:** paket en çok 256 MB, 512 MB doku, bir dokunun uzun kenarında 8192 piksel,
  3 milyon üçgen, 6 milyon vertex ve 10.000 renderer.
- **Ad:** haritanın kimliği sahne adının küçük harflisidir: harf, rakam, `-` ve `_`, en çok 23 karakter.
  Oyunun kendi kullandığı sahne adları (Startup, Menu, Game, HunterRoom ve diğerleri, ayrıca `Map_` ya
  da `GameMode_` ile başlayan her şey) kullanılamaz.

Hata, oyunun haritayı açmayacağı demektir. Uyarılara bakmaya değer ama haritayı durdurmaz.

## Arkadaşlarla oynamak

Oturumdaki herkeste harita dosyalarının birebir aynısı olmalı: oyun haritayı yüklemeden önce onları
karşılaştırır. Paket her sistem için aynı; haritayı bir kez paketle, sonra kitteki `Builds/Maps/<kimlik>/`
klasörünü arkadaşlarına gönder (zip'leyerek). Her biri klasörü, hiç değiştirmeden, oyunun harita
klasörüne koyar:

| Sistem | Klasör |
|---|---|
| Windows | `%USERPROFILE%\AppData\LocalLow\DevAdam\MimicBusters\Maps\Local\` |
| macOS | `~/Library/Application Support/DevAdam/MimicBusters/Maps/Local/` |
| Linux | `~/.config/unity3d/DevAdam/MimicBusters/Maps/Local/` |

Sonuç `...\Maps\Local\<kimlik>\map.json` ve yanında `map.any.bundle` gibi görünür. Harita
bundan sonra oyunun harita seçicisinde **Topluluk** altında çıkar. Steam Atölyesi desteği sonra, oyunun
içinden gelecek.

## Bir şey ters giderse

- **Kit, oyunun haritayı reddedeceğini söylüyor.** Nedenler Paketle ve Test Et penceresinde listeli. Tam
  rapor Console'da ve `Builds/Maps/<kimlik>.report.txt` dosyasında.
- **Oyun haritanın açılamadığını söylüyor.** Oyunda **Ayarlar > Oynanış > Geliştirici konsolu**nu aç,
  konsolu Esc'nin altındaki tuşla aç ve `kitmaps` yaz: haritalarını ve birinin neden oynanamadığını
  listeler. `kitinspect <kimlik>` haritayı yüklemeden okur.
- **Harita fazla karanlık ya da ışığı yanlış.** Işığı yeniden bake et, sonra yeniden paketle.
  Doğrulayıcı sahnenin başka bir sahnenin ışık verisini kullandığını söylüyorsa (kopyalanmış sahnelerde
  olur), **Map Kit > Tools > Detach Copied Lighting**, sonra bake.
- **Oyuncular zeminden düşüyor ya da duvarın içinde doğuyor.** Collider'lara bak, doğma noktasını
  kaydır ve harita sınırını yeniden bake et.
- **Oyun, haritanın eski bir kitle yapıldığını söylüyor.** Yeni kiti indir, haritanı onda aç ve yeniden
  paketle.

## Lisans

[LICENSE.md](LICENSE.md) dosyasına bak. Mimic Busters adı ve logosu DevAdam'a aittir ve lisansa dahil
değildir.

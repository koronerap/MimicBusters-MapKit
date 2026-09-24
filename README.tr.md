# Mimic Busters Harita Kiti

**Mimic Busters** için Unity'de kendi haritanı yap, sonra tek tıkla oyunda gez.

[English guide](README.md)

Kit, oyunun kullandığı harita koduyla gelen bir Unity projesi. Haritayı sıradan bir Unity sahnesi olarak
kurarsın, ışığını Unity'de bake edersin; kit onu oyunun kurallarıyla denetler, Windows, Linux ve macOS'un
aynı şekilde açtığı tek bir dosyaya paketler, oyununa kurar ve oyunu o haritada başlatır.

Bu kitle yapılan haritalar **Standard** (Avcılar ve Modelciler, aynı işaretçileri kullanan Companion ve
Infestation dahil), **Practice** (serbest yapım, round yok) ve **Deathmatch** (herkes silahlı, taraf yok)
modlarında oynanır.

## Gerekenler

- **Unity 6000.3.8f1**, tam olarak bu sürüm, Unity Hub'dan. Haritalar Unity asset bundle'ıdır ve oyun
  yalnızca kendi Unity sürümünün yaptığı bundle'ları okuyabilir. Ek modül gerekmez: kit, editörle gelenle
  paketler.
- Haritaları denemek için Steam'den kurulu **Mimic Busters**.

## Başlarken

1. Kiti indir: GitHub'da **Code > Download ZIP** ya da **Releases** altındaki zip. Kısa bir yola aç.
2. Unity Hub'da **Add > Add project from disk**, açtığın klasörü seç, 6000.3.8f1 ile aç. İlk içe aktarma
   birkaç dakika sürer.
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
| ModelerSpawn | Practice: oyuncuların belirdiği yer, HiderSpawn ve LobbySpawn gibi. |
| ArenaSpawn | İsteğe bağlı. 1v1 düello noktası (Standard). Oyunun Avcı odasında kendi noktaları var. |

Standard için en az bir HunterSpawn ve bir HiderSpawn gerekir. Practice için Practice işaretli bir
HiderSpawn, LobbySpawn ya da ModelerSpawn gerekir. Deathmatch, Deathmatch işaretli bütün HunterSpawn,
HiderSpawn ve LobbySpawn noktalarını kullanır ve oyuncuyu herkesten en uzak olana koyar; en az altı,
dağınık koy. Her birinden birkaç tane koy: dağınık, zeminde, yüzü haritaya dönük. İşaretçiden çıkan
çizgi oyuncunun bakacağı yönü gösterir. Hiçbir modu seçili olmayan işaretçiyi hiçbir mod kullanmaz.

**Harita sınırı.** **Map Bounds Volume** bütün oyun alanını saran bir kutu. Bake edilince oyun,
oyuncuların nerede olabileceğini bilir ve haritadan düşeni geri alır. Kutu, oyuncunun ulaşabildiği her
yeri, zıplamalar ve çatılar için üstte pay bırakarak kaplasın; sonra **Map Kit > Bake Map Bounds**. Duvar,
zemin ya da doğma noktası her değiştiğinde yeniden bake et. Kapalı bir haritada doğrulayıcı "oyun alanı
kutunun duvarına ulaşıyor" diyorsa, söylediği yerde bir delik var.

**Işık.** Yeni haritanın geldiği ışık ayarlarını koru: Mixed ışık **Shadowmask** modunda,
**Directional** lightmap, **Realtime Global Illumination kapalı**. Oyun yalnızca bu düzenin
shader'larını taşıyor. Işıklar **Mixed** ya da **Baked** olsun; gölgeli dörtten fazla gerçek zamanlı ışık
oyunu yavaşlatır. Sis istersen **Exponential Squared** olmalı. **Adaptive Probe Volume** oyuncuları ve
Modelcileri haritaya uygun aydınlatır; geri kalanla birlikte bake edilir (Generate Lighting'in parçası).
Geometriye ya da ışıklara her dokunuşta yeniden bake et, yoksa oyun eski ışığı gösterir.

**Post-processing.** Bir **Volume** yalnızca renk ve ton efektleri taşıyabilir: Bloom, Tonemapping, Color
Adjustments, White Balance, Lift Gamma Gain, Shadows Midtones Highlights, Split Toning, Channel Mixer,
Color Curves, Vignette.

## Kurallar

Oyun bir haritayı Unity'nin açmasına izin vermeden önce okur ve bu kuralların dışındaki her şeyi
reddeder. **Map Kit > Validate** hepsini editörde denetler; **Pack and Test** paketlenmiş dosyaları
oyunun kendi koduyla bir kez daha denetler.

- **Bileşenler:** Mesh Filter, Mesh Renderer, LOD Group, Box, Sphere, Capsule ve Mesh Collider, Light,
  Reflection Probe, Light Probe Group, Occlusion Area ve Portal, Adaptive Probe Volume, Volume ve kitin
  kendi bileşenleri (Map Marker, Map Mode Object, Map Bounds Volume, Penetrable Surface, Probe Volume
  Region, Kit Map Descriptor). Kendi script'in olamaz; kamera, ses, parçacık, UI, video, terrain,
  animasyon ya da fizik gövdesi de olamaz.
- **Shader'lar:** Universal Render Pipeline Lit, Simple Lit, Unlit, Baked Lit. Gökyüzü: Skybox
  Procedural, Cubemap, 6 Sided ya da Panoramic.
- **Katmanlar:** Default, Ignore Raycast, MapBlocker (görünmez duvarlar), GarticMapBlocker.
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

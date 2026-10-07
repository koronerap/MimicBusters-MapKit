using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Mimicraft.Gameplay;
using Mimicraft.MapKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Mimicraft.EditorTools
{
    public enum KitIssueLevel { Error, Warning, Info }

    /// <summary>One finding, in both languages the kit speaks, with the objects it is about.</summary>
    public sealed class KitIssue
    {
        public KitIssueLevel Level;
        public KitArea Area;
        public string Tr, En;
        public UnityEngine.Object[] Targets = Array.Empty<UnityEngine.Object>();

        public string Text(bool turkish) => turkish ? Tr : En;
    }

    public enum KitArea { Scene, MapInfo, Components, Materials, Layers, Lighting, Markers, Bounds, Budget, Packaging, Logic }

    /// <summary>What a validation found, and the numbers the budget is held to.</summary>
    public sealed class KitValidation
    {
        public readonly List<KitIssue> Issues = new List<KitIssue>();
        public string SceneName = "";
        public string MapId = "";
        public int Renderers, Lights, ShadowedRealtimeLights, Meshes, Textures, Lightmaps;
        public long Triangles, Vertices, TextureBytes, LargestTextureSide;

        public int Errors => Issues.Count(i => i.Level == KitIssueLevel.Error);
        public int Warnings => Issues.Count(i => i.Level == KitIssueLevel.Warning);

        /// <summary>No errors: the game would open this map. Warnings are things worth a look that it would
        /// still open.</summary>
        public bool CanPack => Errors == 0;
    }

    /// <summary>
    /// Checks the open scene against what the game will accept (ExternalMapSupportPlan.md §5), before a
    /// map is packed - so the author hears about a forbidden component, a wrong shader or a missing spawn
    /// from here, with the object selected, rather than from a refusal in the game.
    ///
    /// The rules themselves are KitMapRules, the same code the game runs; this only walks a scene with
    /// them and adds what an author needs that a loader does not: required markers per mode, spawns that
    /// are inside walls or in the air, lighting that is not baked, budgets before building. It is a
    /// convenience, not the security boundary (plan §5): a bundle made without the kit meets the game's
    /// checks all the same.
    ///
    /// The budget numbers here are estimates from the scene; Pack runs the game's own inspector on the
    /// built bundle, and that is the number that counts.
    /// </summary>
    public static class KitMapValidator
    {
        static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
        static string Tr(FormattableString s) => s.ToString(Turkish);
        static string En(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);

        // Player.prefab's CharacterController: height 1.8, radius 0.4, centre 1 m up.
        const float PlayerRadius = 0.4f;
        const float PlayerBottom = 0.5f;
        const float PlayerTop = 1.5f;
        const float GroundSearch = 3f;

        // Below this Deathmatch's farthest-from-everyone spawn has little to choose from. The example room
        // has six; House, the built-in Deathmatch map, has 28.
        const int MinDeathmatchSpawns = 6;

        public static KitValidation Run(Scene scene)
        {
            var v = new KitValidation { SceneName = scene.name, MapId = KitMapId.SanitizeName(scene.name) };

            var objects = new List<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                // Never saved, so never packed: the kit's own preview objects (KitGameLook) are not the map's.
                if ((root.hideFlags & HideFlags.DontSave) != 0)
                    continue;

                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    objects.Add(t.gameObject);
            }

            CheckScene(v, scene);
            KitMapDescriptor descriptor = CheckDescriptor(v, objects);
            if (descriptor != null && KitPreview.Describe(scene) == null)
                Add(v, KitIssueLevel.Warning, KitArea.MapInfo,
                    "Önizleme resmi yok: haritanın kartı düz renk görünür. Descriptor'da 'Sahne görünümünden al' ya da kendi resmini seç.",
                    "No preview picture: the map's card shows a flat colour. On the descriptor, use 'Take from the Scene view' or pick your own picture.",
                    new UnityEngine.Object[] { descriptor.gameObject });
            CheckComponents(v, objects);
            CheckMaterials(v, objects);
            CheckLayers(v, objects);
            CheckLighting(v, scene, objects);
            CheckMarkers(v, objects, descriptor);
            CheckLogic(v, objects);
            CheckLadders(v, objects);
            CheckVariants(v, objects);
            CheckBounds(v, objects);
            CheckBudget(v, objects);
            return v;
        }

        /// <summary>The path of the baked lighting the scene points at when another scene owns it, or null.
        ///
        /// Unity keeps a scene's lighting in a folder named after the scene. One elsewhere belongs to another
        /// scene: the trap a copied scene falls into. It keeps the original's lighting, and baking it writes
        /// into the ORIGINAL's file (measured on the HQ spike). The scene must be the active one.</summary>
        public static string ForeignLightingData(Scene scene)
        {
            LightingDataAsset lighting = BakedLighting();
            if (lighting == null)
                return null;
            string path = AssetDatabase.GetAssetPath(lighting);
            return Path.GetFileName(Path.GetDirectoryName(path)) == scene.name ? null : path;
        }

        /// <summary>The active scene's baked lighting data, or null when it has none.
        ///
        /// Not simply Lightmapping.lightingDataAsset: a scene that has never been baked reports a built-in
        /// placeholder from Resources/unity_builtin_extra rather than nothing (seen in the kit's self test),
        /// which passed for a bake and for another scene's lighting at once.</summary>
        public static LightingDataAsset BakedLighting()
        {
            LightingDataAsset lighting = Lightmapping.lightingDataAsset;
            return lighting != null && AssetDatabase.GetAssetPath(lighting).StartsWith("Assets/", StringComparison.Ordinal)
                ? lighting
                : null;
        }

        /// <summary>Whether the scene carries probe data baked into another scene's Baking Set: the same
        /// trap for Adaptive Probe Volumes.</summary>
        public static bool HasForeignProbeData(Scene scene)
        {
            string guid = AssetDatabase.AssetPathToGUID(scene.path);
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<ProbeVolumePerSceneData>(true))
                .Any(data => data.bakingSet != null && !data.bakingSet.sceneGUIDs.Contains(guid));
        }

        static void Add(KitValidation v, KitIssueLevel level, KitArea area, string tr, string en, IEnumerable<UnityEngine.Object> targets = null)
        {
            v.Issues.Add(new KitIssue
            {
                Level = level,
                Area = area,
                Tr = tr,
                En = en,
                Targets = targets != null ? targets.Where(t => t != null).Distinct().ToArray() : Array.Empty<UnityEngine.Object>(),
            });
        }

        // ------------------------------------------------------------------ scene

        static void CheckScene(KitValidation v, Scene scene)
        {
            if (string.IsNullOrEmpty(scene.path))
            {
                Add(v, KitIssueLevel.Error, KitArea.Scene,
                    "Sahne kaydedilmemiş. Önce kaydet.",
                    "The scene is not saved. Save it first.");
                return;
            }

            if (EditorBuildSettings.scenes.Any(s => s.path == scene.path))
                Add(v, KitIssueLevel.Error, KitArea.Scene,
                    "Bu sahne Build Settings listesinde. Kit haritası oyunun kendi sahnelerinden biri olamaz.",
                    "This scene is in Build Settings. A kit map cannot be one of the game's own scenes.");

            if (KitMapRules.IsSceneNameReserved(scene.name))
                Add(v, KitIssueLevel.Error, KitArea.Scene,
                    Tr($"'{scene.name}' adı oyunun kendi sahnelerine ayrılmış. {KitMapRules.ReservedScenePrefixes} ile başlamayan başka bir ad ver."),
                    En($"The name '{scene.name}' is kept for the game's own scenes. Pick one that does not start with {KitMapRules.ReservedScenePrefixes}."));

            string id = KitMapId.SanitizeName(scene.name);
            string lowered = scene.name.ToLowerInvariant();
            Add(v, KitIssueLevel.Info, KitArea.Scene,
                Tr($"Haritanın kimliği '{id}'. Oyunda Maps/Local/{id} klasörüne kurulur."),
                En($"The map's id is '{id}'. In the game it installs to Maps/Local/{id}."));
            if (id != lowered)
                Add(v, KitIssueLevel.Info, KitArea.Scene,
                    Tr($"Sahnenin adı kimliğe çevrilirken değişti ({KitMapId.MaxLocalName} karaktere kadar küçük harf, rakam, '-' ve '_' kalır)."),
                    En($"The scene name changed on its way to an id (up to {KitMapId.MaxLocalName} lower-case letters, digits, '-' and '_' are kept)."));
        }

        // ------------------------------------------------------------------ map info

        static KitMapDescriptor CheckDescriptor(KitValidation v, List<GameObject> objects)
        {
            var found = objects.Select(o => o.GetComponent<KitMapDescriptor>()).Where(d => d != null).ToList();
            if (found.Count == 0)
            {
                Add(v, KitIssueLevel.Error, KitArea.MapInfo,
                    "Sahnede KitMapDescriptor yok. Boş bir objeye ekle: haritanın adı, açıklaması, yapımcısı ve modları onda durur.",
                    "The scene has no KitMapDescriptor. Add one to an empty object: it holds the map's name, description, author and modes.");
                return null;
            }

            if (found.Count > 1)
                Add(v, KitIssueLevel.Error, KitArea.MapInfo,
                    Tr($"Sahnede {found.Count} KitMapDescriptor var. Bir tane olmalı."),
                    En($"The scene has {found.Count} KitMapDescriptors. It needs exactly one."),
                    found.Select(d => d.gameObject));

            KitMapDescriptor descriptor = found[0];
            var target = new UnityEngine.Object[] { descriptor.gameObject };

            if (string.IsNullOrWhiteSpace(descriptor.MapName))
                Add(v, KitIssueLevel.Warning, KitArea.MapInfo,
                    "Harita adı boş. Oyunda sahnenin adı görünür.",
                    "The map name is empty. The game will show the scene name.", target);
            else if (descriptor.MapName.Length > 64)
                Add(v, KitIssueLevel.Error, KitArea.MapInfo,
                    Tr($"Harita adı {descriptor.MapName.Length} karakter. En çok 64 olabilir."),
                    En($"The map name is {descriptor.MapName.Length} characters long. The limit is 64."), target);

            if ((descriptor.Description ?? "").Length > 512)
                Add(v, KitIssueLevel.Error, KitArea.MapInfo,
                    Tr($"Açıklama {descriptor.Description.Length} karakter. En çok 512 olabilir."),
                    En($"The description is {descriptor.Description.Length} characters long. The limit is 512."), target);

            if ((descriptor.Author ?? "").Length > 32)
                Add(v, KitIssueLevel.Error, KitArea.MapInfo,
                    Tr($"Yapımcı adı {descriptor.Author.Length} karakter. En çok 32 olabilir."),
                    En($"The author name is {descriptor.Author.Length} characters long. The limit is 32."), target);

            MapMarkerModes modes = descriptor.Modes & (MapMarkerModes.Standard | MapMarkerModes.Practice
                | MapMarkerModes.Deathmatch | MapMarkerModes.Gartic);
            if (modes == MapMarkerModes.None)
                Add(v, KitIssueLevel.Error, KitArea.MapInfo,
                    "Harita hiçbir mod için işaretlenmemiş. Descriptor'da en az birini seç: Standard, Practice, Deathmatch ya da Gartic.",
                    "The map is not marked for any mode. Tick at least one on the descriptor: Standard, Practice, Deathmatch or Gartic.", target);

            if ((modes & MapMarkerModes.Standard) != 0)
                Add(v, KitIssueLevel.Info, KitArea.MapInfo,
                    "Standard işaretli harita Companion ve Infestation'da da sunulur: üçü aynı işaretçileri kullanır.",
                    "A map marked for Standard is offered in Companion and Infestation too: all three read the same markers.", target);

            return descriptor;
        }

        // ------------------------------------------------------------------ components

        static void CheckComponents(KitValidation v, List<GameObject> objects)
        {
            var missing = new List<GameObject>();
            var refused = new Dictionary<Type, List<GameObject>>();
            var buffer = new List<Component>();

            foreach (GameObject go in objects)
            {
                go.GetComponents(buffer);
                foreach (Component component in buffer)
                {
                    if (component == null)
                    {
                        missing.Add(go);
                        continue;
                    }
                    if (KitMapRules.IsComponentAllowed(component))
                        continue;

                    Type type = component.GetType();
                    if (!refused.TryGetValue(type, out List<GameObject> list))
                        refused[type] = list = new List<GameObject>();
                    list.Add(go);
                }
            }

            if (missing.Count > 0)
                Add(v, KitIssueLevel.Error, KitArea.Components,
                    Tr($"{missing.Count} objede eksik (bulunamayan) script var. Kaldır."),
                    En($"{missing.Count} object(s) have a missing script. Remove it."), missing);

            foreach (var pair in refused.OrderBy(p => p.Key.Name, StringComparer.Ordinal))
            {
                (string tr, string en) = WhyRefused(pair.Key);
                Add(v, KitIssueLevel.Error, KitArea.Components,
                    Tr($"{pair.Key.Name} haritada kullanılamaz: {tr}. {pair.Value.Count} obje."),
                    En($"{pair.Key.Name} cannot be in a map: {en}. {pair.Value.Count} object(s)."), pair.Value);
            }

            // Volume profiles are assets, not scene objects, but a bundle carries them and the game checks
            // every override in them.
            foreach (GameObject go in objects)
            {
                if (!go.TryGetComponent(out Volume volume) || volume.sharedProfile == null)
                    continue;

                foreach (VolumeComponent effect in volume.sharedProfile.components)
                {
                    if (effect == null || KitMapRules.IsVolumeComponentAllowed(effect.GetType()))
                        continue;
                    Add(v, KitIssueLevel.Error, KitArea.Components,
                        Tr($"'{volume.sharedProfile.name}' profilinde {effect.GetType().Name} var. İzinli değil, kapalı olsa da. Profilden kaldır."),
                        En($"The profile '{volume.sharedProfile.name}' has {effect.GetType().Name}. It is not allowed, even switched off. Remove it from the profile."),
                        new UnityEngine.Object[] { go, volume.sharedProfile });
                }
            }
        }

        /// <summary>Why the game refuses a component, in both languages (plan §5.1).</summary>
        static (string tr, string en) WhyRefused(Type type)
        {
            string ns = type.Namespace ?? "";
            if (type == typeof(Camera) || type == typeof(AudioListener))
                return ("ekranı ya da sesi ele geçirebilir", "it could take over the screen or the sound");
            if (type == typeof(RectTransform) || type == typeof(Canvas) || ns.StartsWith("UnityEngine.UI", StringComparison.Ordinal) || ns.StartsWith("TMPro", StringComparison.Ordinal))
                return ("arayüz bileşenleriyle oyunun ekranları taklit edilebilir", "UI components could imitate the game's screens");
            if (type.Name == "VideoPlayer")
                return ("internetten yayın açabilir", "it can stream from the internet");
            if (ns.StartsWith("Unity.Netcode", StringComparison.Ordinal))
                return ("ağı oyun yönetir", "networking belongs to the game");
            if (type == typeof(Animator) || type == typeof(Animation))
                return ("animasyon kontrolcüleri kod çalıştırabilir", "animation controllers can run code");
            if (type == typeof(AudioSource) || type.Name == "ParticleSystem" || type.Name == "Terrain" || type.Name == "TerrainCollider")
                return ("bu sürümde yok, ileride bir bütçeyle gelecek", "not supported yet, planned with a budget");
            if (type == typeof(Rigidbody) || typeof(Joint).IsAssignableFrom(type) || type == typeof(CharacterController))
                return ("hareket eden fizik nesnelerini oyun yönetir", "moving physics objects belong to the game");
            if (typeof(MonoBehaviour).IsAssignableFrom(type))
                return ("haritalar kod taşıyamaz, yalnızca kit'in kendi bileşenleri kullanılabilir", "maps cannot carry code, only the kit's own components");
            return ("oyun bu bileşeni haritalarda kabul etmiyor", "the game does not accept it in maps");
        }

        // ------------------------------------------------------------------ materials

        static void CheckMaterials(KitValidation v, List<GameObject> objects)
        {
            var byShader = new Dictionary<string, List<Renderer>>(StringComparer.Ordinal);
            var empty = new List<Renderer>();

            foreach (GameObject go in objects)
            {
                if (!go.TryGetComponent(out Renderer renderer))
                    continue;
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null || material.shader == null)
                    {
                        empty.Add(renderer);
                        continue;
                    }
                    string shader = material.shader.name;
                    if (KitMapRules.IsShaderAllowed(shader))
                        continue;
                    if (!byShader.TryGetValue(shader, out List<Renderer> list))
                        byShader[shader] = list = new List<Renderer>();
                    list.Add(renderer);
                }
            }

            string allowed = string.Join(", ", KitMapRules.Shaders.Where(s => !s.StartsWith("Skybox/", StringComparison.Ordinal)));
            foreach (var pair in byShader)
                Add(v, KitIssueLevel.Error, KitArea.Materials,
                    Tr($"{pair.Value.Count} renderer '{pair.Key}' shader'ını kullanıyor. Haritalar şunları kullanabilir: {allowed}."),
                    En($"{pair.Value.Count} renderer(s) use the shader '{pair.Key}'. Maps may use: {allowed}."), pair.Value.Select(r => r.gameObject));

            if (empty.Count > 0)
                Add(v, KitIssueLevel.Warning, KitArea.Materials,
                    Tr($"{empty.Count} renderer'da boş materyal yuvası var. Oyunda pembe görünür."),
                    En($"{empty.Count} renderer(s) have an empty material slot. It shows up pink in the game."), empty.Select(r => r.gameObject));

            Material sky = RenderSettings.skybox;
            if (sky != null && sky.shader != null && !KitMapRules.IsShaderAllowed(sky.shader.name))
                Add(v, KitIssueLevel.Error, KitArea.Materials,
                    Tr($"Gökyüzü materyali '{sky.shader.name}' kullanıyor. Skybox/Procedural, Cubemap, 6 Sided ya da Panoramic olmalı."),
                    En($"The skybox material uses '{sky.shader.name}'. It must be Skybox/Procedural, Cubemap, 6 Sided or Panoramic."),
                    new UnityEngine.Object[] { sky });
        }

        // ------------------------------------------------------------------ layers

        static void CheckLayers(KitValidation v, List<GameObject> objects)
        {
            string allowed = string.Join(", ", KitMapRules.LayerNames);
            foreach (var group in objects.Where(o => !KitMapRules.IsLayerAllowed(o.layer)).GroupBy(o => o.layer))
            {
                string layer = LayerMask.LayerToName(group.Key);
                if (string.IsNullOrEmpty(layer))
                    layer = "#" + group.Key;
                Add(v, KitIssueLevel.Error, KitArea.Layers,
                    Tr($"{group.Count()} obje '{layer}' katmanında. Haritalar şu katmanları kullanabilir: {allowed}."),
                    En($"{group.Count()} object(s) are on the '{layer}' layer. Maps may use: {allowed}."), group);
            }
        }

        // ------------------------------------------------------------------ lighting

        static void CheckLighting(KitValidation v, Scene scene, List<GameObject> objects)
        {
            if (!Lightmapping.TryGetLightingSettings(out LightingSettings settings) || settings == null)
            {
                Add(v, KitIssueLevel.Warning, KitArea.Lighting,
                    "Sahnenin Lighting Settings dosyası yok. Lighting penceresinde yeni bir tane oluştur (Shadowmask, Directional).",
                    "The scene has no Lighting Settings asset. Create one in the Lighting window (Shadowmask, Directional).");
            }
            else
            {
                var asset = new UnityEngine.Object[] { settings };
                if (settings.realtimeGI)
                    Add(v, KitIssueLevel.Error, KitArea.Lighting,
                        "Realtime Global Illumination açık. Kapat: haritalar bake edilmiş ışık ve APV kullanır.",
                        "Realtime Global Illumination is on. Turn it off: maps use baked lighting and APV.", asset);
                if (!settings.bakedGI)
                    Add(v, KitIssueLevel.Warning, KitArea.Lighting,
                        "Baked Global Illumination kapalı. Harita yalnızca gerçek zamanlı ışıkla aydınlanır.",
                        "Baked Global Illumination is off. The map will be lit by realtime lights only.", asset);
                else
                {
                    if (settings.mixedBakeMode != MixedLightingMode.Shadowmask)
                        Add(v, KitIssueLevel.Error, KitArea.Lighting,
                            Tr($"Mixed Lighting modu {settings.mixedBakeMode}. Shadowmask olmalı: oyun başka modun shader'larını içermiyor."),
                            En($"The mixed lighting mode is {settings.mixedBakeMode}. It must be Shadowmask: the game carries no shaders for the others."), asset);
                    if (settings.directionalityMode != LightmapsMode.CombinedDirectional)
                        Add(v, KitIssueLevel.Error, KitArea.Lighting,
                            "Lightmap'ler Non-Directional. Directional olmalı.",
                            "The lightmaps are Non-Directional. They must be Directional.", asset);
                    if (BakedLighting() == null)
                        Add(v, KitIssueLevel.Warning, KitArea.Lighting,
                            "Işık henüz bake edilmemiş (Window > Rendering > Lighting > Generate Lighting).",
                            "The lighting is not baked yet (Window > Rendering > Lighting > Generate Lighting).");
                    else if (ForeignLightingData(scene) is string foreign)
                        Add(v, KitIssueLevel.Warning, KitArea.Lighting,
                            Tr($"Sahne başka bir sahnenin ışık verisini kullanıyor ({foreign}). Kopyalanmış sahnelerde olur: harita o sahnenin ışığıyla çıkar ve bake etmek o sahnenin dosyasının üzerine yazar. Map Kit > Tools > Detach Copied Lighting ile ayır, sonra Generate Lighting."),
                            En($"The scene uses another scene's lighting data ({foreign}). Copied scenes do this: the map would carry that scene's light, and baking would write over that scene's file. Detach it with Map Kit > Tools > Detach Copied Lighting, then Generate Lighting."),
                            new UnityEngine.Object[] { BakedLighting() });
                }
            }

            if (RenderSettings.fog && RenderSettings.fogMode != FogMode.ExponentialSquared)
                Add(v, KitIssueLevel.Error, KitArea.Lighting,
                    Tr($"Sis modu {RenderSettings.fogMode}. Exponential Squared olmalı ya da sis kapalı."),
                    En($"The fog mode is {RenderSettings.fogMode}. It must be Exponential Squared, or fog off."));

            // Warnings, not errors: the game does not count lightmaps (their memory is in the texture budget),
            // and the official HQ already uses 9. These are about cost, not about whether the map opens.
            LightmapData[] maps = LightmapSettings.lightmaps;
            v.Lightmaps = maps.Length;
            if (maps.Length > 16)
                Add(v, KitIssueLevel.Warning, KitArea.Lighting,
                    Tr($"{maps.Length} lightmap var, çok fazla: yükleme uzar ve bellek dolar. Lighting penceresinde Lightmap Resolution'ı düşür."),
                    En($"There are {maps.Length} lightmaps, which is a lot: loading slows and memory fills. Lower Lightmap Resolution in the Lighting window."));
            foreach (LightmapData map in maps)
            {
                Texture2D color = map.lightmapColor;
                if (color != null && (color.width > 2048 || color.height > 2048))
                {
                    Add(v, KitIssueLevel.Warning, KitArea.Lighting,
                        Tr($"Lightmap {color.width}x{color.height}. 2048x2048'den büyüğü bellekte pahalı (Lighting > Max Lightmap Size)."),
                        En($"A lightmap is {color.width}x{color.height}. Anything over 2048x2048 is expensive in memory (Lighting > Max Lightmap Size)."));
                    break;
                }
            }

            var lights = objects.Select(o => o.GetComponent<Light>()).Where(l => l != null).ToList();
            v.Lights = lights.Count;
            var shadowed = lights.Where(l => l.lightmapBakeType != LightmapBakeType.Baked && l.shadows != LightShadows.None).ToList();
            v.ShadowedRealtimeLights = shadowed.Count;
            if (lights.Count > KitMapRules.MaxLights)
                Add(v, KitIssueLevel.Error, KitArea.Lighting,
                    Tr($"{lights.Count} ışık var. En çok {KitMapRules.MaxLights} olabilir."),
                    En($"There are {lights.Count} lights. The limit is {KitMapRules.MaxLights}."), lights.Select(l => l.gameObject));
            if (shadowed.Count > 4)
                Add(v, KitIssueLevel.Warning, KitArea.Lighting,
                    Tr($"{shadowed.Count} gerçek zamanlı ışık gölge düşürüyor. 4'ten fazlası oyunu yavaşlatır: çoğunu Baked yap ya da gölgesini kapat."),
                    En($"{shadowed.Count} realtime lights cast shadows. More than 4 slows the game: make most of them Baked, or switch their shadows off."),
                    shadowed.Select(l => l.gameObject));

            // APV: without it players and Modelers are lit by realtime lights alone and read as dark shapes.
            bool hasVolume = objects.Any(o => o.GetComponent<ProbeVolume>() != null);
            ProbeVolumePerSceneData perScene = objects.Select(o => o.GetComponent<ProbeVolumePerSceneData>()).FirstOrDefault(p => p != null);
            string guid = AssetDatabase.AssetPathToGUID(scene.path);
            if (!hasVolume)
                Add(v, KitIssueLevel.Warning, KitArea.Lighting,
                    "Sahnede Adaptive Probe Volume yok. Oyuncular ve Modelciler yalnızca gerçek zamanlı ışıkla aydınlanır ve karanlık görünür.",
                    "The scene has no Adaptive Probe Volume. Players and Modelers will be lit by realtime lights only and look dark.");
            else if (perScene == null || perScene.bakingSet == null || !perScene.bakingSet.sceneGUIDs.Contains(guid))
                Add(v, KitIssueLevel.Warning, KitArea.Lighting,
                    "Probe volume var ama bu sahne için bake edilmemiş. Lighting penceresinde Adaptive Probe Volumes'u bake et.",
                    "There is a probe volume, but it is not baked for this scene. Bake Adaptive Probe Volumes in the Lighting window.");
        }

        // ------------------------------------------------------------------ markers

        static void CheckMarkers(KitValidation v, List<GameObject> objects, KitMapDescriptor descriptor)
        {
            var markers = objects.Select(o => o.GetComponent<MapMarker>()).Where(m => m != null).ToList();
            MapMarkerModes modes = descriptor != null ? descriptor.Modes : MapMarkerModes.None;

            int Count(MapMarkerKind kind, MapMarkerModes mode) => markers.Count(m => m.Is(kind) && m.AppliesTo(mode));

            if ((modes & MapMarkerModes.Standard) != 0)
            {
                if (Count(MapMarkerKind.HunterSpawn, MapMarkerModes.Standard) == 0)
                    Add(v, KitIssueLevel.Error, KitArea.Markers,
                        "Standard için HunterSpawn işaretçisi yok. Avcılar av başlarken buraya çıkar.",
                        "No HunterSpawn marker for Standard. Hunters come out here when the hunt starts.");
                if (Count(MapMarkerKind.HiderSpawn, MapMarkerModes.Standard) == 0)
                    Add(v, KitIssueLevel.Error, KitArea.Markers,
                        "Standard için HiderSpawn işaretçisi yok. Modelciler hazırlık başında buraya konur.",
                        "No HiderSpawn marker for Standard. Modelers are placed here when preparation starts.");
                if (Count(MapMarkerKind.LobbySpawn, MapMarkerModes.Standard) == 0)
                    Add(v, KitIssueLevel.Info, KitArea.Markers,
                        "LobbySpawn yok. Lobide bekleyenler HiderSpawn noktalarına konur.",
                        "No LobbySpawn. Players waiting in the lobby go to the HiderSpawn points.");
                var doors = markers.Where(m => m.Is(MapMarkerKind.HunterDoor)).ToList();
                if (doors.Count > 1)
                    Add(v, KitIssueLevel.Warning, KitArea.Markers,
                        Tr($"{doors.Count} HunterDoor var. Oyun yalnızca ilkini kullanır."),
                        En($"There are {doors.Count} HunterDoors. The game uses only the first."), doors.Select(d => d.gameObject));
            }

            if ((modes & MapMarkerModes.Practice) != 0
                && Count(MapMarkerKind.ModelerSpawn, MapMarkerModes.Practice) + Count(MapMarkerKind.LobbySpawn, MapMarkerModes.Practice)
                + Count(MapMarkerKind.HiderSpawn, MapMarkerModes.Practice) == 0)
                Add(v, KitIssueLevel.Error, KitArea.Markers,
                    "Practice için doğma noktası yok. Practice işaretli bir ModelerSpawn, LobbySpawn ya da HiderSpawn ekle.",
                    "No spawn point for Practice. Add a ModelerSpawn, LobbySpawn or HiderSpawn with Practice ticked.");

            // Deathmatch pools every HunterSpawn, HiderSpawn and LobbySpawn marked for it and puts a player
            // at the one farthest from everybody (DeathmatchRoundManager), so it wants a few, spread out.
            // Strict here where the game is lenient: the game falls back to every marker on a map that marks
            // none for Deathmatch, which suits the built-in maps and hides a forgotten box on a kit one.
            if ((modes & MapMarkerModes.Deathmatch) != 0)
            {
                int points = markers.Count(m => m.AppliesTo(MapMarkerModes.Deathmatch)
                    && (m.Kind & (MapMarkerKind.HunterSpawn | MapMarkerKind.HiderSpawn | MapMarkerKind.LobbySpawn)) != 0);
                if (points == 0)
                    Add(v, KitIssueLevel.Error, KitArea.Markers,
                        "Deathmatch için doğma noktası yok. HunterSpawn, HiderSpawn ya da LobbySpawn işaretçilerinde Deathmatch'i de seç.",
                        "No spawn point for Deathmatch. Tick Deathmatch on HunterSpawn, HiderSpawn or LobbySpawn markers too.");
                else if (points < MinDeathmatchSpawns)
                    Add(v, KitIssueLevel.Warning, KitArea.Markers,
                        Tr($"Deathmatch için {points} doğma noktası var. Oyun oyuncuyu herkesten en uzak noktaya koyar; {MinDeathmatchSpawns}'dan azıyla oyuncular birbirinin yanında doğar."),
                        En($"There are {points} spawn points for Deathmatch. The game puts a player at the one farthest from everyone; with fewer than {MinDeathmatchSpawns} players spawn next to each other."));
            }

            // Gartic stands the others on its LobbySpawns (HiderSpawns when there are none) and the player
            // whose turn it is on the ModelerSpawn (GarticRoundManager). Without one the modeller simply
            // stands among the rest, which works and shows the model to nobody in particular.
            if ((modes & MapMarkerModes.Gartic) != 0)
            {
                if (Count(MapMarkerKind.LobbySpawn, MapMarkerModes.Gartic) + Count(MapMarkerKind.HiderSpawn, MapMarkerModes.Gartic) == 0)
                    Add(v, KitIssueLevel.Error, KitArea.Markers,
                        "Gartic için doğma noktası yok. Gartic işaretli bir LobbySpawn ya da HiderSpawn ekle.",
                        "No spawn point for Gartic. Add a LobbySpawn or HiderSpawn with Gartic ticked.");
                if (Count(MapMarkerKind.ModelerSpawn, MapMarkerModes.Gartic) == 0)
                    Add(v, KitIssueLevel.Warning, KitArea.Markers,
                        "Gartic için ModelerSpawn yok. Sırası gelen oyuncu diğerlerinin arasında durur; herkesin görebileceği bir yere Gartic işaretli bir ModelerSpawn koy.",
                        "No ModelerSpawn for Gartic. The player whose turn it is stands among the others; put a ModelerSpawn with Gartic ticked where everyone can see it.");

                // The game stands its console on one of them (GarticRoundManager), the first by position.
                if (Count(MapMarkerKind.GarticComputer, MapMarkerModes.Gartic) > 1)
                    Add(v, KitIssueLevel.Warning, KitArea.Markers,
                        "Gartic için birden fazla GarticComputer var. Oyun yalnızca birine bilgisayar koyar (konuma göre ilkine); fazlasını sil.",
                        "More than one GarticComputer for Gartic. The game puts a computer on only one (the first by position); delete the rest.");
            }

            var unused = markers.Where(m => m.Modes == MapMarkerModes.None).ToList();
            if (unused.Count > 0)
                Add(v, KitIssueLevel.Warning, KitArea.Markers,
                    Tr($"{unused.Count} işaretçide hiçbir mod seçili değil. Hiçbir modda kullanılmazlar."),
                    En($"{unused.Count} marker(s) have no mode ticked. No mode will use them."), unused.Select(m => m.gameObject));

            CheckSpawnPlacement(v, markers);
        }

        // ------------------------------------------------------------------ map logic (plan §16)

        // The game's limits (MapLogic), which the kit cannot see: past them the game ignores the rest.
        const int MaxLogicEntities = 256;
        const int MaxLogicOutputs = 16;
        const float MaxLogicDelay = 3600f;

        /// <summary>Connections that cannot work, and chains without a delay that feed themselves: the game
        /// cuts those at 32 steps (MapLogic.MaxChainDepth), which reads as a map that half works.</summary>
        static void CheckLogic(KitValidation v, List<GameObject> objects)
        {
            List<MapEntity> entities = objects.SelectMany(o => o.GetComponents<MapEntity>()).ToList();
            if (entities.Count == 0)
                return;

            if (entities.Count > MaxLogicEntities)
                Add(v, KitIssueLevel.Error, KitArea.Logic,
                    Tr($"Haritada {entities.Count} mantık varlığı var; oyun en çok {MaxLogicEntities} tanesini çalıştırır."),
                    En($"The map has {entities.Count} logic entities; the game runs at most {MaxLogicEntities}."));

            var noTarget = new List<GameObject>();
            var wrongInput = new List<GameObject>();
            var foreign = new List<GameObject>();
            var wrongEvent = new List<GameObject>();
            var badDelay = new List<GameObject>();
            var tooMany = new List<GameObject>();
            foreach (MapEntity entity in entities)
            {
                if (entity.Outputs.Count > MaxLogicOutputs)
                    tooMany.Add(entity.gameObject);

                foreach (MapOutput output in entity.Outputs)
                {
                    if (output == null)
                        continue;
                    if (!entity.Raises(output.onEvent))
                        wrongEvent.Add(entity.gameObject);
                    if (float.IsNaN(output.delay) || output.delay < 0f || output.delay > MaxLogicDelay)
                        badDelay.Add(entity.gameObject);

                    if (output.target == null)
                        noTarget.Add(entity.gameObject);
                    else if (output.target.gameObject.scene != entity.gameObject.scene)
                        foreign.Add(entity.gameObject);
                    else if (!output.target.Takes(output.input))
                        wrongInput.Add(entity.gameObject);
                }
            }

            if (noTarget.Count > 0)
                Add(v, KitIssueLevel.Warning, KitArea.Logic,
                    Tr($"{noTarget.Count} çıkışın hedefi yok; hiçbir şey yapmazlar."),
                    En($"{noTarget.Count} output(s) have no target and do nothing."), noTarget);
            if (foreign.Count > 0)
                Add(v, KitIssueLevel.Error, KitArea.Logic,
                    Tr($"{foreign.Count} çıkışın hedefi başka bir sahnede."),
                    En($"{foreign.Count} output(s) target an entity in another scene."), foreign);
            if (wrongInput.Count > 0)
                Add(v, KitIssueLevel.Error, KitArea.Logic,
                    Tr($"{wrongInput.Count} çıkış hedefinin almadığı bir giriş gönderiyor; oyun onları yok sayar."),
                    En($"{wrongInput.Count} output(s) send an input their target does not take; the game ignores them."), wrongInput);
            if (wrongEvent.Count > 0)
                Add(v, KitIssueLevel.Warning, KitArea.Logic,
                    Tr($"{wrongEvent.Count} çıkış varlığın hiç vermediği bir olayı bekliyor."),
                    En($"{wrongEvent.Count} output(s) wait for an event their entity never raises."), wrongEvent);
            if (badDelay.Count > 0)
                Add(v, KitIssueLevel.Error, KitArea.Logic,
                    Tr($"{badDelay.Count} çıkışın gecikmesi 0 ile {MaxLogicDelay} saniye arasında değil."),
                    En($"{badDelay.Count} output(s) have a delay outside 0 to {MaxLogicDelay} seconds."), badDelay);
            if (tooMany.Count > 0)
                Add(v, KitIssueLevel.Warning, KitArea.Logic,
                    Tr($"{tooMany.Count} varlığın {MaxLogicOutputs}'dan fazla çıkışı var; oyun fazlasını yok sayar."),
                    En($"{tooMany.Count} entities have more than {MaxLogicOutputs} outputs; the game ignores the rest."), tooMany);

            var loop = ZeroDelayLoop(entities);
            if (loop != null)
                Add(v, KitIssueLevel.Error, KitArea.Logic,
                    Tr($"Gecikmesiz döngü: {string.Join(" → ", loop.Select(e => e.name))}. Oyun onu {32} adımda keser; döngüdeki bir çıkışa gecikme ver."),
                    En($"A loop with no delay: {string.Join(" → ", loop.Select(e => e.name))}. The game cuts it at {32} steps; give one output in it a delay."),
                    loop.Select(e => e.gameObject));

            // Nothing that moves may be Static: batching bakes it into place, and its lightmap stays behind.
            var frozen = entities.OfType<MapMover>()
                .SelectMany(m => m.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject))
                .Where(o => (GameObjectUtility.GetStaticEditorFlags(o) & (StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI)) != 0)
                .Distinct()
                .ToList();
            if (frozen.Count > 0)
                Add(v, KitIssueLevel.Error, KitArea.Logic,
                    Tr($"Hareket eden {frozen.Count} obje (ya da çocuğu) Static işaretli. Static batching onu yerinde dondurur, bake edilen ışığı da geride kalır: Static'i kaldır, probe'lar onu gittiği yerde aydınlatır."),
                    En($"{frozen.Count} moving object(s) (or children) are marked Static. Static batching freezes them in place and their baked light stays behind: clear Static, and probes light them wherever they go."),
                    frozen);

            // A hurt zone switched on at the start over a spawn point hurts players the moment they appear.
            const MapMarkerKind spawnKinds = MapMarkerKind.HunterSpawn | MapMarkerKind.HiderSpawn | MapMarkerKind.LobbySpawn
                | MapMarkerKind.ModelerSpawn | MapMarkerKind.ArenaSpawn;
            var spawnPoints = objects.Select(o => o.GetComponent<MapMarker>()).Where(m => m != null && m.Is(spawnKinds))
                .Select(m => m.transform.position + Vector3.up * 0.9f).ToList();
            var onSpawns = entities.OfType<MapHurtZone>()
                .Where(z => z.StartEnabled && spawnPoints.Any(p => InBox(z.transform, z.Center, z.Size, p)))
                .Select(z => z.gameObject).ToList();
            if (onSpawns.Count > 0)
                Add(v, KitIssueLevel.Warning, KitArea.Logic,
                    Tr($"{onSpawns.Count} hasar bölgesi bir doğma noktasını kapsıyor; oyuncular doğar doğmaz hasar alır."),
                    En($"{onSpawns.Count} hurt zone(s) cover a spawn point; players are hurt the moment they appear."), onSpawns);

            CheckTeleports(v, entities.OfType<MapTeleport>().ToList());
            CheckSounds(v, entities.OfType<MapSound>().ToList());

            var still = entities.OfType<MapMover>().Where(m => m.Stops.Length == 0).Select(m => m.gameObject).ToList();
            if (still.Count > 0)
                Add(v, KitIssueLevel.Warning, KitArea.Logic,
                    Tr($"{still.Count} hareket edenin durağı yok; hiçbir yere gidemez."),
                    En($"{still.Count} mover(s) have no stops and cannot go anywhere."), still);

            var blind = entities.OfType<MapButton>().Where(b => b.GetComponentInChildren<Collider>(true) == null).Select(b => b.gameObject).ToList();
            if (blind.Count > 0)
                Add(v, KitIssueLevel.Warning, KitArea.Logic,
                    Tr($"{blind.Count} düğmenin collider'ı yok; oyuncu ona bakamaz, basamaz."),
                    En($"{blind.Count} button(s) have no collider; players cannot look at them or press them."), blind);

            var unnumbered = entities.Where(e => e.EntityId <= 0).Select(e => e.gameObject).ToList();
            var ids = entities.Where(e => e.EntityId > 0).GroupBy(e => e.EntityId).Where(g => g.Count() > 1).SelectMany(g => g).Select(e => e.gameObject).ToList();
            if (unnumbered.Count + ids.Count > 0)
                Add(v, KitIssueLevel.Info, KitArea.Logic,
                    "Bazı varlıkların ağ numarası yok ya da aynı; sahneyi kaydetmek (ya da paketlemek) numaraları verir.",
                    "Some entities have no network number, or share one; saving the scene (or packing) numbers them.",
                    unnumbered.Concat(ids));
        }

        /// <summary>A teleport needs somewhere to send players, and that place is judged the way a spawn
        /// point is: a player-sized capsule, nothing solid in it, a floor under it. A destination inside a
        /// teleport's own box (or another's) sends the player on again as soon as the game lets it, a second
        /// later, and again.</summary>
        /// <summary>Ladders (MapLadder): a box a player fits in, leaning at most like a ladder against a wall. The
        /// game climbs along the object's up; one leaned past 30 degrees or rolled onto its side is no ladder anyone
        /// climbs.</summary>
        static void CheckLadders(KitValidation v, List<GameObject> objects)
        {
            List<MapLadder> ladders = objects.Select(o => o.GetComponent<MapLadder>()).Where(l => l != null).ToList();
            if (ladders.Count == 0)
                return;

            var small = new List<GameObject>();
            var tilted = new List<GameObject>();
            foreach (MapLadder ladder in ladders)
            {
                Vector3 scale = ladder.transform.lossyScale;
                Vector3 size = new Vector3(Mathf.Abs(ladder.Size.x * scale.x), Mathf.Abs(ladder.Size.y * scale.y),
                    Mathf.Abs(ladder.Size.z * scale.z));
                if (size.x < 0.3f || size.y < 1f || size.z < 0.2f)
                    small.Add(ladder.gameObject);
                if (Vector3.Dot(ladder.transform.up, Vector3.up) < 0.866f || Mathf.Abs(ladder.transform.right.y) > 0.17f)
                    tilted.Add(ladder.gameObject);
            }

            if (small.Count > 0)
                Add(v, KitIssueLevel.Error, KitArea.Logic,
                    Tr($"{small.Count} merdivenin kutusu tırmanılamayacak kadar küçük: en az 0,3 m geniş, 1 m yüksek, 0,2 m derin olmalı."),
                    En($"{small.Count} ladder box(es) are too small to climb: at least 0.3 m wide, 1 m tall and 0.2 m deep."), small);
            if (tilted.Count > 0)
                Add(v, KitIssueLevel.Warning, KitArea.Logic,
                    Tr($"{tilted.Count} merdiven 30°'den fazla yatık ya da yana devrik. Merdiven yalnızca X'te, tepesi duvara doğru en çok 30° yatırılır."),
                    En($"{tilted.Count} ladder(s) lean more than 30° or are rolled onto a side. Tip a ladder on X only, top toward the wall, 30° at most."), tilted);
        }

        /// <summary>Map variants (MapVariantGroup): a group needs two versions to vary, a mode to vary in, and a version
        /// it can pick. An Occluder Static object inside a version is baked into occlusion culling as a wall that is
        /// always there, so whatever stands behind it vanishes in the rounds it is switched off. Spawn markers inside a
        /// version are fine and used only while it is on - said, so it is not a surprise.</summary>
        static void CheckVariants(KitValidation v, List<GameObject> objects)
        {
            List<MapVariantGroup> groups = objects.Select(o => o.GetComponent<MapVariantGroup>()).Where(g => g != null).ToList();
            List<MapVariantOption> options = objects.Select(o => o.GetComponent<MapVariantOption>()).Where(o => o != null).ToList();
            if (groups.Count == 0 && options.Count == 0)
                return;

            var single = new List<GameObject>();
            var noMode = new List<GameObject>();
            var noWeight = new List<GameObject>();
            var occluders = new List<GameObject>();
            var markers = new List<GameObject>();
            foreach (MapVariantGroup group in groups)
            {
                if (group.Count < 2)
                    single.Add(group.gameObject);
                if (group.Modes == MapMarkerModes.None)
                    noMode.Add(group.gameObject);

                bool any = false;
                for (int i = 0; i < group.Count; i++)
                    any |= group.WeightOf(i) > 0f;
                if (group.Count > 0 && !any)
                    noWeight.Add(group.gameObject);

                foreach (Transform child in group.transform)
                {
                    foreach (Transform t in child.GetComponentsInChildren<Transform>(true))
                    {
                        if ((GameObjectUtility.GetStaticEditorFlags(t.gameObject) & StaticEditorFlags.OccluderStatic) != 0)
                            occluders.Add(t.gameObject);
                        if (t.GetComponent<MapMarker>() != null)
                            markers.Add(t.gameObject);
                    }
                }
            }

            var stray = options.Where(o => o.transform.parent == null || o.transform.parent.GetComponent<MapVariantGroup>() == null)
                .Select(o => o.gameObject).ToList();

            if (noWeight.Count > 0)
                Add(v, KitIssueLevel.Error, KitArea.Logic,
                    Tr($"{noWeight.Count} varyasyon grubunda seçilebilecek çocuk yok: hepsinin ağırlığı 0."),
                    En($"{noWeight.Count} variant group(s) have no child that can be picked: every weight is 0."), noWeight);
            if (single.Count > 0)
                Add(v, KitIssueLevel.Warning, KitArea.Logic,
                    Tr($"{single.Count} varyasyon grubunun ikiden az çocuğu var; her round aynısı gelir. Her versiyon grubun doğrudan çocuğu olmalı."),
                    En($"{single.Count} variant group(s) have fewer than two children, so every round shows the same one. Each version must be a direct child of the group."), single);
            if (noMode.Count > 0)
                Add(v, KitIssueLevel.Warning, KitArea.Logic,
                    Tr($"{noMode.Count} varyasyon grubunda hiçbir mod seçili değil; hiçbir modda değişmez."),
                    En($"{noMode.Count} variant group(s) have no mode ticked, so they never change."), noMode);
            if (occluders.Count > 0)
                Add(v, KitIssueLevel.Warning, KitArea.Logic,
                    Tr($"Varyasyonların içinde {occluders.Count} obje Occluder Static işaretli. Occlusion culling onları hep orada sayar; kapalı oldukları roundlarda arkalarındaki şeyler görünmez olur. Bu işareti kaldır."),
                    En($"{occluders.Count} object(s) inside variants are Occluder Static. Occlusion culling treats them as always there, so whatever stands behind them disappears in the rounds they are off. Clear the flag."), occluders);
            if (stray.Count > 0)
                Add(v, KitIssueLevel.Warning, KitArea.Logic,
                    Tr($"{stray.Count} Map Variant Option bir varyasyon grubunun doğrudan çocuğunda değil; hiçbir şey yapmaz."),
                    En($"{stray.Count} Map Variant Option(s) are not on a direct child of a variant group and do nothing."), stray);
            if (markers.Count > 0)
                Add(v, KitIssueLevel.Info, KitArea.Logic,
                    Tr($"{markers.Count} işaretçi bir varyasyonun içinde; yalnızca o versiyon seçildiği roundlarda kullanılır."),
                    En($"{markers.Count} marker(s) sit inside a variant and are used only in the rounds that version is chosen."), markers);
        }

        static void CheckTeleports(KitValidation v, List<MapTeleport> teleports)
        {
            if (teleports.Count == 0)
                return;

            Physics.SyncTransforms();
            int mask = MapBoundsVolume.MapLayers;
            var nowhere = new List<GameObject>();
            var buried = new List<GameObject>();
            var floating = new List<GameObject>();
            var onward = new List<GameObject>();
            foreach (MapTeleport teleport in teleports)
            {
                if (teleport.Destination == null)
                {
                    nowhere.Add(teleport.gameObject);
                    continue;
                }

                Vector3 feet = teleport.Destination.position;
                if (Physics.CheckCapsule(feet + Vector3.up * PlayerBottom, feet + Vector3.up * PlayerTop, PlayerRadius - 0.02f, mask, QueryTriggerInteraction.Ignore))
                    buried.Add(teleport.gameObject);
                else if (!Physics.Raycast(feet + Vector3.up * PlayerBottom, Vector3.down, PlayerBottom + GroundSearch, mask, QueryTriggerInteraction.Ignore))
                    floating.Add(teleport.gameObject);

                Vector3 body = feet + Vector3.up * 0.9f;
                if (teleports.Any(other => other.StartEnabled && InBox(other.transform, other.Center, other.Size, body)))
                    onward.Add(teleport.gameObject);
            }

            if (nowhere.Count > 0)
                Add(v, KitIssueLevel.Error, KitArea.Logic,
                    Tr($"{nowhere.Count} ışınlayıcının hedefi yok. Hedef olarak boş bir obje koy; oyuncu onun yerine, onun baktığı yöne gider."),
                    En($"{nowhere.Count} teleport(s) have no destination. Give each an empty object: players arrive where it stands, facing where it faces."), nowhere);
            if (buried.Count > 0)
                Add(v, KitIssueLevel.Error, KitArea.Logic,
                    Tr($"{buried.Count} ışınlayıcının hedefi bir şeyin içinde. Oraya giden oyuncu sıkışır."),
                    En($"{buried.Count} teleport destination(s) are inside something. A player sent there gets stuck."), buried);
            if (floating.Count > 0)
                Add(v, KitIssueLevel.Warning, KitArea.Logic,
                    Tr($"{floating.Count} ışınlayıcı hedefinin altında {GroundSearch:0} metre içinde zemin yok."),
                    En($"{floating.Count} teleport destination(s) have no floor within {GroundSearch:0} metres below."), floating);
            if (onward.Count > 0)
                Add(v, KitIssueLevel.Warning, KitArea.Logic,
                    Tr($"{onward.Count} ışınlayıcının hedefi açık başlayan bir ışınlayıcının kutusunda; oyuncu oraya varınca bir saniye sonra yine ışınlanır."),
                    En($"{onward.Count} teleport destination(s) stand in the box of a teleport that starts enabled; a player arriving there is sent on again a second later."), onward);
        }

        /// <summary>The game's sound limits (KitMapRules: 32 MB together, two minutes each, mono or stereo up
        /// to 48 kHz, no tracker modules), estimated from the clips' import settings. Pack measures the
        /// real bundle.</summary>
        static void CheckSounds(KitValidation v, List<MapSound> sounds)
        {
            var noClip = new List<GameObject>();
            var refused = new List<GameObject>();
            var reasons = new SortedSet<string>();
            double total = 0d;

            foreach (MapSound sound in sounds)
            {
                AudioClip clip = sound.Clip;
                if (clip == null)
                {
                    noClip.Add(sound.gameObject);
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(clip);
                string extension = Path.GetExtension(path).ToLowerInvariant();
                bool tracker = extension == ".mod" || extension == ".xm" || extension == ".it" || extension == ".s3m";
                bool wrong = false;
                if (tracker)
                {
                    reasons.Add(T("tracker modülü (.mod, .xm, .it, .s3m)", "a tracker module (.mod, .xm, .it, .s3m)"));
                    wrong = true;
                }
                if (clip.channels > 2)
                {
                    reasons.Add(T("ikiden çok kanal (Import Settings'te Force To Mono ya da stereo bir dosya)", "more than two channels"));
                    wrong = true;
                }
                if (clip.frequency > 48000)
                {
                    reasons.Add(T("48000 Hz'ten yüksek (Import Settings'te Sample Rate Setting'i 48000'e çek)", "above 48000 Hz (set Sample Rate Setting to 48000)"));
                    wrong = true;
                }
                if (clip.length > KitMapRules.MaxAudioSeconds)
                {
                    reasons.Add(T($"{KitMapRules.MaxAudioSeconds:0} saniyeden uzun", $"longer than {KitMapRules.MaxAudioSeconds:0} seconds"));
                    wrong = true;
                }
                if (wrong)
                    refused.Add(sound.gameObject);

                // Decompress On Load holds 16-bit PCM of the whole clip; the others about an eighth of it.
                double pcm = clip.length * clip.frequency * Math.Max(1, clip.channels) * 2d;
                bool decompressed = AssetImporter.GetAtPath(path) is AudioImporter importer
                    && importer.defaultSampleSettings.loadType == AudioClipLoadType.DecompressOnLoad;
                total += decompressed ? pcm : pcm / 8d;
            }

            if (noClip.Count > 0)
                Add(v, KitIssueLevel.Warning, KitArea.Logic,
                    Tr($"{noClip.Count} sesin klibi yok; çalmazlar."),
                    En($"{noClip.Count} sound(s) have no clip and play nothing."), noClip);
            if (refused.Count > 0)
                Add(v, KitIssueLevel.Error, KitArea.Logic,
                    Tr($"{refused.Count} sesi oyun reddeder: {string.Join("; ", reasons)}."),
                    En($"The game refuses {refused.Count} sound(s): {string.Join("; ", reasons)}."), refused);
            if (total > KitMapRules.MaxAudioBytes)
                Add(v, KitIssueLevel.Error, KitArea.Logic,
                    Tr($"Sesler bellekte yaklaşık {total / 1048576d:0} MB tutar; sınır {KitMapRules.MaxAudioBytes >> 20} MB. Uzun sesleri Compressed In Memory ya da Streaming yükle, kısalt ya da azalt."),
                    En($"The sounds take about {total / 1048576d:0} MB in memory; the limit is {KitMapRules.MaxAudioBytes >> 20} MB. Load long ones Compressed In Memory or Streaming, shorten them or use fewer."),
                    sounds.Select(s => s.gameObject));
        }

        static string T(string tr, string en) => KitEditorLanguage.T(tr, en);

        static bool InBox(Transform box, Vector3 center, Vector3 size, Vector3 worldPoint)
        {
            Vector3 local = box.InverseTransformPoint(worldPoint) - center;
            return Mathf.Abs(local.x) <= Mathf.Abs(size.x) * 0.5f && Mathf.Abs(local.y) <= Mathf.Abs(size.y) * 0.5f
                && Mathf.Abs(local.z) <= Mathf.Abs(size.z) * 0.5f;
        }

        /// <summary>Whether this input makes its target raise an event in the same step, as the game's
        /// MapLogic.Apply does: a press, a relay's trigger, a counter's change, a mover sent somewhere (or let
        /// go on), the activator inputs. Switching things on and off, sounds and effects raise nothing then:
        /// a trigger notices players on the game's next check, a sound finishes when its clip has played.
        /// </summary>
        static bool RaisesAtOnce(MapEntity target, MapInput input)
        {
            switch (target)
            {
                case MapButton _:
                    return input == MapInput.Press;
                case MapRelay _:
                    return input == MapInput.Trigger;
                case MapCounter _:
                    return input == MapInput.Add || input == MapInput.Subtract || input == MapInput.SetValue || input == MapInput.ResetCount;
                case MapMover _:
                    // Enable and Toggle carry on a journey that Disable paused, which can start it again.
                    return input != MapInput.Pause && input != MapInput.Disable;
                case MapHurtZone _:
                    return input == MapInput.HurtActivator;
                case MapTeleport _:
                    return input == MapInput.TeleportActivator;
                default:
                    return false;
            }
        }

        /// <summary>A cycle of outputs without a delay, as the entities along it, or null. An input with no
        /// delay runs in the same frame as the event that caused it, so such a cycle never waits. Only inputs
        /// that make their target raise an event there and then close one (RaisesAtOnce): a button switching
        /// itself off, or a tune that starts the other one when it finishes, is no loop.</summary>
        static List<MapEntity> ZeroDelayLoop(List<MapEntity> entities)
        {
            var edges = new Dictionary<MapEntity, List<MapEntity>>();
            foreach (MapEntity entity in entities)
            {
                edges[entity] = entity.Outputs
                    .Where(o => o != null && o.target != null && o.delay <= 0f && o.target.Takes(o.input) && RaisesAtOnce(o.target, o.input)
                        && o.target.Outputs.Count > 0)
                    .Select(o => o.target)
                    .Distinct()
                    .ToList();
            }

            var state = new Dictionary<MapEntity, int>();
            var path = new List<MapEntity>();

            List<MapEntity> Visit(MapEntity entity)
            {
                state[entity] = 1;
                path.Add(entity);
                if (edges.TryGetValue(entity, out List<MapEntity> next))
                {
                    foreach (MapEntity target in next)
                    {
                        state.TryGetValue(target, out int seen);
                        if (seen == 1)
                            return path.Skip(path.IndexOf(target)).Concat(new[] { target }).ToList();
                        if (seen == 0)
                        {
                            List<MapEntity> found = Visit(target);
                            if (found != null)
                                return found;
                        }
                    }
                }
                path.RemoveAt(path.Count - 1);
                state[entity] = 2;
                return null;
            }

            foreach (MapEntity entity in entities)
            {
                if (!state.ContainsKey(entity))
                {
                    List<MapEntity> found = Visit(entity);
                    if (found != null)
                        return found;
                }
            }
            return null;
        }

        /// <summary>A player-sized capsule at every spawn point: inside something solid, or with no floor
        /// under it within a few metres.</summary>
        static void CheckSpawnPlacement(KitValidation v, List<MapMarker> markers)
        {
            Physics.SyncTransforms();
            int mask = MapBoundsVolume.MapLayers;
            const MapMarkerKind spawns = MapMarkerKind.HunterSpawn | MapMarkerKind.HiderSpawn | MapMarkerKind.LobbySpawn
                | MapMarkerKind.ModelerSpawn | MapMarkerKind.ArenaSpawn;

            var buried = new List<GameObject>();
            var floating = new List<GameObject>();
            foreach (MapMarker marker in markers)
            {
                if ((marker.Kind & spawns) == 0)
                    continue;
                Vector3 feet = marker.transform.position;
                if (Physics.CheckCapsule(feet + Vector3.up * PlayerBottom, feet + Vector3.up * PlayerTop, PlayerRadius - 0.02f, mask, QueryTriggerInteraction.Ignore))
                    buried.Add(marker.gameObject);
                else if (!Physics.Raycast(feet + Vector3.up * PlayerBottom, Vector3.down, PlayerBottom + GroundSearch, mask, QueryTriggerInteraction.Ignore))
                    floating.Add(marker.gameObject);
            }

            if (buried.Count > 0)
                Add(v, KitIssueLevel.Error, KitArea.Markers,
                    Tr($"{buried.Count} doğma noktası bir şeyin içinde. Oyuncu orada doğarsa sıkışır."),
                    En($"{buried.Count} spawn point(s) are inside something. A player spawning there gets stuck."), buried);
            if (floating.Count > 0)
                Add(v, KitIssueLevel.Warning, KitArea.Markers,
                    Tr($"{floating.Count} doğma noktasının altında {GroundSearch:0} metre içinde zemin yok."),
                    En($"{floating.Count} spawn point(s) have no floor within {GroundSearch:0} metres below."), floating);
        }

        // ------------------------------------------------------------------ bounds

        static void CheckBounds(KitValidation v, List<GameObject> objects)
        {
            var volumes = objects.Select(o => o.GetComponent<MapBoundsVolume>()).Where(b => b != null).ToList();
            if (volumes.Count == 0)
            {
                Add(v, KitIssueLevel.Error, KitArea.Bounds,
                    "Sahnede MapBoundsVolume yok. Haritayı saran bir kutu ekle ve bake et: oyun, haritanın dışına düşen oyuncuyu bununla geri alır.",
                    "The scene has no MapBoundsVolume. Add a box around the map and bake it: the game uses it to bring back players who fall out.");
                return;
            }

            foreach (MapBoundsVolume volume in volumes)
            {
                var target = new UnityEngine.Object[] { volume.gameObject };
                if (!volume.IsBaked)
                    Add(v, KitIssueLevel.Error, KitArea.Bounds,
                        Tr($"'{volume.name}' bake edilmemiş. Seçip Inspector'daki Bake düğmesine bas."),
                        En($"'{volume.name}' is not baked. Select it and press Bake in the Inspector."), target);
                else if (volume.IsStale)
                    Add(v, KitIssueLevel.Error, KitArea.Bounds,
                        Tr($"'{volume.name}' bake edildikten sonra taşınmış ya da boyutu değişmiş. Yeniden bake et."),
                        En($"'{volume.name}' was moved or resized after it was baked. Bake it again."), target);
                else
                {
                    if (volume.HasEscapePath)
                        Add(v, KitIssueLevel.Warning, KitArea.Bounds,
                            Tr($"'{volume.name}': oyun alanı kutunun duvarına ulaşıyor ({volume.EscapePoint.x:0.0}, {volume.EscapePoint.y:0.0}, {volume.EscapePoint.z:0.0}, en dar yeri {volume.EscapeWidth:0.00} m). Kapalı bir haritaysa orada bir delik var."),
                            En($"'{volume.name}': the play area reaches the box's wall ({volume.EscapePoint.x:0.0}, {volume.EscapePoint.y:0.0}, {volume.EscapePoint.z:0.0}, {volume.EscapeWidth:0.00} m at its narrowest). On a closed map, there is a hole there."), target);
                    if (volume.NarrowCellCount > 0)
                        Add(v, KitIssueLevel.Warning, KitArea.Bounds,
                            Tr($"'{volume.name}': {volume.NarrowCellCount} sızıntı adayı hücre var. Sahnede kırmızı çizilenlere bak."),
                            En($"'{volume.name}': {volume.NarrowCellCount} possible leak cell(s). Look for the red ones in the scene."), target);
                }
            }

            // Everywhere the game puts a player has to be inside what the bake reached: the game pulls anyone
            // outside it somewhere else a few seconds later. The easy way to miss it: a volume with Seeds
            // filled in starts its fill from those alone, not from the spawn points.
            List<MapBoundsVolume> baked = volumes.Where(b => b.IsBaked && !b.IsStale).ToList();
            if (baked.Count < volumes.Count)
                return;

            const MapMarkerKind spawnKinds = MapMarkerKind.HunterSpawn | MapMarkerKind.HiderSpawn | MapMarkerKind.LobbySpawn
                | MapMarkerKind.ModelerSpawn | MapMarkerKind.ArenaSpawn;
            var strandedSpawns = objects.Select(o => o.GetComponent<MapMarker>())
                .Where(m => m != null && m.Is(spawnKinds) && !baked.Any(b => b.Contains(m.transform.position)))
                .Select(m => m.gameObject).ToList();
            var strandedArrivals = objects.SelectMany(o => o.GetComponents<MapTeleport>())
                .Where(t => t.Destination != null && !baked.Any(b => b.Contains(t.Destination.position)))
                .Select(t => t.gameObject).ToList();

            if (strandedSpawns.Count > 0)
                Add(v, KitIssueLevel.Error, KitArea.Bounds,
                    Tr($"{strandedSpawns.Count} doğma noktası bake edilen oyun alanının dışında: oyun orada doğanı birkaç saniye içinde başka bir yere çeker. Noktayı taşı ya da yeniden bake et. Hacmin Seeds listesi doluysa dolgu yalnızca onlardan başlar."),
                    En($"{strandedSpawns.Count} spawn point(s) are outside the baked play area: the game pulls a player who spawns there somewhere else within seconds. Move them, or bake again. With the volume's Seeds filled in, the fill starts from those alone."),
                    strandedSpawns);
            if (strandedArrivals.Count > 0)
                Add(v, KitIssueLevel.Error, KitArea.Bounds,
                    Tr($"{strandedArrivals.Count} ışınlayıcının hedefi bake edilen oyun alanının dışında: oraya gidenin yeri oyunca kayıp sayılır. Kapısı olmayan bir odaysa hacmin Seeds listesine orada bir nokta ekle, ama haritanın geri kalanı için de bir tane."),
                    En($"{strandedArrivals.Count} teleport destination(s) are outside the baked play area: the game takes a player sent there as lost. For a room with no door, add a point in it to the volume's Seeds, and one for the rest of the map too."),
                    strandedArrivals);
        }

        // ------------------------------------------------------------------ budget

        static void CheckBudget(KitValidation v, List<GameObject> objects)
        {
            var meshes = new HashSet<Mesh>();
            var textures = new HashSet<Texture>();

            foreach (GameObject go in objects)
            {
                if (go.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
                    meshes.Add(filter.sharedMesh);
                if (go.TryGetComponent(out MeshCollider collider) && collider.sharedMesh != null)
                    meshes.Add(collider.sharedMesh);
                if (go.TryGetComponent(out Renderer renderer))
                {
                    v.Renderers++;
                    foreach (Material material in renderer.sharedMaterials)
                        AddTextures(textures, material);
                }
                if (go.TryGetComponent(out ReflectionProbe probe) && probe.bakedTexture != null)
                    textures.Add(probe.bakedTexture);
            }

            AddTextures(textures, RenderSettings.skybox);
            foreach (LightmapData map in LightmapSettings.lightmaps)
            {
                if (map.lightmapColor != null) textures.Add(map.lightmapColor);
                if (map.lightmapDir != null) textures.Add(map.lightmapDir);
                if (map.shadowMask != null) textures.Add(map.shadowMask);
            }

            foreach (Mesh mesh in meshes)
            {
                v.Vertices += mesh.vertexCount;
                for (int i = 0; i < mesh.subMeshCount; i++)
                    v.Triangles += mesh.GetIndexCount(i) / 3;
            }
            v.Meshes = meshes.Count;

            Texture largest = null;
            foreach (Texture texture in textures)
            {
                v.TextureBytes += TextureBytes(texture);
                long side = Math.Max(texture.width, texture.height);
                if (side > v.LargestTextureSide)
                {
                    v.LargestTextureSide = side;
                    largest = texture;
                }
            }
            v.Textures = textures.Count;

            if (v.Renderers > KitMapRules.MaxRenderers)
                Add(v, KitIssueLevel.Error, KitArea.Budget,
                    Tr($"{v.Renderers} renderer var. En çok {KitMapRules.MaxRenderers} olabilir."),
                    En($"There are {v.Renderers} renderers. The limit is {KitMapRules.MaxRenderers}."));
            if (v.Triangles > KitMapRules.MaxTriangles)
                Add(v, KitIssueLevel.Error, KitArea.Budget,
                    Tr($"Haritanın meshleri {v.Triangles:N0} üçgen. En çok {KitMapRules.MaxTriangles:N0} olabilir."),
                    En($"The map's meshes have {v.Triangles:N0} triangles. The limit is {KitMapRules.MaxTriangles:N0}."));
            if (v.Vertices > KitMapRules.MaxVertices)
                Add(v, KitIssueLevel.Error, KitArea.Budget,
                    Tr($"Haritanın meshleri {v.Vertices:N0} vertex. En çok {KitMapRules.MaxVertices:N0} olabilir."),
                    En($"The map's meshes have {v.Vertices:N0} vertices. The limit is {KitMapRules.MaxVertices:N0}."));
            if (v.TextureBytes > KitMapRules.MaxTextureBytes)
                Add(v, KitIssueLevel.Error, KitArea.Budget,
                    Tr($"Dokular yaklaşık {v.TextureBytes / 1048576.0:0} MB. En çok {KitMapRules.MaxTextureBytes >> 20} MB olabilir: büyük dokuların Max Size'ını düşür ya da sıkıştır."),
                    En($"The textures take about {v.TextureBytes / 1048576.0:0} MB. The limit is {KitMapRules.MaxTextureBytes >> 20} MB: lower Max Size on the big ones, or compress them."));
            if (v.LargestTextureSide > KitMapRules.MaxTextureSide)
                Add(v, KitIssueLevel.Error, KitArea.Budget,
                    Tr($"'{largest.name}' {largest.width}x{largest.height}. Bir kenar en çok {KitMapRules.MaxTextureSide} olabilir."),
                    En($"'{largest.name}' is {largest.width}x{largest.height}. A side may be at most {KitMapRules.MaxTextureSide}."),
                    new UnityEngine.Object[] { largest });
        }

        static void AddTextures(HashSet<Texture> into, Material material)
        {
            if (material == null)
                return;
            foreach (int id in material.GetTexturePropertyNameIDs())
            {
                Texture texture = material.GetTexture(id);
                if (texture != null)
                    into.Add(texture);
            }
        }

        /// <summary>What a texture will occupy on the GPU in this format, all its mips and faces. An estimate
        /// in the editor's format for the current platform - Pack reports the real number.</summary>
        static long TextureBytes(Texture texture)
        {
            long layers = texture is Cubemap ? 6 : texture is Texture3D t3 ? t3.depth : texture is Texture2DArray t2a ? t2a.depth : 1;
            long bytes = 0;
            int w = texture.width, h = texture.height;
            for (int mip = 0; mip < Math.Max(1, texture.mipmapCount); mip++)
            {
                bytes += GraphicsFormatUtility.ComputeMipmapSize(Math.Max(1, w >> mip), Math.Max(1, h >> mip), texture.graphicsFormat);
            }
            return bytes * layers;
        }
    }
}

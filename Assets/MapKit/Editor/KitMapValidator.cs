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

    public enum KitArea { Scene, MapInfo, Components, Materials, Layers, Lighting, Markers, Bounds, Budget, Packaging }

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

        public static KitValidation Run(Scene scene)
        {
            var v = new KitValidation { SceneName = scene.name, MapId = KitMapId.SanitizeName(scene.name) };

            var objects = new List<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    objects.Add(t.gameObject);
            }

            CheckScene(v, scene);
            KitMapDescriptor descriptor = CheckDescriptor(v, objects);
            CheckComponents(v, objects);
            CheckMaterials(v, objects);
            CheckLayers(v, objects);
            CheckLighting(v, scene, objects);
            CheckMarkers(v, objects, descriptor);
            CheckBounds(v, objects);
            CheckBudget(v, objects);
            CheckPackaging(v);
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

            MapMarkerModes modes = descriptor.Modes;
            if (modes == MapMarkerModes.None)
                Add(v, KitIssueLevel.Error, KitArea.MapInfo,
                    "Harita hiçbir mod için işaretlenmemiş. Descriptor'da en az Standard ya da Practice seç.",
                    "The map is not marked for any mode. Tick at least Standard or Practice on the descriptor.", target);

            MapMarkerModes unsupported = modes & ~(MapMarkerModes.Standard | MapMarkerModes.Practice);
            if (unsupported != MapMarkerModes.None)
                Add(v, KitIssueLevel.Warning, KitArea.MapInfo,
                    Tr($"Kit haritaları şimdilik yalnızca Standard ve Practice'te oynanıyor. {unsupported} yok sayılır."),
                    En($"Kit maps are played in Standard and Practice only, for now. {unsupported} will be ignored."), target);

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

            var unused = markers.Where(m => m.Modes == MapMarkerModes.None).ToList();
            if (unused.Count > 0)
                Add(v, KitIssueLevel.Warning, KitArea.Markers,
                    Tr($"{unused.Count} işaretçide hiçbir mod seçili değil. Hiçbir modda kullanılmazlar."),
                    En($"{unused.Count} marker(s) have no mode ticked. No mode will use them."), unused.Select(m => m.gameObject));

            CheckSpawnPlacement(v, markers);
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

        // ------------------------------------------------------------------ packaging

        static void CheckPackaging(KitValidation v)
        {
            foreach (var (target, name) in new[] { (BuildTarget.StandaloneWindows64, "Windows"), (BuildTarget.StandaloneLinux64, "Linux"), (BuildTarget.StandaloneOSX, "macOS") })
            {
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                    Add(v, KitIssueLevel.Warning, KitArea.Packaging,
                        Tr($"{name} Build Support kurulu değil. {name} oyuncuları bu haritayı yükleyemez. Unity Hub > Installs > Add modules."),
                        En($"{name} Build Support is not installed. {name} players will not be able to load this map. Unity Hub > Installs > Add modules."));
            }
        }
    }
}

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mimicraft.Gameplay;
using Mimicraft.MapKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Mimicraft.EditorTools
{
    /// <summary>
    /// Map Kit > New Map: a new map scene with everything the game needs already in place, so an author
    /// starts from something that validates rather than from an empty scene and a list of rules.
    ///
    /// What it makes: the descriptor, lighting settings the game's shaders are built for (Shadowmask,
    /// Directional, no realtime GI), a probe volume, spawn markers for Standard, Practice and Deathmatch, a baked map
    /// bounds box - and, unless asked not to, a small closed example room with a few props and two lamps,
    /// so the first bake and the first walk have something to show. Only the lighting is left to bake,
    /// because that takes minutes and the author should choose when.
    ///
    /// Markers and the descriptor are filled through SerializedObject rather than new setters: their
    /// fields are part of the frozen map format (KitAssemblyAudit), and an editor tool does not need an API
    /// for them.
    /// </summary>
    public sealed class KitNewMapWindow : EditorWindow
    {
        string mapName = "";
        string author = "";
        string description = "";
        bool standard = true;
        bool practice = true;
        bool deathmatch = true;
        bool exampleRoom = true;
        string folder = "Assets/Maps";

        // Empty while the scene name follows the map name. A name in another alphabet leaves nothing to
        // follow, so the author can always write one.
        string sceneNameEdit = "";

        // Ids of the scenes already in the project, once per window: two maps with one id would install
        // over each other in the game, and FindAssets is too slow to run on every repaint.
        HashSet<string> takenIds;

        [MenuItem("Map Kit/New Map", priority = 1)]
        public static void Open()
        {
            var window = GetWindow<KitNewMapWindow>(true, "Map Kit", true);
            window.minSize = new Vector2(420, 360);
        }

        static string T(string tr, string en) => KitEditorLanguage.T(tr, en);

        void OnEnable()
        {
            takenIds = new HashSet<string>(AssetDatabase.FindAssets("t:Scene", new[] { "Assets" })
                .Select(guid => KitMapId.SanitizeName(Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)))));
        }

        void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(T("Yeni harita", "New map"), EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                KitEditorLanguage.Toolbar();
            }

            EditorGUILayout.Space(6);
            mapName = EditorGUILayout.TextField(T("Harita adı", "Map name"), mapName);
            author = EditorGUILayout.TextField(T("Yapımcı", "Author"), author);
            EditorGUILayout.LabelField(T("Açıklama", "Description"));
            description = EditorGUILayout.TextArea(description, GUILayout.MinHeight(48));

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(T("Modlar", "Modes"));
                standard = GUILayout.Toggle(standard, "Standard");
                practice = GUILayout.Toggle(practice, "Practice");
                deathmatch = GUILayout.Toggle(deathmatch, "Deathmatch");
            }
            exampleRoom = EditorGUILayout.ToggleLeft(
                T("Örnek oda ekle (bir oda, bir kapı, birkaç eşya, iki lamba)", "Add an example room (a room, a door, a few props, two lamps)"),
                exampleRoom);

            using (new EditorGUILayout.HorizontalScope())
            {
                folder = EditorGUILayout.TextField(T("Klasör", "Folder"), folder);
                if (GUILayout.Button("...", GUILayout.Width(28)))
                    PickFolder();
            }

            string derived = KitNewMap.SceneNameFor(mapName);
            string sceneName = string.IsNullOrEmpty(sceneNameEdit) ? derived : sceneNameEdit;
            string typed = EditorGUILayout.TextField(T("Sahne adı", "Scene name"), sceneName).Trim();
            if (typed != sceneName)
                sceneNameEdit = typed == derived ? "" : typed;
            sceneName = string.IsNullOrEmpty(sceneNameEdit) ? derived : sceneNameEdit;
            string scenePath = KitNewMap.ScenePathFor(folder, sceneName);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(string.Format(KitEditorLanguage.Culture, T("Sahne: {0}\nKimlik: {1}", "Scene: {0}\nId: {1}"),
                scenePath, KitMapId.SanitizeName(sceneName)), EditorStyles.wordWrappedMiniLabel);

            string problem = Problem(sceneName, scenePath);
            if (problem != null)
                EditorGUILayout.HelpBox(problem, MessageType.Warning);

            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(problem != null))
            {
                if (GUILayout.Button(T("Oluştur", "Create"), GUILayout.Height(28)))
                    Create(sceneName, scenePath);
            }
        }

        string Problem(string sceneName, string scenePath)
        {
            if (string.IsNullOrWhiteSpace(mapName))
                return T("Haritaya bir ad ver.", "Give the map a name.");
            if (mapName.Length > 64)
                return T("Ad en çok 64 karakter olabilir.", "The name can be at most 64 characters.");
            if (author.Length > 32)
                return T("Yapımcı adı en çok 32 karakter olabilir.", "The author name can be at most 32 characters.");
            if (description.Length > 512)
                return T("Açıklama en çok 512 karakter olabilir.", "The description can be at most 512 characters.");
            if (!standard && !practice && !deathmatch)
                return T("En az bir mod seç.", "Pick at least one mode.");
            string normalized = folder.Replace('\\', '/').TrimEnd('/');
            if (normalized != "Assets" && !normalized.StartsWith("Assets/", System.StringComparison.Ordinal))
                return T("Klasör projenin Assets klasörünün içinde olmalı.", "The folder must be inside the project's Assets folder.");
            if (!KitMapId.IsValidLocalName(sceneName.ToLowerInvariant()))
                return T("Sahne adında yalnızca İngilizce harfler, rakamlar, '-' ve '_' olabilir, en çok 23 karakter: haritanın kimliği ondan çıkar.",
                    "The scene name may only use English letters, digits, '-' and '_', up to 23 characters: the map's id comes from it.");
            if (KitMapRules.IsSceneNameReserved(sceneName))
                return T("Bu ad oyunun kendi sahnelerine ayrılmış. Başka bir ad ver.", "That name is kept for the game's own scenes. Pick another.");
            if (File.Exists(scenePath) || Directory.Exists(Path.GetDirectoryName(scenePath)))
                return T("Bu adda bir harita klasörü zaten var.", "A map folder with this name already exists.");
            if (takenIds != null && takenIds.Contains(KitMapId.SanitizeName(sceneName)))
                return T("Projede bu kimliği alan bir sahne zaten var. Oyunda biri ötekinin üzerine kurulur; başka bir ad ver.",
                    "A scene in the project already has this id. In the game one would install over the other; pick another name.");
            return null;
        }

        void PickFolder()
        {
            string picked = EditorUtility.OpenFolderPanel(T("Haritaların klasörü", "Folder for maps"), folder, "");
            if (string.IsNullOrEmpty(picked))
                return;
            string project = Path.GetFullPath(Application.dataPath + "/..").Replace('\\', '/');
            picked = picked.Replace('\\', '/');
            if (picked.StartsWith(project + "/", System.StringComparison.OrdinalIgnoreCase))
                folder = picked.Substring(project.Length + 1);
        }

        void Create(string sceneName, string scenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var options = new KitNewMap.Options
            {
                MapName = mapName.Trim(),
                Author = author.Trim(),
                Description = description.Trim(),
                Modes = (standard ? MapMarkerModes.Standard : 0) | (practice ? MapMarkerModes.Practice : 0)
                    | (deathmatch ? MapMarkerModes.Deathmatch : 0),
                ExampleRoom = exampleRoom,
                SceneName = sceneName,
                ScenePath = scenePath,
            };

            if (!KitNewMap.Create(options, out string error))
            {
                EditorUtility.DisplayDialog("Map Kit", error, T("Tamam", "OK"));
                return;
            }

            Close();
            KitMapValidatorWindow.Open();
            EditorUtility.DisplayDialog("Map Kit", T(
                "Harita oluşturuldu. Sıradakiler:\n\n1. Işığı bake et: Window > Rendering > Lighting > Generate Lighting.\n2. Map Kit > Validate ile denetle.\n3. Map Kit > Pack and Test ile oyunda dene.",
                "The map is ready. Next:\n\n1. Bake the lighting: Window > Rendering > Lighting > Generate Lighting.\n2. Check it with Map Kit > Validate.\n3. Try it in the game with Map Kit > Pack and Test."),
                T("Tamam", "OK"));
        }
    }

    /// <summary>Builds a new map scene. Kept apart from the window so a map can be made from code too
    /// (the kit's own examples, a test).</summary>
    public static class KitNewMap
    {
        public sealed class Options
        {
            public string MapName = "", Author = "", Description = "";
            public MapMarkerModes Modes = MapMarkerModes.Standard | MapMarkerModes.Practice | MapMarkerModes.Deathmatch;
            public bool ExampleRoom = true;
            public string SceneName = "", ScenePath = "";
        }

        /// <summary>A scene name from a map name: letters and digits only, Turkish letters written without
        /// their marks (the map id keeps only a-z, 0-9, '-' and '_'), words joined with capitals. No longer
        /// than an id may be, so the id is the scene name in lower case and nothing is cut off silently.
        ///
        /// Capitals rather than underscores between the words: "Map of Doom" would otherwise become
        /// Map_of_Doom, which starts with one of the prefixes kept for the game's own scenes.</summary>
        public static string SceneNameFor(string mapName)
        {
            var text = new StringBuilder();
            bool upper = true;
            foreach (char raw in (mapName ?? "").Trim())
            {
                char c = Plain(raw);
                if (char.IsLetterOrDigit(c) && c < 128)
                {
                    text.Append(upper ? char.ToUpperInvariant(c) : c);
                    upper = false;
                }
                else
                {
                    upper = true;
                }
            }
            string name = text.ToString();
            if (name.Length == 0)
                name = "NewMap";
            if (char.IsDigit(name[0]))
                name = "Map" + name;
            return name.Length > KitMapId.MaxLocalName ? name.Substring(0, KitMapId.MaxLocalName) : name;
        }

        /// <summary>A letter without its marks: ç to c, é to e, ñ to n, through Unicode decomposition. The
        /// dotless ı has nothing to decompose into and is mapped by hand. Anything else outside ASCII comes
        /// back as it is and is dropped by the caller.</summary>
        static char Plain(char c)
        {
            if (c < 128)
                return c;
            if (c == 'ı')
                return 'i';
            string parts = c.ToString().Normalize(NormalizationForm.FormD);
            return parts.Length > 0 && parts[0] < 128 ? parts[0] : c;
        }

        public static string ScenePathFor(string folder, string sceneName) =>
            $"{folder.TrimEnd('/', '\\').Replace('\\', '/')}/{sceneName}/{sceneName}.unity";

        public static bool Create(Options options, out string error)
        {
            error = null;
            string mapFolder = Path.GetDirectoryName(options.ScenePath).Replace('\\', '/');

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                error = KitEditorLanguage.T("URP Lit shader'ı bulunamadı. Proje Universal Render Pipeline kullanmıyor gibi.",
                    "The URP Lit shader was not found. The project does not seem to use the Universal Render Pipeline.");
                return false;
            }

            Directory.CreateDirectory(mapFolder);
            Directory.CreateDirectory(mapFolder + "/Materials");
            AssetDatabase.Refresh();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Fog off, and in the one mode the game keeps shaders for, so switching it on stays legal.
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fog = false;
            RenderSettings.fogDensity = 0.01f;

            var map = new GameObject("Map");
            var descriptor = map.AddComponent<KitMapDescriptor>();
            var info = new SerializedObject(descriptor);
            info.FindProperty("mapName").stringValue = options.MapName;
            info.FindProperty("author").stringValue = options.Author;
            info.FindProperty("description").stringValue = options.Description;
            info.FindProperty("modes").intValue = (int)options.Modes;
            info.ApplyModifiedPropertiesWithoutUndo();

            Transform lighting = Child(map.transform, "Lighting");
            var sun = Lamp(lighting, "Sun", LightType.Directional, Vector3.zero, new Vector3(50f, -30f, 0f), new Color(1f, 0.96f, 0.88f), 1.2f, 0f, LightShadows.Soft);
            RenderSettings.sun = sun;

            var probes = new GameObject("Probe Volume").AddComponent<ProbeVolume>();
            probes.transform.SetParent(lighting, false);
            probes.mode = ProbeVolume.Mode.Scene;

            var materials = new Dictionary<string, Material>
            {
                ["Floor"] = MaterialAsset(mapFolder, lit, "Floor", new Color(0.46f, 0.47f, 0.49f)),
                ["Wall"] = MaterialAsset(mapFolder, lit, "Wall", new Color(0.83f, 0.8f, 0.74f)),
                ["Prop"] = MaterialAsset(mapFolder, lit, "Prop", new Color(0.55f, 0.38f, 0.24f)),
                ["Accent"] = MaterialAsset(mapFolder, lit, "Accent", new Color(0.24f, 0.42f, 0.62f)),
            };

            Transform geometry = Child(map.transform, "Geometry");
            Block(geometry, "Floor", new Vector3(0f, -0.1f, 0f), new Vector3(20f, 0.2f, 16f), materials["Floor"]);
            if (options.ExampleRoom)
                ExampleRoom(geometry, lighting, materials);

            Transform markers = Child(map.transform, "Markers");
            MapMarkerModes modes = options.Modes;

            // Hunter spawns for Standard, and for Deathmatch, which pools every spawn kind: with them the
            // room has six points for it, the validator's minimum. Practice has no Hunters.
            MapMarkerModes hunterModes = modes & (MapMarkerModes.Standard | MapMarkerModes.Deathmatch);
            if (hunterModes != MapMarkerModes.None)
            {
                Marker(markers, "Hunter Spawn 1", new Vector3(7f, 0f, -4f), 270f, MapMarkerKind.HunterSpawn, hunterModes);
                Marker(markers, "Hunter Spawn 2", new Vector3(7f, 0f, 4f), 270f, MapMarkerKind.HunterSpawn, hunterModes);
            }
            Marker(markers, "Hider Spawn 1", new Vector3(-7f, 0f, -5f), 90f, MapMarkerKind.HiderSpawn | MapMarkerKind.LobbySpawn, modes);
            Marker(markers, "Hider Spawn 2", new Vector3(-7f, 0f, 5f), 90f, MapMarkerKind.HiderSpawn | MapMarkerKind.LobbySpawn, modes);
            Marker(markers, "Hider Spawn 3", new Vector3(-3f, 0f, -5f), 90f, MapMarkerKind.HiderSpawn | MapMarkerKind.LobbySpawn, modes);
            Marker(markers, "Hider Spawn 4", new Vector3(-3f, 0f, 5f), 90f, MapMarkerKind.HiderSpawn | MapMarkerKind.LobbySpawn, modes);

            var bounds = new GameObject("Map Bounds").AddComponent<MapBoundsVolume>();
            bounds.transform.SetParent(map.transform, false);
            bounds.LocalBox = new Bounds(new Vector3(0f, 2.5f, 0f), new Vector3(26f, 9f, 22f));
            bounds.UseMapLayers();

            if (!EditorSceneManager.SaveScene(scene, options.ScenePath))
            {
                error = KitEditorLanguage.T("Sahne kaydedilemedi: ", "Could not save the scene: ") + options.ScenePath;
                return false;
            }

            // After the scene has a path: lighting settings belong to a saved scene.
            var settings = new LightingSettings
            {
                name = options.SceneName + " Lighting",
                bakedGI = true,
                realtimeGI = false,
                mixedBakeMode = MixedLightingMode.Shadowmask,
                directionalityMode = LightmapsMode.CombinedDirectional,
                lightmapMaxSize = 2048,
                lightmapResolution = 20f,
                lightmapper = LightingSettings.Lightmapper.ProgressiveGPU,
            };
            AssetDatabase.CreateAsset(settings, $"{mapFolder}/{options.SceneName} Lighting.lighting");
            Lightmapping.lightingSettings = settings;

            // Quick, and without it the scene would not validate: the flood fill runs from the markers.
            MapBoundsBaker.BakeAll(new List<MapBoundsVolume> { bounds });

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return true;
        }

        /// <summary>A closed room split in two by a wall with a doorway, a roof, a few props, two lamps. Closed
        /// on purpose: the bounds bake finds no way out, so the new map validates without a warning.</summary>
        static void ExampleRoom(Transform geometry, Transform lighting, Dictionary<string, Material> m)
        {
            const float height = 4f, thick = 0.3f;
            Block(geometry, "Wall North", new Vector3(0f, height / 2f, 8f), new Vector3(20f, height, thick), m["Wall"]);
            Block(geometry, "Wall South", new Vector3(0f, height / 2f, -8f), new Vector3(20f, height, thick), m["Wall"]);
            Block(geometry, "Wall East", new Vector3(10f, height / 2f, 0f), new Vector3(thick, height, 16f), m["Wall"]);
            Block(geometry, "Wall West", new Vector3(-10f, height / 2f, 0f), new Vector3(thick, height, 16f), m["Wall"]);
            Block(geometry, "Roof", new Vector3(0f, height + 0.1f, 0f), new Vector3(20f, 0.2f, 16f), m["Wall"]);

            // The middle wall, with a doorway 2 m wide and 2.5 m high.
            Block(geometry, "Middle Wall A", new Vector3(0f, height / 2f, -4.5f), new Vector3(thick, height, 7f), m["Wall"]);
            Block(geometry, "Middle Wall B", new Vector3(0f, height / 2f, 4.5f), new Vector3(thick, height, 7f), m["Wall"]);
            Block(geometry, "Doorway Lintel", new Vector3(0f, 3.25f, 0f), new Vector3(thick, 1.5f, 2f), m["Wall"]);

            // Props either against a wall or well clear of one, and solid to the floor. A gap narrower than a
            // player (a crate 1.15 m out from a corner, the space under a table) is a pocket the bounds bake
            // reports as a possible leak: 16 of them in the kit's self test, before this.
            Block(geometry, "Crate 1", new Vector3(-9.35f, 0.5f, -7.35f), Vector3.one, m["Prop"]);
            Block(geometry, "Crate 2", new Vector3(-9.45f, 0.4f, -6.45f), new Vector3(0.8f, 0.8f, 0.8f), m["Prop"]);
            Block(geometry, "Crate 3", new Vector3(-9.4f, 1.35f, -7.4f), new Vector3(0.7f, 0.7f, 0.7f), m["Prop"]);
            Block(geometry, "Shelf", new Vector3(-5f, 0.9f, 7.5f), new Vector3(3f, 1.8f, 0.6f), m["Prop"]);
            Block(geometry, "Counter", new Vector3(4.5f, 0.45f, 0f), new Vector3(2f, 0.9f, 1f), m["Prop"]);
            Block(geometry, "Bench", new Vector3(8.8f, 0.25f, 0f), new Vector3(0.6f, 0.5f, 3f), m["Accent"]);

            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "Pillar";
            pillar.transform.SetParent(geometry, false);
            pillar.transform.localPosition = new Vector3(5f, height / 2f, -4.5f);
            pillar.transform.localScale = new Vector3(0.6f, height / 2f, 0.6f);
            pillar.GetComponent<MeshRenderer>().sharedMaterial = m["Accent"];
            MakeStatic(pillar);

            Lamp(lighting, "Lamp West", LightType.Point, new Vector3(-5f, 3.4f, 0f), Vector3.zero, new Color(1f, 0.86f, 0.66f), 3f, 12f, LightShadows.Soft);
            Lamp(lighting, "Lamp East", LightType.Point, new Vector3(5f, 3.4f, 0f), Vector3.zero, new Color(1f, 0.86f, 0.66f), 3f, 12f, LightShadows.Soft);

            var reflections = new GameObject("Reflection Probe").AddComponent<ReflectionProbe>();
            reflections.transform.SetParent(lighting, false);
            reflections.transform.localPosition = new Vector3(0f, 2f, 0f);
            reflections.mode = ReflectionProbeMode.Baked;
            reflections.size = new Vector3(20f, height + 0.4f, 16f);
        }

        static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static GameObject Block(Transform parent, string name, Vector3 center, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            MakeStatic(go);
            return go;
        }

        static void MakeStatic(GameObject go) => GameObjectUtility.SetStaticEditorFlags(go,
            StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic
            | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);

        static Light Lamp(Transform parent, string name, LightType type, Vector3 position, Vector3 euler, Color color,
            float intensity, float range, LightShadows shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            var light = go.AddComponent<Light>();
            light.type = type;
            light.color = color;
            light.intensity = intensity;
            if (type != LightType.Directional)
                light.range = range;
            light.lightmapBakeType = LightmapBakeType.Mixed;
            light.shadows = shadows;
            return light;
        }

        static void Marker(Transform parent, string name, Vector3 position, float yaw, MapMarkerKind kinds, MapMarkerModes modes)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var marker = new SerializedObject(go.AddComponent<MapMarker>());
            marker.FindProperty("kinds").intValue = (int)kinds;
            marker.FindProperty("modes").intValue = (int)modes;
            marker.ApplyModifiedPropertiesWithoutUndo();
        }

        static Material MaterialAsset(string mapFolder, Shader shader, string name, Color color)
        {
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, $"{mapFolder}/Materials/{name}.mat");
            return material;
        }
    }
}

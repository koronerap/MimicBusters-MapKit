using System;
using System.IO;
using System.Linq;
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
    /// Map Kit > Examples > Logic Playground: a greybox map where every map logic component is at work, to
    /// open, walk round, pack and take apart. Made by code rather than shipped as a scene, like New Map:
    /// it always matches the kit it comes with, and the kit carries no baked lighting or sound files.
    ///
    /// A courtyard of nine rooms, 36 m across, open to the sky. The middle one is where everybody starts;
    /// each of the others shows one idea:
    ///
    ///   00 Hub           round events: a chime, a siren and a smoke beacon at the hunt, fireworks at the end;
    ///                    a generator humming on a loop from the start.
    ///   01 Doors         a sliding door that closes by itself (Return After), with a sensor that opens it
    ///                    for Hunters (a trigger pressing a button); a swinging door a button turns back.
    ///   02 Elevator      three floors: call buttons and buttons riding on the car (Go To Stop), a motor
    ///                    and a chime at every floor.
    ///   03 Platforms     a pit that kills; a ferry going to and fro over it, stopped and started by
    ///                    buttons (Pause, Resume); a turning carousel switched on and off.
    ///   04 Hazards       a crusher; fire vents on a random timer, lit and put out through a relay; a
    ///                    steam pipe that scalds; a power switch for all of it.
    ///   05 Vault         three runes counted to open a locked door (Counter, Lock, Unlock); a decoy rune
    ///                    that takes one back, a Hunters' master key that sets the count, a reset.
    ///   06 Teleports     two pads sending players to each other; a room with no door and a button to
    ///                    leave it (Teleport Activator); a Modelers' shortcut across the map.
    ///   07 Triggers      a pressure plate that shows a bridge while someone stands on it, with a railing
    ///                    that is a toggle inside a toggle; an alarm for Hunters; a hidden nook.
    ///   08 Sounds & FX   every effect on a pedestal; a jukebox of two tunes, each starting the other when
    ///                    it ends (Finished); an announcement for the whole map; a button that hurts only
    ///                    whoever presses it (Hurt Activator).
    ///
    /// The sky is closed by an invisible ceiling, a collider with nothing drawn: the map bounds bake needs
    /// the play area shut, and a player on top of the outer walls would otherwise walk off the world.
    /// </summary>
    public static class KitExampleMaps
    {
        public const string PlaygroundMapName = "Logic Playground";
        public const string PlaygroundSceneName = "LogicPlayground";
        public const string DefaultFolder = "Assets/Maps";

        static string T(string tr, string en) => KitEditorLanguage.T(tr, en);
        static string F(string tr, string en, params object[] args) => KitEditorLanguage.F(tr, en, args);

        [MenuItem("Map Kit/Examples/Logic Playground", priority = 2)]
        static void CreateFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            string scenePath = KitNewMap.ScenePathFor(DefaultFolder, PlaygroundSceneName);
            if (Directory.Exists(Path.GetDirectoryName(scenePath)))
            {
                int choice = EditorUtility.DisplayDialogComplex("Map Kit",
                    F("Örnek harita zaten var: {0}\n\nAçılsın mı, yoksa baştan mı yapılsın? Baştan yapmak o klasördeki her şeyi (senin değişikliklerin dahil) siler.",
                        "The example map is already there: {0}\n\nOpen it, or make it again? Making it again deletes everything in its folder, your changes included.",
                        scenePath),
                    T("Aç", "Open"), T("Vazgeç", "Cancel"), T("Baştan yap", "Make it again"));
                if (choice == 1)
                    return;
                if (choice == 0)
                {
                    if (File.Exists(scenePath))
                        EditorSceneManager.OpenScene(scenePath);
                    return;
                }
            }

            if (!CreateLogicPlayground(DefaultFolder, out scenePath, out string error))
            {
                EditorUtility.DisplayDialog("Map Kit", error, T("Tamam", "OK"));
                return;
            }

            KitMapValidatorWindow.Open();
            if (EditorUtility.DisplayDialog("Map Kit",
                    T("Örnek harita hazır. Işık şimdi bake edilsin mi? Arka planda bir iki dakika sürer; bitince Map Kit > Pack and Test ile oyunda dene.\n\nHer istasyonun nasıl bağlandığını Map Kit > Logic penceresinde ya da objeleri seçip Inspector'da görebilirsin.",
                        "The example map is ready. Bake its lighting now? It takes a minute or two in the background; then try it in the game with Map Kit > Pack and Test.\n\nSee how each station is wired in the Map Kit > Logic window, or by selecting its objects."),
                    T("Şimdi bake et", "Bake now"), T("Sonra", "Later")))
                Lightmapping.BakeAsync();
        }

        /// <summary>Makes the example in folder/LogicPlayground, replacing whatever is there, and leaves it
        /// open. The lighting is not baked: that is the caller's to start.</summary>
        public static bool CreateLogicPlayground(string folder, out string scenePath, out string error)
        {
            error = null;
            scenePath = KitNewMap.ScenePathFor(folder, PlaygroundSceneName);
            string mapFolder = Path.GetDirectoryName(scenePath).Replace('\\', '/');

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                error = T("URP Lit shader'ı bulunamadı. Proje Universal Render Pipeline kullanmıyor gibi.",
                    "The URP Lit shader was not found. The project does not seem to use the Universal Render Pipeline.");
                return false;
            }

            // A new, empty scene first: the old one may be open, and its folder is about to go.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (AssetDatabase.IsValidFolder(mapFolder))
                AssetDatabase.DeleteAsset(mapFolder);
            Directory.CreateDirectory(mapFolder);
            AssetDatabase.Refresh();

            try
            {
                new Playground(mapFolder, lit).Build(scenePath);
            }
            catch (Exception e)
            {
                error = T("Örnek harita yapılamadı: ", "The example map could not be made: ") + e.Message;
                Debug.LogException(e);
                return false;
            }
            return true;
        }

        // ================================================================== the playground

        sealed class Playground
        {
            readonly string folder;
            readonly KitGreybox box;
            readonly Material floor, wall, dark, prop, button, mover, hazard, teleport, toggle, reward;

            // The example's sounds, made once and shared between the entities that play them.
            AudioClip motorHum, ding, buzz, chime, fanfare, siren, whoosh, pop, generatorHum, musicA, musicB,
                alarm, splash, clunk, click, slam, roar, sizzle, dingDong;

            Transform stations, markers;
            Transform ledgeArrival, secretArrival;
            MapSound ledgeWhoosh;

            public Playground(string mapFolder, Shader lit)
            {
                folder = mapFolder;
                box = new KitGreybox(mapFolder, lit);
                floor = box.Material(KitGreybox.Floor, "Greybox Floor");
                wall = box.Material(KitGreybox.Wall, "Greybox Wall");
                dark = box.Material(KitGreybox.Dark, "Greybox Dark");
                prop = box.Material(KitGreybox.Prop, "Greybox Prop");
                button = box.Material(KitGreybox.Button, "Greybox Button");
                mover = box.Material(KitGreybox.Mover, "Greybox Mover");
                hazard = box.Material(KitGreybox.Hazard, "Greybox Hazard");
                teleport = box.Material(KitGreybox.Teleport, "Greybox Teleport");
                toggle = box.Material(KitGreybox.Toggle, "Greybox Toggle");
                reward = box.Material(KitGreybox.Reward, "Greybox Reward");
            }

            public void Build(string scenePath)
            {
                Scene scene = SceneManager.GetActiveScene();
                MakeSounds();

                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fog = false;
                RenderSettings.fogDensity = 0.01f;

                var map = new GameObject("Map");
                var descriptor = map.AddComponent<KitMapDescriptor>();
                Edit(descriptor, s =>
                {
                    s.FindProperty("mapName").stringValue = PlaygroundMapName;
                    s.FindProperty("author").stringValue = "Mimic Busters";
                    s.FindProperty("description").stringValue =
                        "Every map logic component at work in a greybox courtyard: doors, an elevator, platforms over a pit, "
                        + "hazards, a vault puzzle, teleports, triggers and toggles, sounds and effects. Open it in the Map Kit "
                        + "to see how each station is wired.";
                    s.FindProperty("modes").intValue = (int)(MapMarkerModes.Standard | MapMarkerModes.Practice
                        | MapMarkerModes.Deathmatch | MapMarkerModes.Gartic);
                });

                Transform lighting = Group(map.transform, "Lighting");
                Light sun = KitNewMap.Lamp(lighting, "Sun", LightType.Directional, Vector3.zero, new Vector3(50f, -35f, 0f),
                    new Color(1f, 0.96f, 0.88f), 1.3f, 0f, LightShadows.Soft);
                RenderSettings.sun = sun;

                var probes = new GameObject("Probe Volume").AddComponent<ProbeVolume>();
                probes.transform.SetParent(lighting, false);
                probes.mode = ProbeVolume.Mode.Scene;

                var reflections = new GameObject("Reflection Probe").AddComponent<ReflectionProbe>();
                reflections.transform.SetParent(lighting, false);
                reflections.transform.localPosition = new Vector3(0f, 2f, 0f);
                reflections.mode = ReflectionProbeMode.Baked;
                reflections.center = new Vector3(0f, 2f, 0f);
                reflections.size = new Vector3(36f, 8f, 36f);

                BakedLamp(lighting, "Vault Lamp", new Vector3(15f, 3.3f, 15f), new Color(1f, 0.85f, 0.6f), 2.5f, 7f);
                BakedLamp(lighting, "Secret Room Lamp", new Vector3(-16.2f, 2.5f, 16f), new Color(0.75f, 0.85f, 1f), 2f, 6f);

                Shell(Group(map.transform, "Geometry"));

                stations = Group(map.transform, "Stations");
                markers = Group(map.transform, "Markers");
                Hub();
                Doors();
                Elevator();
                Platforms();
                Hazards();
                Vault();
                Triggers();     // before Teleports: the Modelers' shortcut lands on this room's ledge
                Teleports();
                SoundsAndEffects();
                DeathmatchSpawns();

                var bounds = new GameObject("Map Bounds").AddComponent<MapBoundsVolume>();
                bounds.transform.SetParent(map.transform, false);
                bounds.LocalBox = new Bounds(new Vector3(0f, 2.25f, 0f), new Vector3(38f, 13f, 38f));
                bounds.UseMapLayers();

                // The secret room has no door: a flood fill from the spawn points would never find it, and
                // the game would pull anyone teleported in there back out as lost. So the volume is given
                // seeds, one in the courtyard and one in the room: with Seeds filled in, the fill starts from
                // those alone, and a seed for the room only would leave the rest of the map out.
                Transform courtyardSeed = Empty(bounds.transform, "Seed (Courtyard)", new Vector3(0f, 1f, -3.5f)).transform;
                Transform roomSeed = Empty(bounds.transform, "Seed (Secret Room)", secretArrival.position + Vector3.up).transform;
                Edit(bounds, s =>
                {
                    SerializedProperty seeds = s.FindProperty("seeds");
                    seeds.arraySize = 2;
                    seeds.GetArrayElementAtIndex(0).objectReferenceValue = courtyardSeed;
                    seeds.GetArrayElementAtIndex(1).objectReferenceValue = roomSeed;
                });

                box.SaveMeshes();
                if (!EditorSceneManager.SaveScene(scene, scenePath))
                    throw new IOException(T("Sahne kaydedilemedi: ", "Could not save the scene: ") + scenePath);

                var settings = new LightingSettings
                {
                    name = PlaygroundSceneName + " Lighting",
                    bakedGI = true,
                    realtimeGI = false,
                    mixedBakeMode = MixedLightingMode.Shadowmask,
                    directionalityMode = LightmapsMode.CombinedDirectional,
                    lightmapMaxSize = 2048,
                    lightmapResolution = 12f,
                    lightmapper = LightingSettings.Lightmapper.ProgressiveGPU,
                };
                AssetDatabase.CreateAsset(settings, $"{folder}/{PlaygroundSceneName} Lighting.lighting");
                Lightmapping.lightingSettings = settings;

                // The card picture: the whole courtyard from above the south-west corner.
                KitMapPreviewSettings preview = KitPreview.SettingsOrCreate(scene);
                if (preview != null)
                {
                    var from = new Vector3(-17.2f, 7.5f, -17.2f);
                    preview.hasPose = true;
                    preview.position = from;
                    preview.rotation = Quaternion.LookRotation(new Vector3(2f, 0f, 2f) - from);
                    preview.fieldOfView = 70f;
                    EditorUtility.SetDirty(preview);
                }

                MapBoundsBaker.BakeAll(new System.Collections.Generic.List<MapBoundsVolume> { bounds });

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
            }

            // ------------------------------------------------------------------ the courtyard

            /// <summary>Floor, outer walls, the walls between the rooms, and the invisible ceiling. Rooms are
            /// 12 m squares on a 3 by 3 grid; every wall between two of them has a 3 m doorway in its
            /// middle. The floor leaves a hole for the pit in the east room.
            ///
            /// Everywhere in this map, the space between two things is either a hairline or at least a
            /// metre, across and up: the bounds bake measures the air in half-metre cells, and air only one
            /// cell across (a slot over a wall, a gap beside a platform) is a crack no player could be in,
            /// which it reports as a possible leak.</summary>
            void Shell(Transform geometry)
            {
                const KitGreybox.Faces upAndSides = KitGreybox.Faces.All & ~KitGreybox.Faces.Bottom;
                Transform shell = Group(geometry, "Shell");
                Box(shell, "Floor West", -18f, -0.5f, -18f, 8f, 0f, 18f, floor, faces: upAndSides);
                Box(shell, "Floor East", 16f, -0.5f, -18f, 18f, 0f, 18f, floor, faces: upAndSides);
                Box(shell, "Floor South of the Pit", 8f, -0.5f, -18f, 16f, 0f, -4f, floor, faces: upAndSides);
                Box(shell, "Floor North of the Pit", 8f, -0.5f, 4f, 16f, 0f, 18f, floor, faces: upAndSides);

                Box(shell, "Outer Wall North", -18.5f, -0.5f, 18f, 18.5f, 8f, 18.5f, wall, faces: KitGreybox.Faces.Back);
                Box(shell, "Outer Wall South", -18.5f, -0.5f, -18.5f, 18.5f, 8f, -18f, wall, faces: KitGreybox.Faces.Front);
                Box(shell, "Outer Wall East", 18f, -0.5f, -18f, 18.5f, 8f, 18f, wall, faces: KitGreybox.Faces.Left);
                Box(shell, "Outer Wall West", -18.5f, -0.5f, -18f, -18f, 8f, 18f, wall, faces: KitGreybox.Faces.Right);
                Box(shell, "Sky Clip (invisible ceiling)", -18.5f, 8f, -18.5f, 18.5f, 8.5f, 18.5f, null, faces: KitGreybox.Faces.None);

                Transform walls = Group(geometry, "Room Walls");
                float[] alongZ = { -18f, -13.5f, -10.5f, -1.5f, 1.5f, 10.5f, 13.5f, 18f };
                foreach (float x in new[] { -6f, 6f })
                {
                    for (int i = 0; i < alongZ.Length; i += 2)
                        Box(walls, $"Wall x{x:+0;-0} ({alongZ[i]:0.#} to {alongZ[i + 1]:0.#})", x - 0.2f, 0f, alongZ[i], x + 0.2f, 4f, alongZ[i + 1], wall);
                }

                float[] alongX = { -18f, -13.5f, -10.5f, -6.2f, -5.8f, -1.5f, 1.5f, 5.8f, 6.2f, 10.5f, 13.5f, 18f };
                foreach (float z in new[] { -6f, 6f })
                {
                    for (int i = 0; i < alongX.Length; i += 2)
                        Box(walls, $"Wall z{z:+0;-0} ({alongX[i]:0.#} to {alongX[i + 1]:0.#})", alongX[i], 0f, z - 0.2f, alongX[i + 1], 4f, z + 0.2f, wall);
                }
            }

            // ------------------------------------------------------------------ 00 Hub

            void Hub()
            {
                Transform hub = Group(stations, "00 Hub");
                Box(hub, "Stage", -1.5f, 0f, -1.5f, 1.5f, 0.3f, 1.5f, dark);
                Box(hub, "Generator", -5.8f, 0f, 4.3f, -4.3f, 1.5f, 5.8f, dark);

                Sound(hub, "Generator Hum", new Vector3(-5.05f, 1.6f, 5.05f), generatorHum, volume: 0.5f, loop: true, playOnStart: true,
                    minDistance: 1.5f, maxDistance: 9f);

                var round = Entity<MapRoundEvents>(Empty(hub, "Round Events", new Vector3(0f, 0.5f, 5.2f)));
                MapSound roundChime = Sound(hub, "Round Chime", new Vector3(0f, 2f, 0f), fanfare, volume: 0.6f, spatial: false);
                MapSound huntSiren = Sound(hub, "Hunt Siren", new Vector3(0f, 3f, 0f), siren, volume: 0.55f, spatial: false);
                MapEffect beacon = Effect(hub, "Hunt Beacon", new Vector3(0f, 0.3f, 0f), MapEffectPreset.Smoke, false,
                    size: 2.5f, amount: 2f, tint: new Color(1f, 0.35f, 0.3f));

                Wire(round, MapEvent.RoundStarted, roundChime, MapInput.Play);
                Wire(round, MapEvent.HuntStarted, huntSiren, MapInput.Play);
                Wire(round, MapEvent.HuntStarted, beacon, MapInput.Enable);
                Wire(round, MapEvent.HuntStarted, beacon, MapInput.Disable, delay: 10f);

                var fireworks = Entity<MapRelay>(Empty(hub, "Fireworks", new Vector3(0f, 0.5f, -5.2f)));
                MapSound bang = Sound(hub, "Firework Pop", new Vector3(0f, 6.5f, 0f), pop, volume: 0.8f, maxDistance: 45f);
                Color[] colours = { new Color(1f, 0.4f, 0.3f), new Color(0.4f, 0.8f, 1f), new Color(1f, 0.9f, 0.4f), new Color(0.6f, 1f, 0.5f) };
                Vector3[] where = { new Vector3(-4.2f, 6.5f, -4.2f), new Vector3(4.2f, 6.5f, 4.2f), new Vector3(4.2f, 6.5f, -4.2f), new Vector3(-4.2f, 6.5f, 4.2f) };
                for (int i = 0; i < where.Length; i++)
                {
                    MapEffect rocket = Effect(hub, $"Firework {i + 1}", where[i], MapEffectPreset.Sparks, false, size: 3f, amount: 3f, tint: colours[i]);
                    Wire(fireworks, MapEvent.Triggered, rocket, MapInput.Play, delay: 0.45f * i);
                    Wire(fireworks, MapEvent.Triggered, bang, MapInput.Play, delay: 0.45f * i);
                }
                Wire(round, MapEvent.RoundEnded, fireworks, MapInput.Trigger);

                // Everybody starts here: round the stage, facing it. Gartic's modeler stands on the stage.
                const MapMarkerModes everyMode = MapMarkerModes.Standard | MapMarkerModes.Practice | MapMarkerModes.Deathmatch | MapMarkerModes.Gartic;
                for (int i = 0; i < 6; i++)
                {
                    float angle = i * 60f;
                    Vector3 at = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0f, 3.8f);
                    KitNewMap.Marker(markers, $"Hider Spawn {i + 1}", at, angle + 180f, MapMarkerKind.HiderSpawn | MapMarkerKind.LobbySpawn, everyMode);
                }
                KitNewMap.Marker(markers, "Modeler Spawn", new Vector3(0f, 0.3f, 0f), 180f, MapMarkerKind.ModelerSpawn, MapMarkerModes.Gartic);
            }

            // ------------------------------------------------------------------ 01 Doors (south)

            void Doors()
            {
                Transform room = Group(stations, "01 Doors");

                // Between this room and the hub: slides east behind the wall, and back four seconds later.
                GameObject panel = Box(room, "Sliding Door", -1.6f, 0f, -6.5f, 1.6f, 3.8f, -6.25f, mover, isStatic: false);
                MapMover sliding = Mover(panel, new[] { Stop(new Vector3(3.2f, 0f, 0f)) }, speed: 2.5f, returnAfter: 4f);

                MapButton hubSide = WallButton(room, "Door Button (Hub Side)", new Vector3(-2.4f, 1.2f, -5.8f), Vector3.forward, MapButtonPrompt.Open);
                MapButton roomSide = WallButton(room, "Door Button (Room Side)", new Vector3(-2.4f, 1.2f, -6.2f), Vector3.back, MapButtonPrompt.Open);
                Wire(hubSide, MapEvent.Pressed, sliding, MapInput.Forward);
                Wire(roomSide, MapEvent.Pressed, sliding, MapInput.Forward);

                // A Hunter walking up to the door opens it: the trigger presses the button for them.
                MapTrigger sensor = Trigger(room, "Door Sensor (Hunters)", new Vector3(0f, 0f, -7.6f), new Vector3(0f, 1f, 0f),
                    new Vector3(3f, 2f, 2f), MapWho.Hunters);
                Wire(sensor, MapEvent.Entered, roomSide, MapInput.Press);

                MapSound whir = Sound(room, "Door Motor", new Vector3(0f, 2.5f, -6.7f), motorHum, volume: 0.5f, maxDistance: 15f);
                MapSound shut = Sound(room, "Door Clunk", new Vector3(0f, 1f, -6.7f), clunk, maxDistance: 15f);
                MapSound refuse = Sound(room, "Door Buzz", new Vector3(0f, 2f, -6.7f), buzz, maxDistance: 15f);
                Wire(sliding, MapEvent.Started, whir, MapInput.Play);
                Wire(sliding, MapEvent.ArrivedAtStart, shut, MapInput.Play);
                Wire(sliding, MapEvent.Blocked, refuse, MapInput.Play);

                // To the east room: a door on a hinge, turned open or shut by either button (Reverse).
                GameObject hinge = Empty(room, "Swing Door", new Vector3(5.8f, 0f, -13.5f));
                Box(hinge.transform, "Panel", 5.5f, 0f, -13.45f, 5.8f, 3.8f, -10.55f, mover, isStatic: false);
                MapMover swing = Mover(hinge, new[] { Stop(Vector3.zero, new Vector3(0f, -90f, 0f)) }, angularSpeed: 90f);
                MapButton swingInside = WallButton(room, "Swing Door Button (Room Side)", new Vector3(5.8f, 1.2f, -9.8f), Vector3.left, MapButtonPrompt.Use);
                MapButton swingOutside = WallButton(room, "Swing Door Button (East Side)", new Vector3(6.2f, 1.2f, -9.8f), Vector3.right, MapButtonPrompt.Use);
                Wire(swingInside, MapEvent.Pressed, swing, MapInput.Reverse);
                Wire(swingOutside, MapEvent.Pressed, swing, MapInput.Reverse);

                Box(room, "Cover 1", -3.5f, 0f, -12f, -2f, 1.2f, -11f, prop);
                Box(room, "Cover 2", 2f, 0f, -9.5f, 3.5f, 1.2f, -8.5f, prop);

                for (int i = 0; i < 4; i++)
                    KitNewMap.Marker(markers, $"Hunter Spawn {i + 1}", new Vector3(-3f + 2f * i, 0f, -15.5f), 0f, MapMarkerKind.HunterSpawn,
                        MapMarkerModes.Standard | MapMarkerModes.Deathmatch);
            }

            // ------------------------------------------------------------------ 02 Elevator (north)

            void Elevator()
            {
                Transform room = Group(stations, "02 Elevator");
                Box(room, "Balcony 1", -5.8f, 2.45f, 14f, 5.8f, 2.75f, 18f, floor);
                Box(room, "Balcony 2", -5.8f, 5.5f, 14f, 5.8f, 5.8f, 18f, floor);

                GameObject car = Box(room, "Elevator Car", -1.25f, 0f, 11.5f, 1.25f, 0.25f, 14f, mover, isStatic: false);
                MapMover lift = Mover(car, new[] { Stop(new Vector3(0f, 2.5f, 0f)), Stop(new Vector3(0f, 5.55f, 0f)) }, speed: 1.5f);

                // Buttons riding on the car: children of it, so they move too (and so they are not Static).
                Box(car.transform, "Console", -1.2f, 0.25f, 12.6f, -0.9f, 1.35f, 12.9f, dark, isStatic: false);
                string[] floors = { "Ground", "Floor 1", "Floor 2" };
                for (int f = 0; f < 3; f++)
                {
                    float y = 0.75f + 0.2f * f;
                    MapButton onCar = Button(car.transform, $"Car Button ({floors[f]})", new Vector3(-0.9f, y, 12.65f), new Vector3(-0.82f, y + 0.15f, 12.85f),
                        MapButtonPrompt.Custom, floors[f], isStatic: false);
                    Wire(onCar, MapEvent.Pressed, lift, MapInput.GoToStop, parameter: f);
                }

                // Posts under a metre: the air over one must stay tall enough to be space a player could be
                // in, or the bounds bake takes it for a crack (see Shell).
                float[] levels = { 0f, 2.75f, 5.8f };
                float[] postZ = { 12.4f, 14.3f, 14.3f };
                for (int f = 0; f < 3; f++)
                {
                    Box(room, $"Call Post ({floors[f]})", 2.6f, levels[f], postZ[f], 2.9f, levels[f] + 0.95f, postZ[f] + 0.3f, dark);
                    MapButton call = Button(room, $"Call Button ({floors[f]})", new Vector3(2.52f, levels[f] + 0.7f, postZ[f] + 0.075f),
                        new Vector3(2.6f, levels[f] + 0.85f, postZ[f] + 0.225f), MapButtonPrompt.Call);
                    Wire(call, MapEvent.Pressed, lift, MapInput.GoToStop, parameter: f);
                }

                MapSound motor = Sound(car.transform, "Elevator Motor", new Vector3(0f, 0.6f, 12.75f), motorHum, volume: 0.6f, maxDistance: 18f);
                MapSound bell = Sound(car.transform, "Elevator Ding", new Vector3(0f, 1.8f, 12.75f), ding, volume: 0.7f, maxDistance: 20f);
                Wire(lift, MapEvent.Started, motor, MapInput.Play);
                Wire(lift, MapEvent.ArrivedAtStop, bell, MapInput.Play);
            }

            // ------------------------------------------------------------------ 03 Platforms (east)

            void Platforms()
            {
                Transform room = Group(stations, "03 Platforms");

                // The pit: walls under the floor's edge, a red floor 3.5 m down, and a zone that kills.
                Box(room, "Pit Wall West", 7.5f, -3.5f, -4.5f, 8f, -0.5f, 4.5f, dark, faces: KitGreybox.Faces.Right);
                Box(room, "Pit Wall East", 16f, -3.5f, -4.5f, 16.5f, -0.5f, 4.5f, dark, faces: KitGreybox.Faces.Left);
                Box(room, "Pit Wall South", 8f, -3.5f, -4.5f, 16f, -0.5f, -4f, dark, faces: KitGreybox.Faces.Front);
                Box(room, "Pit Wall North", 8f, -3.5f, 4f, 16f, -0.5f, 4.5f, dark, faces: KitGreybox.Faces.Back);
                Box(room, "Pit Floor", 8f, -4f, -4f, 16f, -3.5f, 4f, hazard, faces: KitGreybox.Faces.Top);
                Box(room, "Stepping Stone", 8f, -3.5f, 0.75f, 9.5f, 0f, 2.25f, dark,
                    faces: KitGreybox.Faces.Top | KitGreybox.Faces.Right | KitGreybox.Faces.Front | KitGreybox.Faces.Back);

                MapHurtZone kill = HurtZone(room, "Pit Kill Zone", new Vector3(12f, -3.5f, 0f), new Vector3(0f, 1.25f, 0f), new Vector3(8f, 2.5f, 8f),
                    damage: 100, interval: 0.5f, instantKill: true);
                MapSound fall = Sound(room, "Pit Splash", new Vector3(12f, -2f, 0f), splash, maxDistance: 25f);
                Wire(kill, MapEvent.Killed, fall, MapInput.Play);

                // A ferry across the pit and back for as long as the round lasts, and a carousel turning in
                // its middle, both started at the round's start.
                GameObject ferryDeck = Box(room, "Ferry", 8f, -0.3f, -4f, 10.5f, 0f, -1f, mover, isStatic: false);
                MapMover ferry = Mover(ferryDeck, new[] { Stop(new Vector3(5.5f, 0f, 0f), wait: 1.5f) }, speed: 1.5f,
                    loop: MapMoverLoop.PingPong, waitAtStart: 1.5f, autoStart: true, whenBlocked: MapMoverBlocked.Ignore);

                GameObject carouselDeck = Box(room, "Carousel", 10.7f, -0.3f, 0.2f, 13.3f, 0f, 2.8f, mover, isStatic: false);
                MapMover carousel = Mover(carouselDeck,
                    new[] { Stop(Vector3.zero, new Vector3(0f, 90f, 0f)), Stop(Vector3.zero, new Vector3(0f, 180f, 0f)), Stop(Vector3.zero, new Vector3(0f, 270f, 0f)) },
                    angularSpeed: 30f, smooth: false, loop: MapMoverLoop.Loop, autoStart: true, whenBlocked: MapMoverBlocked.Ignore);

                MapButton halt = WallButton(room, "Ferry Stop Button", new Vector3(6.2f, 1.2f, -3.6f), Vector3.right, MapButtonPrompt.Custom, "Stop the ferry");
                MapButton go = WallButton(room, "Ferry Go Button", new Vector3(6.2f, 1.2f, -2.8f), Vector3.right, MapButtonPrompt.Custom, "Start the ferry");
                MapButton spin = WallButton(room, "Carousel Switch", new Vector3(6.2f, 1.2f, 3f), Vector3.right, MapButtonPrompt.Custom, "Carousel on/off");
                Wire(halt, MapEvent.Pressed, ferry, MapInput.Pause);
                Wire(go, MapEvent.Pressed, ferry, MapInput.Resume);
                Wire(spin, MapEvent.Pressed, carousel, MapInput.Toggle);
            }

            // ------------------------------------------------------------------ 04 Hazards (west)

            void Hazards()
            {
                Transform room = Group(stations, "04 Hazards");

                // A crusher over the way to the north-west room: waits up top, slams down, hurts whoever
                // is under it (When Blocked: Crush).
                Box(room, "Crusher Warning", -14.5f, 0f, 1f, -11.5f, 0.02f, 4f, hazard);
                GameObject block = Box(room, "Crusher", -14.25f, 3.2f, 1.25f, -11.75f, 4.7f, 3.75f, hazard, isStatic: false);
                MapMover crusher = Mover(block, new[] { Stop(new Vector3(0f, -3.1f, 0f), wait: 0.5f) }, speed: 4f,
                    loop: MapMoverLoop.PingPong, waitAtStart: 2f, autoStart: true, whenBlocked: MapMoverBlocked.Crush, crushDamage: 60);
                MapSound thud = Sound(room, "Crusher Slam", new Vector3(-13f, 0.6f, 2.5f), slam, maxDistance: 25f);
                MapEffect cloud = Effect(room, "Crusher Dust", new Vector3(-13f, 0.1f, 2.5f), MapEffectPreset.Dust, false, size: 1.5f, amount: 2f);
                Wire(crusher, MapEvent.ArrivedAtEnd, thud, MapInput.Play);
                Wire(crusher, MapEvent.ArrivedAtEnd, cloud, MapInput.Play);

                // Three fire vents: a timer at a random interval triggers a relay, which lights them all
                // and puts them out a second and a half later.
                var timer = Entity<MapTimer>(Empty(room, "Vent Timer", new Vector3(-12.5f, 0.5f, -5.2f)), s =>
                {
                    s.FindProperty("interval").floatValue = 4f;
                    s.FindProperty("randomExtra").floatValue = 2f;
                });
                var relay = Entity<MapRelay>(Empty(room, "Vent Relay", new Vector3(-12.5f, 0.5f, -4.8f)));
                Wire(timer, MapEvent.Elapsed, relay, MapInput.Trigger);

                MapSound whoomph = Sound(room, "Vent Roar", new Vector3(-12.5f, 0.8f, -3.25f), roar, maxDistance: 22f);
                MapSound hiss = Sound(room, "Vent Sizzle", new Vector3(-12.5f, 1.2f, -3.25f), sizzle, maxDistance: 15f);
                Wire(relay, MapEvent.Triggered, whoomph, MapInput.Play);
                for (int i = 0; i < 3; i++)
                {
                    float x = -15.5f + 3f * i;
                    Box(room, $"Vent {i + 1} Grate", x - 0.75f, 0f, -4f, x + 0.75f, 0.05f, -2.5f, dark);
                    MapHurtZone heat = HurtZone(room, $"Vent {i + 1} Heat", new Vector3(x, 0.05f, -3.25f), new Vector3(0f, 1f, 0f), new Vector3(1.5f, 2f, 1.5f),
                        damage: 15, interval: 0.5f, startEnabled: false);
                    MapEffect flame = Effect(room, $"Vent {i + 1} Fire", new Vector3(x, 0.05f, -3.25f), MapEffectPreset.Fire, false, size: 1.3f, amount: 1.5f);
                    Wire(relay, MapEvent.Triggered, heat, MapInput.Enable);
                    Wire(relay, MapEvent.Triggered, flame, MapInput.Enable);
                    Wire(relay, MapEvent.Triggered, heat, MapInput.Disable, delay: 1.5f);
                    Wire(relay, MapEvent.Triggered, flame, MapInput.Disable, delay: 1.5f);
                    Wire(heat, MapEvent.Hurt, hiss, MapInput.Play);
                }

                // A leaking pipe in the corner: steam that is always on, and scalds a little.
                Box(room, "Steam Pipe", -18f, 0f, 4.2f, -17.4f, 3.5f, 5.8f, dark);
                Effect(room, "Steam", new Vector3(-17.1f, 0.8f, 5f), MapEffectPreset.Steam, true);
                HurtZone(room, "Steam Burn", new Vector3(-16.7f, 0f, 5f), new Vector3(0f, 1f, 0f), new Vector3(1.2f, 2f, 1.4f), damage: 5, interval: 1f);

                // One switch for the room: the crusher stops where it is and the vents go quiet (Toggle on a
                // mover pauses it; on a timer it stops the clock).
                MapButton power = WallButton(room, "Hazard Power", new Vector3(-6.2f, 1.2f, -3.5f), Vector3.left, MapButtonPrompt.Custom, "Power");
                MapSound snap = Sound(room, "Power Click", new Vector3(-6.4f, 1.2f, -3.5f), click, maxDistance: 10f);
                Wire(power, MapEvent.Pressed, crusher, MapInput.Toggle);
                Wire(power, MapEvent.Pressed, timer, MapInput.Toggle);
                Wire(power, MapEvent.Pressed, snap, MapInput.Play);
            }

            // ------------------------------------------------------------------ 05 Vault (north-east)

            void Vault()
            {
                Transform room = Group(stations, "05 Vault");

                // A roofed room in the corner, its door on the west side.
                Box(room, "Vault Wall West A", 11.8f, 0f, 11.8f, 12.2f, 4f, 14f, wall);
                Box(room, "Vault Wall West B", 11.8f, 0f, 16f, 12.2f, 4f, 18f, wall);
                Box(room, "Vault Lintel", 11.8f, 3f, 14f, 12.2f, 4f, 16f, wall);
                Box(room, "Vault Wall South", 12.2f, 0f, 11.8f, 18f, 4f, 12.2f, wall);
                Box(room, "Vault Roof", 11.8f, 4f, 11.8f, 18f, 4.3f, 18f, wall);
                Box(room, "Treasure Pedestal", 16.8f, 0f, 14.4f, 18f, 1f, 15.6f, dark);
                Box(room, "Treasure", 17f, 1f, 14.6f, 17.8f, 1.6f, 15.4f, reward);
                Box(room, "Crate 1", 6.2f, 0f, 16.5f, 7.7f, 1.5f, 18f, prop);
                Box(room, "Crate 2", 6.2f, 0f, 15.3f, 7.4f, 1.2f, 16.5f, prop);

                GameObject slab = Box(room, "Vault Door", 11.45f, 0f, 13.9f, 11.75f, 3.05f, 16.1f, mover, isStatic: false);
                MapMover door = Mover(slab, new[] { Stop(new Vector3(0f, 0f, -2.2f)) }, speed: 1.2f);

                var count = Entity<MapCounter>(Empty(room, "Vault Counter", new Vector3(9f, 0.5f, 9.5f)), s =>
                {
                    s.FindProperty("startValue").intValue = 0;
                    s.FindProperty("target").intValue = 3;
                });

                MapSound runeChime = Sound(room, "Rune Chime", new Vector3(11f, 1.5f, 9f), chime, maxDistance: 25f);
                MapSound opened = Sound(room, "Vault Fanfare", new Vector3(11.5f, 2f, 15f), fanfare, maxDistance: 30f);
                MapSound locked = Sound(room, "Vault Buzz", new Vector3(11.5f, 1.5f, 17f), buzz, maxDistance: 15f);
                MapSound wrong = Sound(room, "Decoy Buzz", new Vector3(17.5f, 1.4f, 9f), buzz, maxDistance: 15f);
                MapEffect sparkle = Effect(room, "Treasure Sparks", new Vector3(17.4f, 1.7f, 15f), MapEffectPreset.Sparks, false,
                    size: 1.2f, amount: 2f, tint: new Color(1f, 0.85f, 0.4f));

                // Each rune counts once: it switches itself off when pressed, and the reset switches it on.
                MapButton[] runes =
                {
                    WallButton(room, "Rune 1", new Vector3(6.2f, 1.2f, 9f), Vector3.right, MapButtonPrompt.Use),
                    WallButton(room, "Rune 2", new Vector3(16f, 1.2f, 6.2f), Vector3.forward, MapButtonPrompt.Use),
                    WallButton(room, "Rune 3", new Vector3(15f, 1.2f, 11.8f), Vector3.back, MapButtonPrompt.Use),
                };
                foreach (MapButton rune in runes)
                {
                    Wire(rune, MapEvent.Pressed, count, MapInput.Add);
                    Wire(rune, MapEvent.Pressed, rune, MapInput.Disable);
                    Wire(rune, MapEvent.Pressed, runeChime, MapInput.Play);
                }

                MapButton decoy = WallButton(room, "Decoy Rune", new Vector3(18f, 1.4f, 9f), Vector3.left, MapButtonPrompt.Use, cooldown: 2f);
                Wire(decoy, MapEvent.Pressed, count, MapInput.Subtract);
                Wire(decoy, MapEvent.Pressed, wrong, MapInput.Play);

                MapButton key = WallButton(room, "Master Key (Hunters)", new Vector3(8f, 1.2f, 6.2f), Vector3.forward, MapButtonPrompt.Custom, "Master key",
                    who: MapWho.Hunters);
                Wire(key, MapEvent.Pressed, count, MapInput.SetValue, parameter: 3f);

                MapButton open = WallButton(room, "Vault Button", new Vector3(11.8f, 1.2f, 17f), Vector3.left, MapButtonPrompt.Open, locked: true);
                Wire(count, MapEvent.ReachedTarget, open, MapInput.Unlock);
                Wire(count, MapEvent.ReachedTarget, opened, MapInput.Play);
                Wire(open, MapEvent.Pressed, door, MapInput.Forward);
                Wire(open, MapEvent.PressedWhileLocked, locked, MapInput.Play);
                Wire(door, MapEvent.ArrivedAtEnd, sparkle, MapInput.Play);

                MapButton reset = WallButton(room, "Vault Reset", new Vector3(13f, 1.2f, 11.8f), Vector3.back, MapButtonPrompt.Close);
                Wire(reset, MapEvent.Pressed, count, MapInput.ResetCount);
                Wire(reset, MapEvent.Pressed, open, MapInput.Lock);
                Wire(reset, MapEvent.Pressed, door, MapInput.Back);
                foreach (MapButton rune in runes)
                    Wire(reset, MapEvent.Pressed, rune, MapInput.Enable);
            }

            // ------------------------------------------------------------------ 07 Triggers and toggles (south-east)

            void Triggers()
            {
                Transform room = Group(stations, "07 Triggers and Toggles");

                // Stairs up the south wall to a landing, 2 m of air, and a ledge in the corner. They start a
                // metre off the west wall: flush against it, the air over the lowest steps is a wedge the
                // bounds bake takes for a crack.
                for (int i = 0; i < 12; i++)
                    Box(room, $"Step {i + 1}", 7.2f + 0.4f * i, 0f, -18f, 7.6f + 0.4f * i, 0.25f * (i + 1), -16.5f, wall);
                Box(room, "Landing", 12f, 0f, -18f, 13f, 3f, -16.5f, wall);
                Box(room, "Ledge", 15f, 0f, -18f, 18f, 3f, -15f, wall);

                // The bridge is there only while somebody stands on the plate. Its railing is a toggle inside
                // the bridge's: shown only while the bridge is, and switched on its own from the landing.
                GameObject deck = Box(room, "Bridge", 13f, 2.8f, -18f, 15f, 3f, -16.5f, toggle, isStatic: false);
                MapToggle bridge = Toggle(deck, startEnabled: false);
                GameObject rail = Box(deck.transform, "Bridge Railing", 13f, 3f, -16.6f, 15f, 3.9f, -16.5f, toggle, isStatic: false);
                MapToggle railing = Toggle(rail, startEnabled: true);
                MapButton railSwitch = WallButton(room, "Railing Switch", new Vector3(12.5f, 4.4f, -18f), Vector3.forward, MapButtonPrompt.Custom, "Railing");
                Wire(railSwitch, MapEvent.Pressed, railing, MapInput.Toggle);

                Box(room, "Pressure Plate", 8.25f, 0f, -13.75f, 9.75f, 0.05f, -12.25f, button);
                MapTrigger plate = Trigger(room, "Plate Trigger", new Vector3(9f, 0.05f, -13f), new Vector3(0f, 1f, 0f), new Vector3(1.5f, 2f, 1.5f));
                MapSound tick = Sound(room, "Plate Click", new Vector3(9f, 0.5f, -13f), click, maxDistance: 12f);
                Wire(plate, MapEvent.Occupied, bridge, MapInput.Enable);
                Wire(plate, MapEvent.Emptied, bridge, MapInput.Disable);
                Wire(plate, MapEvent.Entered, tick, MapInput.Play);
                Wire(plate, MapEvent.Exited, tick, MapInput.Play);

                // A nook behind a panel that a switch above the ledge hides and shows.
                Box(room, "Nook Side Wall", 15.8f, 0f, -8.2f, 16f, 2.5f, -6.2f, wall);
                GameObject board = Box(room, "Secret Panel", 16f, 0f, -8.2f, 18f, 2.5f, -8f, toggle, isStatic: false);
                MapToggle panel = Toggle(board, startEnabled: true);
                Box(room, "Nook Stash", 17.2f, 0f, -7f, 18f, 0.8f, -6.2f, reward);
                MapButton secret = WallButton(room, "Secret Switch", new Vector3(18f, 4.4f, -16.5f), Vector3.left, MapButtonPrompt.Custom, "Secret");
                MapSound snap = Sound(room, "Secret Click", new Vector3(17.5f, 4.4f, -16.5f), click, maxDistance: 12f);
                Wire(secret, MapEvent.Pressed, panel, MapInput.Toggle);
                Wire(secret, MapEvent.Pressed, snap, MapInput.Play);

                // An alarm for the Modelers hiding here: rings when a Hunter comes through either door, once a
                // round each.
                MapTrigger west = Trigger(room, "Hunter Alarm (West Door)", new Vector3(7.2f, 0f, -12f), new Vector3(0f, 1f, 0f), new Vector3(2f, 2f, 3f), MapWho.Hunters);
                MapTrigger north = Trigger(room, "Hunter Alarm (North Door)", new Vector3(12f, 0f, -7.2f), new Vector3(0f, 1f, 0f), new Vector3(3f, 2f, 2f), MapWho.Hunters);
                MapSound bell = Sound(room, "Alarm", new Vector3(12f, 3.5f, -12f), alarm, maxDistance: 30f);
                Wire(west, MapEvent.Entered, bell, MapInput.Play, once: true);
                Wire(north, MapEvent.Entered, bell, MapInput.Play, once: true);

                ledgeArrival = Empty(room, "Ledge Arrival", new Vector3(16.5f, 3f, -16.5f), 270f).transform;
                ledgeWhoosh = Sound(room, "Ledge Whoosh", new Vector3(16.5f, 4f, -16.5f), whoosh, maxDistance: 15f);
            }

            // ------------------------------------------------------------------ 06 Teleports (north-west)

            void Teleports()
            {
                Transform room = Group(stations, "06 Teleports");

                // A room with no door in the corner: in by the pad, out by the button inside.
                Box(room, "Secret Room Wall East", -14.3f, 0f, 13.7f, -14f, 3f, 18f, wall);
                Box(room, "Secret Room Wall South", -18f, 0f, 13.7f, -14.3f, 3f, 14f, wall);
                Box(room, "Secret Room Roof", -18f, 3f, 13.7f, -14f, 3.3f, 18f, wall);
                Box(room, "Secret Stash", -18f, 0f, 17f, -17f, 1f, 18f, reward);

                Box(room, "Pad A", -16.5f, 0f, 8f, -14.5f, 0.1f, 10f, teleport);
                Box(room, "Pad B", -10f, 0f, 14.5f, -8f, 0.1f, 16.5f, teleport);
                Box(room, "Secret Pad", -12.5f, 0f, 16f, -10.5f, 0.1f, 18f, teleport);
                Box(room, "Modelers' Pad", -9f, 0f, 7.5f, -7f, 0.1f, 9.5f, teleport);

                // Arrivals stand clear of every pad's box, facing away from it: arriving must not send anyone on.
                Transform arrivalA = Empty(room, "Pad A Arrival", new Vector3(-13f, 0f, 9f), 90f).transform;
                Transform arrivalB = Empty(room, "Pad B Arrival", new Vector3(-9f, 0f, 12.5f), 180f).transform;
                secretArrival = Empty(room, "Secret Room Arrival", new Vector3(-16f, 0f, 15.8f), 90f).transform;
                Transform exitArrival = Empty(room, "Secret Exit Arrival", new Vector3(-11.5f, 0f, 11.5f), 180f).transform;

                var padBox = new Vector3(1.8f, 2f, 1.8f);
                MapTeleport toB = Teleport(room, "Teleport A", new Vector3(-15.5f, 0.1f, 9f), padBox, arrivalB);
                MapTeleport toA = Teleport(room, "Teleport B (keeps facing)", new Vector3(-9f, 0.1f, 15.5f), padBox, arrivalA, keepFacing: true);
                MapTeleport inside = Teleport(room, "Secret Teleport", new Vector3(-11.5f, 0.1f, 17f), padBox, secretArrival);
                MapTeleport shortcut = Teleport(room, "Modelers' Shortcut", new Vector3(-8f, 0.1f, 8.5f), padBox, ledgeArrival, who: MapWho.Hiders);

                // Used only through its input: switched off, so its box takes nobody.
                MapTeleport exit = Teleport(room, "Secret Exit", new Vector3(-17.5f, 0f, 14.5f), new Vector3(0.5f, 2f, 0.5f), exitArrival, startEnabled: false);
                MapButton leave = WallButton(room, "Leave Button", new Vector3(-14.3f, 1.2f, 16f), Vector3.left, MapButtonPrompt.Custom, "Leave");
                Wire(leave, MapEvent.Pressed, exit, MapInput.TeleportActivator);

                MapSound atA = Sound(room, "Arrival Whoosh A", new Vector3(-13f, 1f, 9f), whoosh, maxDistance: 15f);
                MapSound atB = Sound(room, "Arrival Whoosh B", new Vector3(-9f, 1f, 12.5f), whoosh, maxDistance: 15f);
                MapSound atSecret = Sound(room, "Secret Whoosh", new Vector3(-16f, 1.5f, 15.8f), whoosh, maxDistance: 10f);
                MapSound atExit = Sound(room, "Exit Whoosh", new Vector3(-11.5f, 1f, 11.5f), whoosh, maxDistance: 15f);
                MapEffect sparksA = Effect(room, "Arrival Sparks A", new Vector3(-13f, 0.2f, 9f), MapEffectPreset.Sparks, false, tint: new Color(0.4f, 0.8f, 1f));
                MapEffect sparksB = Effect(room, "Arrival Sparks B", new Vector3(-9f, 0.2f, 12.5f), MapEffectPreset.Sparks, false, tint: new Color(0.4f, 0.8f, 1f));

                Wire(toB, MapEvent.Teleported, atB, MapInput.Play);
                Wire(toB, MapEvent.Teleported, sparksB, MapInput.Play);
                Wire(toA, MapEvent.Teleported, atA, MapInput.Play);
                Wire(toA, MapEvent.Teleported, sparksA, MapInput.Play);
                Wire(inside, MapEvent.Teleported, atSecret, MapInput.Play);
                Wire(exit, MapEvent.Teleported, atExit, MapInput.Play);
                Wire(shortcut, MapEvent.Teleported, ledgeWhoosh, MapInput.Play);
            }

            // ------------------------------------------------------------------ 08 Sounds and effects (south-west)

            void SoundsAndEffects()
            {
                Transform room = Group(stations, "08 Sounds and Effects");

                // Every effect on a pedestal along the south wall, each with its button. Sparks bursts once
                // a press (Play); the others go on and off (Toggle).
                float[] xs = { -17.6f, -15.4f, -13.2f, -11f, -8.8f, -6.6f };
                MapEffectPreset[] presets = { MapEffectPreset.Smoke, MapEffectPreset.Steam, MapEffectPreset.Fire, MapEffectPreset.Sparks, MapEffectPreset.Dust, MapEffectPreset.Drips };
                for (int i = 0; i < presets.Length; i++)
                {
                    string name = presets[i].ToString();
                    Box(room, $"Pedestal ({name})", xs[i] - 0.4f, 0f, -18f, xs[i] + 0.4f, 1f, -17.2f, dark);
                    MapEffect effect = Effect(room, $"{name} Effect", new Vector3(xs[i], 1f, -17.6f), presets[i], false);
                    MapButton press = WallButton(room, $"{name} Button", new Vector3(xs[i], 0.7f, -17.2f), Vector3.forward, MapButtonPrompt.Custom, name);
                    Wire(press, MapEvent.Pressed, effect, presets[i] == MapEffectPreset.Sparks ? MapInput.Play : MapInput.Toggle);
                }

                // A jukebox of two tunes: when one ends it starts the other (Finished), until Stop.
                Box(room, "Jukebox", -18f, 0f, -12.8f, -17.2f, 1.6f, -11.2f, dark);
                MapSound tuneA = Sound(room, "Music A", new Vector3(-17.4f, 1.2f, -12f), musicA, volume: 0.7f, maxDistance: 16f);
                MapSound tuneB = Sound(room, "Music B", new Vector3(-17.4f, 1.2f, -12f), musicB, volume: 0.7f, maxDistance: 16f);
                MapButton play = WallButton(room, "Jukebox Play", new Vector3(-17.2f, 1.1f, -12.4f), Vector3.right, MapButtonPrompt.Custom, "Play music");
                MapButton stop = WallButton(room, "Jukebox Stop", new Vector3(-17.2f, 1.1f, -11.6f), Vector3.right, MapButtonPrompt.Custom, "Stop music");
                Wire(play, MapEvent.Pressed, tuneB, MapInput.Stop);
                Wire(play, MapEvent.Pressed, tuneA, MapInput.Play);
                Wire(stop, MapEvent.Pressed, tuneA, MapInput.Stop);
                Wire(stop, MapEvent.Pressed, tuneB, MapInput.Stop);
                Wire(tuneA, MapEvent.Finished, tuneB, MapInput.Play);
                Wire(tuneB, MapEvent.Finished, tuneA, MapInput.Play);

                // Heard everywhere at once (not spatial), and only once a round.
                MapButton announce = WallButton(room, "Announcement Button", new Vector3(-15.5f, 1.2f, -6.2f), Vector3.back, MapButtonPrompt.Custom, "Announce",
                    once: true);
                MapSound announcement = Sound(room, "Announcement", new Vector3(-12f, 3f, -12f), dingDong, volume: 0.7f, spatial: false);
                Wire(announce, MapEvent.Pressed, announcement, MapInput.Play);

                // Hurts only the one who presses it: a switched-off hurt zone's Hurt Activator, with its own
                // damage as the output's number.
                MapButton prank = WallButton(room, "Do Not Press", new Vector3(-6.2f, 1.2f, -9f), Vector3.left, MapButtonPrompt.Custom, "Do not press");
                MapHurtZone sting = HurtZone(room, "Prank Zone", new Vector3(-7.5f, 0f, -9f), new Vector3(0f, 1f, 0f), new Vector3(1f, 2f, 1f),
                    damage: 10, interval: 1f, startEnabled: false);
                MapEffect sparks = Effect(room, "Prank Sparks", new Vector3(-6.5f, 1.2f, -9f), MapEffectPreset.Sparks, false, size: 0.8f);
                MapSound raspberry = Sound(room, "Prank Buzz", new Vector3(-6.5f, 1.2f, -9f), buzz, maxDistance: 12f);
                Wire(prank, MapEvent.Pressed, sting, MapInput.HurtActivator, parameter: 25f);
                Wire(prank, MapEvent.Pressed, sparks, MapInput.Play);
                Wire(prank, MapEvent.Pressed, raspberry, MapInput.Play);
            }

            /// <summary>Deathmatch puts a player at the spawn farthest from everybody, so it gets a few more,
            /// spread over the rooms that do not hurt.</summary>
            void DeathmatchSpawns()
            {
                (string name, Vector3 at)[] spots =
                {
                    ("North", new Vector3(0f, 0f, 8.5f)),
                    ("North-West", new Vector3(-12.5f, 0f, 12.5f)),
                    ("North-East", new Vector3(9f, 0f, 9.5f)),
                    ("South-East", new Vector3(12f, 0f, -10.5f)),
                    ("South-West", new Vector3(-12f, 0f, -11f)),
                };
                foreach ((string name, Vector3 at) in spots)
                {
                    float yaw = Quaternion.LookRotation(-new Vector3(at.x, 0f, at.z)).eulerAngles.y;
                    KitNewMap.Marker(markers, $"Deathmatch Spawn ({name})", at, yaw, MapMarkerKind.HiderSpawn, MapMarkerModes.Deathmatch);
                }
            }

            // ------------------------------------------------------------------ sounds

            void MakeSounds()
            {
                string sounds = folder + "/Sounds";
                motorHum = KitSoundSynth.Save(sounds, "Motor", KitSoundSynth.MotorHum());
                ding = KitSoundSynth.Save(sounds, "Ding", KitSoundSynth.Ding());
                buzz = KitSoundSynth.Save(sounds, "Buzz", KitSoundSynth.Buzz());
                chime = KitSoundSynth.Save(sounds, "Chime", KitSoundSynth.Chime());
                fanfare = KitSoundSynth.Save(sounds, "Fanfare", KitSoundSynth.Fanfare());
                siren = KitSoundSynth.Save(sounds, "Siren", KitSoundSynth.Siren());
                whoosh = KitSoundSynth.Save(sounds, "Whoosh", KitSoundSynth.Whoosh());
                pop = KitSoundSynth.Save(sounds, "Firework", KitSoundSynth.Pop());
                generatorHum = KitSoundSynth.Save(sounds, "Generator Loop", KitSoundSynth.GeneratorHum(), loops: true);
                musicA = KitSoundSynth.Save(sounds, "Music A", KitSoundSynth.Music(false));
                musicB = KitSoundSynth.Save(sounds, "Music B", KitSoundSynth.Music(true));
                alarm = KitSoundSynth.Save(sounds, "Alarm", KitSoundSynth.Alarm());
                splash = KitSoundSynth.Save(sounds, "Splash", KitSoundSynth.Splash());
                clunk = KitSoundSynth.Save(sounds, "Clunk", KitSoundSynth.Clunk());
                click = KitSoundSynth.Save(sounds, "Click", KitSoundSynth.Click());
                slam = KitSoundSynth.Save(sounds, "Slam", KitSoundSynth.Slam());
                roar = KitSoundSynth.Save(sounds, "Fire Roar", KitSoundSynth.Roar());
                sizzle = KitSoundSynth.Save(sounds, "Sizzle", KitSoundSynth.Sizzle());
                dingDong = KitSoundSynth.Save(sounds, "Ding Dong", KitSoundSynth.DingDong());
            }

            // ------------------------------------------------------------------ helpers

            static Transform Group(Transform parent, string name) => KitNewMap.Child(parent, name);

            GameObject Box(Transform parent, string name, float x0, float y0, float z0, float x1, float y1, float z1, Material material,
                bool isStatic = true, KitGreybox.Faces faces = KitGreybox.Faces.Auto) =>
                box.Box(parent, name, new Vector3(x0, y0, z0), new Vector3(x1, y1, z1), material, isStatic, faces);

            static GameObject Empty(Transform parent, string name, Vector3 position, float yaw = 0f)
            {
                var go = new GameObject(name);
                go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
                go.transform.SetParent(parent, true);
                return go;
            }

            static void Edit(UnityEngine.Object target, Action<SerializedObject> edit)
            {
                var data = new SerializedObject(target);
                edit(data);
                data.ApplyModifiedPropertiesWithoutUndo();
            }

            static T Entity<T>(GameObject go, Action<SerializedObject> edit = null, bool startEnabled = true) where T : MapEntity
            {
                var entity = go.AddComponent<T>();
                Edit(entity, s =>
                {
                    s.FindProperty("startEnabled").boolValue = startEnabled;
                    edit?.Invoke(s);
                });
                return entity;
            }

            static void Wire(MapEntity from, MapEvent onEvent, MapEntity to, MapInput input, float delay = 0f, float parameter = 0f, bool once = false) =>
                KitNewMap.Output(from, onEvent, to, input, delay, parameter, once);

            static void BakedLamp(Transform parent, string name, Vector3 at, Color color, float intensity, float range)
            {
                Light lamp = KitNewMap.Lamp(parent, name, LightType.Point, at, Vector3.zero, color, intensity, range, LightShadows.Soft);
                lamp.lightmapBakeType = LightmapBakeType.Baked;
            }

            MapButton Button(Transform parent, string name, Vector3 min, Vector3 max, MapButtonPrompt prompt, string custom = "",
                MapWho who = MapWho.Everyone, bool once = false, bool locked = false, float cooldown = 1f, bool isStatic = true)
            {
                GameObject go = box.Box(parent, name, min, max, button, isStatic);
                return Entity<MapButton>(go, s =>
                {
                    s.FindProperty("prompt").intValue = (int)prompt;
                    s.FindProperty("customPrompt").stringValue = custom ?? "";
                    s.FindProperty("who").intValue = (int)who;
                    s.FindProperty("once").boolValue = once;
                    s.FindProperty("startLocked").boolValue = locked;
                    s.FindProperty("cooldown").floatValue = cooldown;
                });
            }

            /// <summary>A button set into a wall: a 30 cm square, 10 cm deep, standing out of the wall's face
            /// at this point, in this direction. On the outer walls they sit a little higher (1.4 m): those
            /// walls' faces fall on the bounds bake's cell boundaries, and at 1.2 m the air between the button
            /// and the floor would be one cell tall, which the bake reports as a crack.</summary>
            MapButton WallButton(Transform parent, string name, Vector3 at, Vector3 outward, MapButtonPrompt prompt, string custom = "",
                MapWho who = MapWho.Everyone, bool once = false, bool locked = false, float cooldown = 1f)
            {
                Vector3 side = Vector3.Cross(Vector3.up, outward).normalized * 0.15f;
                Vector3 up = Vector3.up * 0.15f;
                return Button(parent, name, at - side - up, at + outward * 0.1f + side + up, prompt, custom, who, once, locked, cooldown);
            }

            static (Vector3 position, Vector3 rotation, float wait) Stop(Vector3 position, Vector3 rotation = default, float wait = 0f) =>
                (position, rotation, wait);

            static MapMover Mover(GameObject go, (Vector3 position, Vector3 rotation, float wait)[] stops, float speed = 2f, float angularSpeed = 90f,
                bool smooth = true, MapMoverLoop loop = MapMoverLoop.Once, float waitAtStart = 0f, float returnAfter = -1f, bool autoStart = false,
                MapMoverBlocked whenBlocked = MapMoverBlocked.Reverse, int crushDamage = 25)
            {
                return Entity<MapMover>(go, s =>
                {
                    SerializedProperty list = s.FindProperty("stops");
                    list.arraySize = stops.Length;
                    for (int i = 0; i < stops.Length; i++)
                    {
                        SerializedProperty stop = list.GetArrayElementAtIndex(i);
                        stop.FindPropertyRelative("position").vector3Value = stops[i].position;
                        stop.FindPropertyRelative("rotation").vector3Value = stops[i].rotation;
                        stop.FindPropertyRelative("wait").floatValue = stops[i].wait;
                    }
                    s.FindProperty("speed").floatValue = speed;
                    s.FindProperty("angularSpeed").floatValue = angularSpeed;
                    s.FindProperty("smooth").boolValue = smooth;
                    s.FindProperty("loop").intValue = (int)loop;
                    s.FindProperty("waitAtStart").floatValue = waitAtStart;
                    s.FindProperty("returnAfter").floatValue = returnAfter;
                    s.FindProperty("autoStart").boolValue = autoStart;
                    s.FindProperty("whenBlocked").intValue = (int)whenBlocked;
                    s.FindProperty("crushDamage").intValue = crushDamage;
                });
            }

            static MapToggle Toggle(GameObject go, bool startEnabled) => Entity<MapToggle>(go, startEnabled: startEnabled);

            static MapSound Sound(Transform parent, string name, Vector3 at, AudioClip clip, float volume = 1f, bool loop = false,
                bool playOnStart = false, bool spatial = true, float minDistance = 1f, float maxDistance = 25f)
            {
                return Entity<MapSound>(Empty(parent, name, at), s =>
                {
                    s.FindProperty("clip").objectReferenceValue = clip;
                    s.FindProperty("volume").floatValue = volume;
                    s.FindProperty("loop").boolValue = loop;
                    s.FindProperty("playOnStart").boolValue = playOnStart;
                    s.FindProperty("spatial").boolValue = spatial;
                    s.FindProperty("minDistance").floatValue = minDistance;
                    s.FindProperty("maxDistance").floatValue = maxDistance;
                });
            }

            static MapEffect Effect(Transform parent, string name, Vector3 at, MapEffectPreset preset, bool startEnabled, float size = 1f,
                float amount = 1f, Color? tint = null)
            {
                return Entity<MapEffect>(Empty(parent, name, at), s =>
                {
                    s.FindProperty("preset").intValue = (int)preset;
                    s.FindProperty("tint").colorValue = tint ?? Color.white;
                    s.FindProperty("size").floatValue = size;
                    s.FindProperty("amount").floatValue = amount;
                }, startEnabled);
            }

            static MapHurtZone HurtZone(Transform parent, string name, Vector3 at, Vector3 center, Vector3 size, int damage, float interval,
                bool instantKill = false, bool startEnabled = true, MapWho who = MapWho.Everyone)
            {
                return Entity<MapHurtZone>(Empty(parent, name, at), s =>
                {
                    s.FindProperty("center").vector3Value = center;
                    s.FindProperty("size").vector3Value = size;
                    s.FindProperty("who").intValue = (int)who;
                    s.FindProperty("damage").intValue = damage;
                    s.FindProperty("interval").floatValue = interval;
                    s.FindProperty("instantKill").boolValue = instantKill;
                }, startEnabled);
            }

            static MapTrigger Trigger(Transform parent, string name, Vector3 at, Vector3 center, Vector3 size, MapWho who = MapWho.Everyone)
            {
                return Entity<MapTrigger>(Empty(parent, name, at), s =>
                {
                    s.FindProperty("center").vector3Value = center;
                    s.FindProperty("size").vector3Value = size;
                    s.FindProperty("who").intValue = (int)who;
                });
            }

            static MapTeleport Teleport(Transform parent, string name, Vector3 at, Vector3 size, Transform destination, MapWho who = MapWho.Everyone,
                bool keepFacing = false, bool startEnabled = true)
            {
                return Entity<MapTeleport>(Empty(parent, name, at), s =>
                {
                    s.FindProperty("center").vector3Value = new Vector3(0f, size.y * 0.5f, 0f);
                    s.FindProperty("size").vector3Value = size;
                    s.FindProperty("who").intValue = (int)who;
                    s.FindProperty("destination").objectReferenceValue = destination;
                    s.FindProperty("keepFacing").boolValue = keepFacing;
                }, startEnabled);
            }
        }
    }
}

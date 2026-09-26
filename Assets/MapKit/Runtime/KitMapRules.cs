using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mimicraft.MapKit
{
    /// <summary>
    /// What a kit map may contain, in one place, for both the check before Unity opens the bundle
    /// (Check, over the inspector's inventory) and the check after the scene has loaded (Audit, over the
    /// live objects). ExternalMapSupportPlan.md §5 is the reasoning; this is the list.
    ///
    /// An allowlist, never a denylist: the game contains hundreds of types a map should not construct, and
    /// a new one arrives with every feature. Anything not named here is refused, including types this list
    /// has never heard of - which is also how the spike finds out what Unity puts in a legitimate scene
    /// bundle that the list is missing.
    ///
    /// The map-facing scripts live in the Mimicraft.MapKit assembly, which the kit shares with the game.
    /// A bundle names each one by assembly, namespace and class, so those three are part of the format and
    /// never change (plan §9); KitAssemblyAudit fails the build if one does.
    /// </summary>
    public sealed class KitMapRules : IKitBundlePolicy
    {
        public static readonly KitMapRules Instance = new KitMapRules();

        // Budgets, plan §5.6. Starting values, to be replaced by what phase 0 measures.
        public const long MaxBundleBytes = 256L << 20;
        public const long MaxTextureBytes = 512L << 20;
        public const long MaxTriangles = 3_000_000;
        public const int MaxRenderers = 10_000;
        public const int MaxLights = 64;

        // Not in the first budget table, and a map could be ruinous under both: one 16384-texel texture
        // fits the total if it is compressed and still fails on every card whose limit is 8192, and a mesh
        // of fifty million vertices and a handful of triangles never touches the triangle count.
        public const long MaxTextureSide = 8192;
        public const long MaxVertices = 6_000_000;

        // Sound (plan §16.5): what a map's clips may hold in memory together, and the longest one.
        public const long MaxAudioBytes = 32L << 20;
        public const double MaxAudioSeconds = 120d;

        static readonly HashSet<int> classes = new HashSet<int>
        {
            1,          // GameObject
            4,          // Transform
            21,         // Material
            23,         // MeshRenderer
            28,         // Texture2D
            29,         // OcclusionCullingSettings
            33,         // MeshFilter
            41,         // OcclusionPortal
            43,         // Mesh
            48,         // Shader (replaced by the game's own before anything draws - see KitShaders)
            49,         // TextAsset (APV cell data)
            64,         // MeshCollider
            65,         // BoxCollider
            83,         // AudioClip (map logic's sounds, plan §16.5; the data's header is checked)
            89,         // Cubemap
            104,        // RenderSettings
            108,        // Light
            115,        // MonoScript
            117,        // Texture3D
            135,        // SphereCollider
            136,        // CapsuleCollider
            142,        // AssetBundle
            150,        // PreloadData
            157,        // LightmapSettings
            187,        // Texture2DArray
            192,        // OcclusionArea
            196,        // NavMeshSettings (every scene has one, empty)
            205,        // LODGroup
            215,        // ReflectionProbe
            220,        // LightProbeGroup
            258,        // LightProbes
            363,        // OcclusionCullingData
            1120,       // LightingDataAsset
            850595691,  // LightingSettings
        };

        const string Kit = "Mimicraft.MapKit";
        const string Core = "Unity.RenderPipelines.Core.Runtime";
        const string Urp = "Unity.RenderPipelines.Universal.Runtime";

        // A type added here usually needs a line in Assets/Scripts/MapKit/link.xml too: only a bundle
        // references it, and the linker cannot see into bundles.

        static readonly HashSet<string> scripts = new HashSet<string>(StringComparer.Ordinal)
        {
            Kit + "|Mimicraft.Gameplay.MapMarker",
            Kit + "|Mimicraft.Gameplay.MapModeObject",
            Kit + "|Mimicraft.Gameplay.MapBoundsVolume",
            Kit + "|Mimicraft.Gameplay.PenetrableSurface",
            Kit + "|Mimicraft.Gameplay.ProbeVolumeRegion",
            Kit + "|Mimicraft.MapKit.KitMapDescriptor",

            // Map logic (plan §16): data the game's MapLogic reads, never code that runs.
            Kit + "|Mimicraft.MapKit.MapButton",
            Kit + "|Mimicraft.MapKit.MapRelay",
            Kit + "|Mimicraft.MapKit.MapTimer",
            Kit + "|Mimicraft.MapKit.MapCounter",
            Kit + "|Mimicraft.MapKit.MapToggle",
            Kit + "|Mimicraft.MapKit.MapRoundEvents",
            Kit + "|Mimicraft.MapKit.MapMover",
            Kit + "|Mimicraft.MapKit.MapTrigger",
            Kit + "|Mimicraft.MapKit.MapHurtZone",
            Kit + "|Mimicraft.MapKit.MapSound",
            Kit + "|Mimicraft.MapKit.MapEffect",
            Kit + "|Mimicraft.MapKit.MapTeleport",

            Urp + "|UnityEngine.Rendering.Universal.UniversalAdditionalLightData",

            Core + "|UnityEngine.Rendering.ProbeVolume",
            Core + "|UnityEngine.Rendering.ProbeVolumePerSceneData",
            Core + "|UnityEngine.Rendering.ProbeAdjustmentVolume",
            Core + "|UnityEngine.Rendering.ProbeVolumeBakingSet",
            Core + "|UnityEngine.Rendering.Volume",
            Core + "|UnityEngine.Rendering.VolumeProfile",

            // Post-processing a map may carry. Colour and tone only: nothing that moves the image
            // (distortion, motion blur, depth of field) or can be tuned into flashing.
            Urp + "|UnityEngine.Rendering.Universal.Bloom",
            Urp + "|UnityEngine.Rendering.Universal.Tonemapping",
            Urp + "|UnityEngine.Rendering.Universal.ColorAdjustments",
            Urp + "|UnityEngine.Rendering.Universal.WhiteBalance",
            Urp + "|UnityEngine.Rendering.Universal.LiftGammaGain",
            Urp + "|UnityEngine.Rendering.Universal.ShadowsMidtonesHighlights",
            Urp + "|UnityEngine.Rendering.Universal.SplitToning",
            Urp + "|UnityEngine.Rendering.Universal.ChannelMixer",
            Urp + "|UnityEngine.Rendering.Universal.ColorCurves",
            Urp + "|UnityEngine.Rendering.Universal.Vignette",
        };

        /// <summary>Shaders a kit material may name. The game swaps each for its own copy of the same
        /// shader before the scene draws, so these must be in the game build.</summary>
        public static readonly string[] Shaders =
        {
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Simple Lit",
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Baked Lit",
            "Mimicraft/VoxelVertexColorLit",
            "Skybox/Procedural",
            "Skybox/Cubemap",
            "Skybox/6 Sided",
            "Skybox/Panoramic",
        };

        static readonly string[] layerNames = { "Default", "Ignore Raycast", "MapBlocker", "GarticMapBlocker" };

        static readonly HashSet<Type> components = new HashSet<Type>
        {
            typeof(Transform),
            typeof(MeshFilter),
            typeof(MeshRenderer),
            typeof(LODGroup),
            typeof(BoxCollider),
            typeof(SphereCollider),
            typeof(CapsuleCollider),
            typeof(MeshCollider),
            typeof(Light),
            typeof(ReflectionProbe),
            typeof(LightProbeGroup),
            typeof(OcclusionArea),
            typeof(OcclusionPortal),
        };

        /// <summary>Scene names a kit map may not use. The game finds its own scenes by name (MapLoader binds
        /// "HunterRoom", GameSceneDirector unloads a map by its scene name), so a kit scene sharing one would
        /// be taken for it. Prefixes for the families the game keeps adding to, so a name that is free today
        /// cannot collide with a map shipped next month. The loader also checks the game's actual build.</summary>
        static readonly string[] reservedSceneNames =
            { "Startup", "Menu", "Game", "HunterRoom", "Editor", "CharacterStudio", "WeaponStudio", "TemplateStudio" };
        static readonly string[] reservedScenePrefixes = { "Map_", "GameMode_" };

        public static bool IsSceneNameReserved(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return false;
            foreach (string name in reservedSceneNames)
            {
                if (string.Equals(name, sceneName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            foreach (string prefix in reservedScenePrefixes)
            {
                if (sceneName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public static string ReservedScenePrefixes => string.Join(", ", reservedScenePrefixes);

        /// <summary>Whether a component may be in a kit map: one of the allowed built-in types, or an allowed
        /// script. A missing script (null) is not. The one definition the game's post-load audit and the
        /// kit's validator both use.</summary>
        public static bool IsComponentAllowed(Component component)
        {
            if (component == null)
                return false;
            Type type = component.GetType();
            return components.Contains(type) || scripts.Contains(type.Assembly.GetName().Name + "|" + type.FullName);
        }

        /// <summary>Whether a type may be a post-processing override in a map's volume profile.</summary>
        public static bool IsVolumeComponentAllowed(Type type) =>
            type != null && scripts.Contains(type.Assembly.GetName().Name + "|" + type.FullName);

        public static IEnumerable<string> LayerNames => layerNames;

        public bool IsClassAllowed(int classId) => classes.Contains(classId);

        public bool IsScriptAllowed(string assembly, string fullName) => scripts.Contains(assembly + "|" + fullName);

        public static bool IsShaderAllowed(string name) => Array.IndexOf(Shaders, name) >= 0;

        public static bool IsLayerAllowed(int layer)
        {
            foreach (string name in layerNames)
            {
                if (LayerMask.NameToLayer(name) == layer)
                    return true;
            }
            return false;
        }

        /// <summary>The same lighting rule for the environment the map carries in its descriptor, which is
        /// what the game actually applies (KitEnvironment.Apply) - the kit scene itself is never the active
        /// scene. Checked after load, because the descriptor is a script's data and the pre-scan does not
        /// read scripts' fields.</summary>
        public static List<string> AuditEnvironment(KitEnvironment environment)
        {
            var findings = new List<string>();
            if (environment == null || !environment.captured)
                return findings;

            if (environment.fog && environment.fogMode != FogMode.ExponentialSquared)
                findings.Add($"the map's environment asks for {environment.fogMode} fog; maps use Exponential Squared");
            if (environment.lightmapsMode != LightmapsMode.CombinedDirectional)
                findings.Add($"the map's environment asks for {environment.lightmapsMode} lightmaps; maps use Directional");
            return findings;
        }

        /// <summary>Reasons to refuse a bundle, from its inventory. Empty means Unity may open it.</summary>
        public static List<string> Check(KitBundleInventory inventory) =>
            Check(inventory, IsLayerAllowed, LayerMask.LayerToName);

        /// <summary>Check with the layer lookup handed in: layer names live in the project's tag manager,
        /// which only a running Unity can read, and the hostile-input tests run the rest of this outside
        /// Unity.</summary>
        public static List<string> Check(KitBundleInventory inventory, Func<int, bool> layerAllowed, Func<int, string> layerName)
        {
            var errors = new List<string>(inventory.Refused);

            if (inventory.SceneFiles.Count != 1)
                errors.Add($"must hold exactly one scene, holds {inventory.SceneFiles.Count}");

            foreach (var pair in inventory.Layers)
            {
                if (!layerAllowed(pair.Key))
                    errors.Add($"{pair.Value} object(s) on layer {pair.Key} ({layerName(pair.Key)}), maps may use {string.Join(", ", layerNames)}");
            }

            if (inventory.TextureBytes > MaxTextureBytes)
                errors.Add($"textures take {inventory.TextureBytes >> 20} MB, the limit is {MaxTextureBytes >> 20} MB");
            if (inventory.MeshTriangles > MaxTriangles)
                errors.Add($"{inventory.MeshTriangles} triangles, the limit is {MaxTriangles}");
            if (inventory.MeshVertices > MaxVertices)
                errors.Add($"{inventory.MeshVertices} vertices, the limit is {MaxVertices}");
            if (inventory.LargestTextureSide > MaxTextureSide)
                errors.Add($"a texture is {inventory.LargestTextureSide} texels across, the limit is {MaxTextureSide}");

            // Lighting the game's shaders are kept for. Project Settings > Graphics strips every other
            // lightmap and fog variant from the build (plan §5.4), so a map lit any other way would draw
            // wrong rather than fail - which is worse, because nobody would know why.
            if (inventory.FogMode >= 0 && inventory.FogMode != (int)FogMode.ExponentialSquared)
                errors.Add($"fog is {(FogMode)inventory.FogMode}; maps use Exponential Squared fog, or none");
            if (inventory.LightmapsMode >= 0 && inventory.LightmapsMode != (int)LightmapsMode.CombinedDirectional)
                errors.Add("lightmaps are baked non-directional; maps bake Directional");
            if (inventory.RealtimeGI)
                errors.Add("realtime global illumination is on; maps use baked lightmaps and APV");

            errors.AddRange(inventory.AudioProblems);
            if (inventory.AudioBytes > MaxAudioBytes)
                errors.Add($"sounds take {inventory.AudioBytes >> 20} MB, the limit is {MaxAudioBytes >> 20} MB");
            if (inventory.LongestAudioSeconds > MaxAudioSeconds)
                errors.Add($"a sound is {inventory.LongestAudioSeconds:0} seconds long, the limit is {MaxAudioSeconds:0}");

            inventory.ClassCounts.TryGetValue(23, out int renderers);
            if (renderers > MaxRenderers)
                errors.Add($"{renderers} renderers, the limit is {MaxRenderers}");

            inventory.ClassCounts.TryGetValue(108, out int lights);
            if (lights > MaxLights)
                errors.Add($"{lights} lights, the limit is {MaxLights}");

            return errors;
        }

        /// <summary>The same rules against the loaded scene, run from sceneLoaded - after Awake, before
        /// Start. A second opinion on the inventory, not a replacement for it.</summary>
        public static List<string> Audit(Scene scene)
        {
            var errors = new List<string>();
            var seenShaders = new HashSet<string>(StringComparer.Ordinal);
            var buffer = new List<Component>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    GameObject go = t.gameObject;
                    if (!IsLayerAllowed(go.layer))
                        Add(errors, $"'{go.name}' is on layer {LayerMask.LayerToName(go.layer)}");

                    go.GetComponents(buffer);
                    foreach (Component component in buffer)
                    {
                        if (component == null)
                            Add(errors, $"'{go.name}' has a script this game does not have");
                        else if (!IsComponentAllowed(component))
                            Add(errors, $"'{go.name}' has {component.GetType().FullName} ({component.GetType().Assembly.GetName().Name})");
                    }

                    if (go.TryGetComponent(out Renderer renderer))
                    {
                        foreach (Material material in renderer.sharedMaterials)
                        {
                            string shader = material != null && material.shader != null ? material.shader.name : null;
                            if (shader != null && seenShaders.Add(shader) && !IsShaderAllowed(shader))
                                Add(errors, $"shader '{shader}' (first seen on '{go.name}')");
                        }
                    }
                }
            }

            return errors;
        }

        static void Add(List<string> errors, string line)
        {
            if (errors.Count < 40)
                errors.Add(line);
            else if (errors.Count == 40)
                errors.Add("and more");
        }
    }
}

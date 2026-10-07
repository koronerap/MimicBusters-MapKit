using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mimicraft.EditorTools
{
    /// <summary>
    /// Map Kit ▸ Tools ▸ Lightmap Cleanup: the three things behind "overlapping UVs" warnings, glowing patches and a
    /// lightmap that grows to gigabytes, fixed on the open scene in one go.
    ///
    /// 1. A light's Indirect Multiplier above 1 is put back to 1. It multiplies bounced light, which takes the colour of
    ///    what it bounces off - at 12 a wooden floor lights the room yellow in patches.
    /// 2. Small static objects stop taking lightmap space and are lit by the light probes instead (Receive Global
    ///    Illumination: Light Probes), as players are. "Small" is the middle one of the object's three dimensions under
    ///    a threshold: a cup or a curtain rod, not a table top. Their faces are smaller than a lightmap texel, so their
    ///    charts overlap whatever the margin, and they are most of a detailed model's texels.
    /// 3. The models' generated lightmap UVs get margins for the resolution they will really be baked at: Margin Method
    ///    Calculate, Min Lightmap Resolution = the scene's, Min Object Scale = the smallest Scale In Lightmap among the
    ///    objects still lightmapped. A margin worked out for a higher resolution than the bake is too thin, which is
    ///    the "scene's lightmap resolution is less than..." warning and most of the overlaps.
    ///
    /// Scene changes are undoable (one undo step) and leave the scene unsaved; model import settings are saved and the
    /// models reimported. A report lists everything changed with the old values. Bake again afterwards.
    /// </summary>
    public static class KitLightmapCleanup
    {
        public const float DefaultSmallSize = 0.5f;

        static string T(string tr, string en) => KitEditorLanguage.T(tr, en);

        [MenuItem("Map Kit/Tools/Lightmap Cleanup", priority = 61)]
        static void RunFromMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!EditorUtility.DisplayDialog("Lightmap Cleanup",
                    T($"'{scene.name}' sahnesinde: 1'den büyük Indirect Multiplier'lar 1 olur, {DefaultSmallSize} m'den küçük " +
                      "statik objeler ışık sondalarından aydınlanır, modellerin lightmap UV payları sahnenin çözünürlüğüne " +
                      "göre ayarlanıp yeniden içe aktarılır. Sahne değişiklikleri geri alınabilir. Sonra yeniden bake et.",
                      $"In '{scene.name}': Indirect Multipliers above 1 become 1, static objects smaller than {DefaultSmallSize} m are " +
                      "lit by the light probes, and the models' lightmap UV margins are set for the scene's resolution and " +
                      "reimported. Scene changes can be undone. Bake again afterwards."),
                    T("Uygula", "Apply"), T("Vazgeç", "Cancel")))
                return;

            string report = Run(scene, DefaultSmallSize);
            Debug.Log("[Lightmap Cleanup]\n" + report);
            EditorUtility.DisplayDialog("Lightmap Cleanup", report.Length > 1500 ? report.Substring(0, 1500) + "\n…" : report, "OK");
        }

        /// <summary>Applies the cleanup to <paramref name="scene"/> and says what it changed.</summary>
        public static string Run(Scene scene, float smallSize)
        {
            var report = new StringBuilder();
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Lightmap Cleanup");
            int group = Undo.GetCurrentGroup();

            var lights = new List<Light>();
            var renderers = new List<MeshRenderer>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                lights.AddRange(root.GetComponentsInChildren<Light>(true));
                renderers.AddRange(root.GetComponentsInChildren<MeshRenderer>(true));
            }

            // 1. Indirect Multiplier.
            int lightsChanged = 0;
            foreach (Light light in lights)
            {
                if (light.bounceIntensity <= 1.001f)
                    continue;

                report.AppendLine(T($"Işık '{light.name}': Indirect Multiplier {light.bounceIntensity:0.##} -> 1",
                    $"Light '{light.name}': Indirect Multiplier {light.bounceIntensity:0.##} -> 1"));
                Undo.RecordObject(light, "Lightmap Cleanup");
                light.bounceIntensity = 1f;
                PrefabUtility.RecordPrefabInstancePropertyModifications(light);
                lightsChanged++;
            }

            // 2. Small objects to the probes.
            int toProbes = 0, lightmapped = 0;
            var stillLightmapped = new List<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers)
            {
                if ((GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) & StaticEditorFlags.ContributeGI) == 0)
                    continue;
                if (renderer.receiveGI != ReceiveGI.Lightmaps)
                    continue;

                Mesh mesh = MeshOf(renderer);
                if (mesh == null)
                    continue;

                if (MiddleDimension(mesh, renderer.transform) < smallSize)
                {
                    Undo.RecordObject(renderer, "Lightmap Cleanup");
                    renderer.receiveGI = ReceiveGI.LightProbes;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    toProbes++;
                    continue;
                }

                stillLightmapped.Add(renderer);
                lightmapped++;
            }

            report.AppendLine(T($"{toProbes} küçük obje artık ışık sondalarından aydınlanıyor (Receive GI: Light Probes); " +
                                $"{lightmapped} obje lightmap'te kaldı.",
                $"{toProbes} small object(s) are now lit by the light probes (Receive GI: Light Probes); " +
                $"{lightmapped} remain lightmapped."));

            // 3. Lightmap UV margins, per model.
            float resolution = 0f;
            int maxSize = 1024;
            if (Lightmapping.TryGetLightingSettings(out LightingSettings settings) && settings != null)
            {
                resolution = settings.lightmapResolution;
                maxSize = settings.lightmapMaxSize;
            }

            var smallestScale = new Dictionary<ModelImporter, float>();
            double texels = 0;
            foreach (MeshRenderer renderer in stillLightmapped)
            {
                float scale = ScaleInLightmap(renderer);
                Mesh mesh = MeshOf(renderer);
                texels += SurfaceArea(mesh, renderer.transform) * (resolution * scale) * (resolution * scale);

                if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(mesh)) is ModelImporter importer)
                {
                    smallestScale.TryGetValue(importer, out float smallest);
                    smallestScale[importer] = smallest <= 0f ? scale : Mathf.Min(smallest, scale);
                }
            }

            if (resolution <= 0f)
            {
                report.AppendLine(T("Sahnenin Lighting Settings'i yok: UV payları ayarlanmadı.",
                    "The scene has no Lighting Settings: UV margins were not set."));
            }
            else
            {
                foreach (KeyValuePair<ModelImporter, float> entry in smallestScale)
                {
                    ModelImporter importer = entry.Key;
                    float minScale = Mathf.Clamp(entry.Value, 0.01f, 1f);
                    var before = $"generate={importer.generateSecondaryUV} margin={importer.secondaryUVMarginMethod} " +
                                 $"minRes={importer.secondaryUVMinLightmapResolution:0.##} minScale={importer.secondaryUVMinObjectScale:0.##}";

                    bool changed = !importer.generateSecondaryUV
                                   || importer.secondaryUVMarginMethod != ModelImporterSecondaryUVMarginMethod.Calculate
                                   || !Mathf.Approximately(importer.secondaryUVMinLightmapResolution, resolution)
                                   || !Mathf.Approximately(importer.secondaryUVMinObjectScale, minScale);
                    if (!changed)
                    {
                        report.AppendLine(T($"Model '{importer.assetPath}': UV payları zaten doğru ({before}).",
                            $"Model '{importer.assetPath}': UV margins already right ({before})."));
                        continue;
                    }

                    importer.generateSecondaryUV = true;
                    importer.secondaryUVMarginMethod = ModelImporterSecondaryUVMarginMethod.Calculate;
                    importer.secondaryUVMinLightmapResolution = resolution;
                    importer.secondaryUVMinObjectScale = minScale;
                    report.AppendLine(T($"Model '{importer.assetPath}': {before} -> generate=True margin=Calculate " +
                                        $"minRes={resolution:0.##} minScale={minScale:0.##}; yeniden içe aktarılıyor.",
                        $"Model '{importer.assetPath}': {before} -> generate=True margin=Calculate " +
                        $"minRes={resolution:0.##} minScale={minScale:0.##}; reimporting."));
                    importer.SaveAndReimport();
                }
            }

            // A rough size, so the next bake's is no surprise: charts are packed to about two thirds of an atlas.
            double perAtlas = (double)maxSize * maxSize * 0.65;
            report.AppendLine(T($"Tahmini lightmap: ~{texels / 1e6:0.0} milyon texel, {maxSize}px atlaslardan ~{System.Math.Ceiling(texels / perAtlas)} tane " +
                                $"(çözünürlük {resolution:0.##} texel/birim). Kesin değer bake'ten sonra Lighting penceresinde.",
                $"Estimated lightmap: ~{texels / 1e6:0.0} million texels, ~{System.Math.Ceiling(texels / perAtlas)} atlas(es) of {maxSize}px " +
                $"(resolution {resolution:0.##} texels/unit). The real figure is in the Lighting window after the bake."));
            report.Insert(0, T($"'{scene.name}': {lightsChanged} ışık, {toProbes} obje değişti.\n",
                $"'{scene.name}': {lightsChanged} light(s), {toProbes} object(s) changed.\n"));

            Undo.CollapseUndoOperations(group);
            if (lightsChanged > 0 || toProbes > 0)
                EditorSceneManager.MarkSceneDirty(scene);
            return report.ToString();
        }

        static Mesh MeshOf(Renderer renderer)
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            return filter != null ? filter.sharedMesh : null;
        }

        /// <summary>The middle one of the object's three dimensions, in metres: its own box times its scale, so a
        /// rotated object measures the same as an upright one.</summary>
        static float MiddleDimension(Mesh mesh, Transform transform)
        {
            Vector3 scale = transform.lossyScale;
            Vector3 size = Vector3.Scale(mesh.bounds.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            float a = size.x, b = size.y, c = size.z;
            return Mathf.Max(Mathf.Min(a, b), Mathf.Min(Mathf.Max(a, b), c));
        }

        /// <summary>The object's surface in square metres. A mesh that cannot be read (Read/Write off, the default for an
        /// imported model) is measured as its box, which is what most blocky map pieces are anyway.</summary>
        static float SurfaceArea(Mesh mesh, Transform transform)
        {
            if (mesh == null)
                return 0f;

            if (!mesh.isReadable)
            {
                Vector3 scale = transform.lossyScale;
                Vector3 s = Vector3.Scale(mesh.bounds.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                return 2f * (s.x * s.y + s.y * s.z + s.x * s.z);
            }

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Matrix4x4 world = transform.localToWorldMatrix;
            float area = 0f;
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                Vector3 a = world.MultiplyPoint3x4(vertices[triangles[i]]);
                Vector3 b = world.MultiplyPoint3x4(vertices[triangles[i + 1]]);
                Vector3 c = world.MultiplyPoint3x4(vertices[triangles[i + 2]]);
                area += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            }

            return area;
        }

        static float ScaleInLightmap(Renderer renderer)
        {
            var serialized = new SerializedObject(renderer);
            SerializedProperty property = serialized.FindProperty("m_ScaleInLightmap");
            return property != null ? Mathf.Max(0.0001f, property.floatValue) : 1f;
        }
    }
}

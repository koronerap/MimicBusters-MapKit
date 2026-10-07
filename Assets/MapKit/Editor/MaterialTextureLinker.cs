using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Mimicraft.EditorTools
{
    /// <summary>
    /// Finds each selected material's texture by NAME and plugs it in - the step Blockbench models need
    /// after import, and the one Unity cannot do for them.
    ///
    /// Why Unity cannot: both of its own routes read the link out of the model file. "Extract Textures"
    /// needs the textures EMBEDDED in the FBX, and the legacy "By Base Texture Name" material search needs
    /// the FBX to at least NAME its texture file. Blockbench's FBX exporter does neither - measured on
    /// House.fbx and dungeon.fbx: no embedded PNG data and no .png filename anywhere in either file. What
    /// Blockbench does do, reliably, is name every material after its texture, so the link exists - it is
    /// just in the file names instead of the file.
    ///
    /// Right-click materials (or the folder holding them) in the Project window, Find Material Textures.
    /// </summary>
    public static class MaterialTextureLinker
    {
        const string ContextMenuPath = "Assets/Find Material Textures";
        const string ToolsMenuPath = "Map Kit/Tools/Find Material Textures";

        /// <summary>Blockbench's own suffix for a texture's emissive layer - avize_emission.png beside
        /// avize.png. Linked when it exists, because a chandelier with its glow texture sitting unused in
        /// the folder is the same missing link as the base colour.</summary>
        const string EmissionSuffix = "_emission";

        [MenuItem(ContextMenuPath, priority = 1200)]
        [MenuItem(ToolsMenuPath)]
        static void LinkSelected()
        {
            Material[] materials = SelectedMaterials();
            if (materials.Length == 0)
                return;

            var report = new Report();

            // One undo step for the whole batch: a hundred materials fixed by one click should come back
            // with one Ctrl+Z, not a hundred.
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            try
            {
                for (int i = 0; i < materials.Length; i++)
                {
                    Material material = materials[i];
                    if (EditorUtility.DisplayCancelableProgressBar("Find Material Textures", material.name,
                            (float)i / materials.Length))
                    {
                        report.Cancelled = true;
                        break;
                    }

                    Link(material, report);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Undo.CollapseUndoOperations(undoGroup);
                Undo.SetCurrentGroupName("Find Material Textures");
                AssetDatabase.SaveAssets();
            }

            report.Print(materials.Length);
        }

        /// <summary>Only offered when there is something to act on - greyed out otherwise, rather than a
        /// click that silently does nothing.</summary>
        [MenuItem(ContextMenuPath, isValidateFunction: true)]
        [MenuItem(ToolsMenuPath, isValidateFunction: true)]
        static bool CanLinkSelected() => SelectedMaterials().Length > 0;

        /// <summary>Materials in the selection, INCLUDING the ones inside a selected folder - so selecting
        /// the Materials folder itself does the whole model, which is what anyone fixing an import of 244
        /// materials actually wants to click.</summary>
        static Material[] SelectedMaterials() =>
            Selection.GetFiltered<Material>(SelectionMode.Assets | SelectionMode.DeepAssets);

        static void Link(Material material, Report report)
        {
            string materialPath = AssetDatabase.GetAssetPath(material);
            if (string.IsNullOrEmpty(materialPath))
                return;

            // A material that already has a base texture is left exactly as it is. The job is filling what
            // the import left empty; overwriting something somebody put there on purpose - or a texture a
            // different model's import resolved correctly - is not a fix anybody asked for.
            if (MainTexture(material) != null)
            {
                report.Skipped.Add(material.name);
                return;
            }

            Texture2D baseTexture = FindTexture(material.name, materialPath, out bool baseWasNearby);

            // The exact name first, always. Only when that finds nothing is the name Unity may have mangled
            // tried - see Unmangled - so a texture that genuinely matches is never passed over for one that
            // merely resembles it.
            bool viaUnmangled = false;
            if (baseTexture == null)
            {
                string unmangled = Unmangled(material.name);
                if (unmangled != material.name)
                {
                    baseTexture = FindTexture(unmangled, materialPath, out baseWasNearby);
                    viaUnmangled = baseTexture != null;
                }
            }

            if (baseTexture == null)
            {
                report.Missing.Add(material.name);
                return;
            }

            Undo.RecordObject(material, "Find Material Textures");
            SetMainTexture(material, baseTexture);
            report.Linked++;

            string texturePath = AssetDatabase.GetAssetPath(baseTexture);
            if (viaUnmangled)
                report.Unmangled.Add($"{material.name}  <-  {Path.GetFileName(texturePath)}");
            if (!baseWasNearby)
                report.FarAway.Add($"{material.name}  <-  {texturePath}");

            // Named after the TEXTURE that was found, not after the material. Blockbench writes the glow
            // layer beside the texture it belongs to, so when the material is "tencere_1" the file on
            // disk is still "tencere_emission" - asking for the material's name would miss it every time
            // the unmangling above was needed.
            Texture2D emission = FindTexture(
                Path.GetFileNameWithoutExtension(texturePath) + EmissionSuffix, materialPath, out _);
            if (emission != null && material.HasProperty("_EmissionMap")
                && material.GetTexture("_EmissionMap") == null)
            {
                SetEmission(material, emission);
                report.Emissive++;
            }

            EditorUtility.SetDirty(material);
        }

        /// <summary>The texture whose file name is exactly `textureName`, preferring the one nearest the
        /// material.
        ///
        /// Searched from the material's PARENT folder first, because that covers both layouts this project
        /// actually has without knowing either: House keeps its textures in House/textures/ beside
        /// House/Materials/, Dungeon keeps them loose in Dungeon/ beside Dungeon/Materials/. Both are "the
        /// folder above the materials, and anything under it".
        ///
        /// Then project-wide, as a fallback, and reported as such (see Report.FarAway). A texture found on
        /// the far side of the project is often right and occasionally a different model's texture of the
        /// same name - "table", "wall" - so it is used but never silently.
        ///
        /// Nearest by shared path, because two maps can each have a "tabure.png", and the one in the same
        /// model's folder is the one that material was painted with.
        ///
        /// EXACT name, case-insensitive. FindAssets alone would not do: its filter is a fuzzy token search,
        /// so "arka bahce ground" also returns "arka bahce  ground tas" - which in this project is a
        /// different material, one space apart.</summary>
        static Texture2D FindTexture(string textureName, string materialPath, out bool nearby)
        {
            nearby = false;

            string materialFolder = Path.GetDirectoryName(materialPath)?.Replace('\\', '/');
            string searchRoot = Path.GetDirectoryName(materialFolder ?? "")?.Replace('\\', '/');

            if (!string.IsNullOrEmpty(searchRoot) && AssetDatabase.IsValidFolder(searchRoot))
            {
                Texture2D local = Nearest(Candidates(textureName, new[] { searchRoot }), materialPath);
                if (local != null)
                {
                    nearby = true;
                    return local;
                }
            }

            return Nearest(Candidates(textureName, null), materialPath);
        }

        static IEnumerable<string> Candidates(string textureName, string[] folders)
        {
            string[] guids = folders == null
                ? AssetDatabase.FindAssets($"t:Texture2D {textureName}")
                : AssetDatabase.FindAssets($"t:Texture2D {textureName}", folders);

            // Trailing whitespace is trimmed on BOTH sides for the comparison and nothing else. Blockbench
            // allows a texture name ending in a space ("orta masa "); Unity will not keep that space in a
            // material's asset name. Everything else about the name still has to match exactly.
            string wanted = textureName.TrimEnd();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(Path.GetFileNameWithoutExtension(path).TrimEnd(), wanted,
                        StringComparison.OrdinalIgnoreCase))
                    yield return path;
            }
        }

        /// <summary>A material name with what Unity's own extraction adds to it taken back off.
        ///
        /// Measured on House: of 243 extracted materials, 13 found no texture by exact name, and 7 of those
        /// were Unity's doing rather than a missing file. Two marks account for all seven:
        ///
        ///   - "_1", "_2": Unity de-duplicates when two of a model's materials share a name, and the second
        ///     one's asset becomes "tencere_1". The texture is still "tencere.png".
        ///   - a trailing "_": Unity will not end an asset file name in a space, and replaces it. The
        ///     texture is "orta masa .png", the material "orta masa_".
        ///
        /// Only the END of the name is touched, and only these two marks. The other six misses were real
        /// - glass materials with no texture at all, a texture that was never exported - and a looser rule
        /// that "found" something for them would be handing a pane of glass somebody else's wood.</summary>
        static string Unmangled(string materialName) =>
            Regex.Replace(materialName, @"_\d*$", "");

        static Texture2D Nearest(IEnumerable<string> paths, string materialPath)
        {
            string best = paths
                .OrderByDescending(path => SharedPrefixLength(path, materialPath))
                .ThenBy(path => path, StringComparer.Ordinal)
                .FirstOrDefault();

            return best != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(best) : null;
        }

        static int SharedPrefixLength(string a, string b)
        {
            int length = Math.Min(a.Length, b.Length);
            int i = 0;
            while (i < length && a[i] == b[i])
                i++;
            return i;
        }

        /// <summary>The base colour texture, whichever name the shader gives it. URP's Lit and Unlit call
        /// it _BaseMap, the built-in and legacy shaders _MainTex, and URP Lit carries BOTH - so reading
        /// either one alone would report an empty slot on a material that is already filled.</summary>
        static Texture MainTexture(Material material)
        {
            if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null)
                return material.GetTexture("_BaseMap");

            return material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
        }

        /// <summary>Written to both slots when both exist, for the reason MainTexture reads both: URP Lit
        /// draws _BaseMap, and anything reading the legacy name - a Standard fallback, a script asking for
        /// mainTexture on an older shader - finds _MainTex.</summary>
        static void SetMainTexture(Material material, Texture2D texture)
        {
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);

            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);
        }

        /// <summary>Plugs in the glow layer and turns emission on.
        ///
        /// The emission COLOUR is only touched when it is black. A black colour multiplies the map to
        /// nothing, so white is the value that makes the texture appear as painted - but a material that
        /// already has a colour has had it chosen by somebody (the dungeon's gas lamps are an HDR orange
        /// several stops over white), and replacing it with plain white would dim exactly the lights that
        /// were tuned to be bright.
        ///
        /// Global illumination flags are left alone for the same reason: whether a lamp bakes into the
        /// lightmap is a lighting decision, not a missing link.</summary>
        static void SetEmission(Material material, Texture2D texture)
        {
            material.SetTexture("_EmissionMap", texture);

            if (material.HasProperty("_EmissionColor")
                && material.GetColor("_EmissionColor").maxColorComponent <= 0f)
                material.SetColor("_EmissionColor", Color.white);

            material.EnableKeyword("_EMISSION");
        }

        sealed class Report
        {
            public int Linked;
            public int Emissive;
            public bool Cancelled;
            public readonly List<string> Missing = new List<string>();
            public readonly List<string> Skipped = new List<string>();
            public readonly List<string> FarAway = new List<string>();
            public readonly List<string> Unmangled = new List<string>();

            public void Print(int total)
            {
                var text = new StringBuilder();
                text.AppendLine($"[Find Material Textures] {total} materyal: {Linked} baglandi, "
                    + $"{Emissive} emission, {Skipped.Count} zaten doluydu, {Missing.Count} bulunamadi"
                    + (Cancelled ? " (IPTAL EDILDI)" : "") + ".");

                AppendList(text, "Ayni isimde texture bulunamadi", Missing);
                AppendList(text, "Unity'nin ekledigi sonek (_1, sondaki _) atilarak eslesti", Unmangled);
                AppendList(text, "Modelin klasoru disinda bulundu - dogru dosya mi kontrol et", FarAway);

                if (Missing.Count > 0 || FarAway.Count > 0)
                    Debug.LogWarning(text.ToString());
                else
                    Debug.Log(text.ToString());

                EditorUtility.DisplayDialog("Find Material Textures",
                    $"{Linked} materyale texture baglandi" + (Emissive > 0 ? $", {Emissive} tanesine emission" : "")
                    + $".\n\n{Skipped.Count} zaten doluydu, dokunulmadi.\n{Missing.Count} icin ayni isimde "
                    + "texture bulunamadi."
                    + (FarAway.Count > 0 ? $"\n{FarAway.Count} tanesi modelin klasoru disinda bulundu." : "")
                    + (Missing.Count > 0 || FarAway.Count > 0 ? "\n\nListe Console'da." : ""),
                    "Tamam");
            }

            static void AppendList(StringBuilder text, string title, List<string> items)
            {
                if (items.Count == 0)
                    return;

                text.AppendLine($"  {title} ({items.Count}):");
                foreach (string item in items)
                    text.AppendLine($"    {item}");
            }
        }
    }
}

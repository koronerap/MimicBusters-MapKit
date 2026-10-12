using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Mimicraft.MapKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Mimicraft.EditorTools
{
    /// <summary>
    /// Packing a map: the open scene built as one AssetBundle that Windows, Linux and macOS all open
    /// (KitMapId.AnyPlatform, manifest format 2), read with the game's own pre-scan (KitBundleInspector and
    /// KitMapRules, the same code the game runs before it lets Unity open a file), the manifest written,
    /// the whole folder put in Builds/Maps/&lt;id&gt; and installed in the game's local maps folder.
    ///
    /// One bundle rather than one per platform since 2026-09-24: with the shaders' variants stripped
    /// (KitShaderStripper) the platforms' bundles were nearly byte for byte the same, a Windows player
    /// opened the Linux one as its own, and the Workshop sends every file of an item to every subscriber.
    /// It is built for the platform the editor runs on, whose Build Support always comes with the editor,
    /// so an author installs no extra module.
    ///
    /// Validation comes first and its errors stop the pack: they are exactly what the game would refuse.
    /// The pre-scan comes after, on the real file, because the validator only estimates from the scene
    /// and a bundle can differ from its scene (a texture's import settings, a shader Unity pulled in).
    ///
    /// A bundle the pre-scan refuses is written to Builds but not installed. The game would refuse it on
    /// load anyway, and an author testing a map should read the reason here rather than in a toast.
    /// </summary>
    public static class KitPack
    {
        /// <summary>The target the bundle is built for: the editor's own platform. Any would do for the
        /// game; this one never needs a module installed.</summary>
        static BuildTarget HostTarget
        {
            get
            {
                switch (Application.platform)
                {
                    case RuntimePlatform.OSXEditor:
                        return BuildTarget.StandaloneOSX;
                    case RuntimePlatform.LinuxEditor:
                        return BuildTarget.StandaloneLinux64;
                    default:
                        return BuildTarget.StandaloneWindows64;
                }
            }
        }

        /// <summary>Where packed maps are written, relative to the project.</summary>
        public const string OutputRoot = "Builds/Maps";

        /// <summary>True while a kit bundle is being built, so KitShaderStripper leaves every other build
        /// alone.</summary>
        public static bool Building { get; private set; }

        public sealed class Bundle
        {
            public string Platform = "";
            public long Size;
            public double Seconds;
            public readonly List<string> Refused = new List<string>();
            public string Report = "";
        }

        public sealed class Result
        {
            public string MapId = "";
            public string MapName = "";
            public string Output = "";
            public string Installed = "";
            public readonly List<Bundle> Bundles = new List<Bundle>();
            public readonly List<string> Notes = new List<string>();

            /// <summary>Why nothing usable came out, or null.</summary>
            public string Error;

            public bool Ok => Error == null;
            public bool Refused => Bundles.Any(b => b.Refused.Count > 0);
        }

        static string T(string tr, string en) => KitEditorLanguage.T(tr, en);

        static string F(string tr, string en, params object[] args) => KitEditorLanguage.F(tr, en, args);

        /// <summary>Packs the scene, and installs it when asked and when the game would accept it. The scene
        /// must be the active one: the environment the map carries is read from the active scene's
        /// settings.</summary>
        public static Result Pack(Scene scene, bool install)
        {
            var result = new Result();

            if (string.IsNullOrEmpty(scene.path))
            {
                result.Error = T("Sahne kaydedilmemiş. Önce kaydet.", "The scene is not saved. Save it first.");
                return result;
            }

            // Every map entity numbered: the number is how the network names it (MapLogicIds). Saving does it
            // too; a scene packed straight after an edit may not have been saved since.
            if (MapLogicIds.Ensure(scene) > 0)
                EditorSceneManager.SaveScene(scene);

            KitValidation validation = KitMapValidator.Run(scene);
            if (!validation.CanPack)
            {
                result.Error = F("Doğrulama {0} hata buldu ve oyun bu haritayı açmaz. Önce Map Kit > Validate'teki hataları düzelt.",
                    "Validation found {0} error(s), and the game would not open this map. Fix the errors in Map Kit > Validate first.",
                    validation.Errors);
                return result;
            }

            // A copied scene keeps pointing at the original's lighting. It opens in the game, lit as the other
            // scene was, which nobody wants to upload - so the author is asked, not stopped.
            var doubts = new List<string>();
            string foreignLighting = KitMapValidator.ForeignLightingData(scene);
            if (foreignLighting != null)
                doubts.Add(F("Sahne başka bir sahnenin ışık verisini kullanıyor ({0}). Paket o sahnenin ışığıyla çıkar.",
                    "The scene uses another scene's lighting data ({0}). The package would carry that scene's light.", foreignLighting));
            if (KitMapValidator.HasForeignProbeData(scene))
                doubts.Add(T("Sahnenin probe verisi başka bir sahnenin Baking Set'ine ait. Oyuncular o sahnenin ışığıyla aydınlanır.",
                    "The scene's probe data belongs to another scene's Baking Set. Players would be lit by that scene's light."));
            if (doubts.Count > 0 && !EditorUtility.DisplayDialog("Map Kit",
                    string.Join("\n\n", doubts) + "\n\n" + T("Kopyalanmış sahnelerde olur. Doğrusu: Lighting penceresinde bu sahne için Generate Lighting.",
                        "Copied scenes do this. The fix: Generate Lighting for this scene in the Lighting window."),
                    T("Yine de paketle", "Pack anyway"), T("İptal", "Cancel")))
            {
                result.Error = T("İptal edildi.", "Cancelled.");
                return result;
            }

            KitMapDescriptor descriptor = FindDescriptor(scene);
            CaptureEnvironment(scene, descriptor, result);

            string id = KitMapId.SanitizeName(scene.name);
            result.MapId = id;
            result.MapName = string.IsNullOrWhiteSpace(descriptor.MapName) ? scene.name : descriptor.MapName.Trim();

            string work = Path.GetFullPath(Path.Combine("Temp", "MapKitBuild", id));
            if (Directory.Exists(work))
                Directory.Delete(work, true);

            string file;
            Bundle bundle;
            uint crc;
            string why;
            try
            {
                EditorUtility.DisplayProgressBar("Map Kit", T("Paketleniyor...", "Packing..."), 0.5f);
                ClearToolLeftovers(result);
                file = Build(scene, work, out bundle, out crc, out why);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (file == null)
            {
                result.Error = string.IsNullOrEmpty(why)
                    ? T("Paket oluşturulamadı. Ayrıntı Console'da.", "The package could not be built. The Console has the details.")
                    : F("Paket oluşturulamadı. Unity'nin verdiği ilk hata:\n{0}\nTamamı Console'da.",
                        "The package could not be built. The first error Unity gave:\n{0}\nThe Console has the rest.", why);
                return result;
            }

            var files = new List<string> { file };
            result.Bundles.Add(bundle);

            string previewFile = TakePreview(scene, work, result);
            if (previewFile != null)
                files.Add(previewFile);
            var listed = new List<KitMapManifest.Bundle>
            {
                new KitMapManifest.Bundle
                {
                    platform = KitMapId.AnyPlatform,
                    file = KitMapId.BundleFileName(KitMapId.AnyPlatform),
                    sha256 = KitMapManifest.Sha256File(file),
                    crc = crc,
                    size = bundle.Size,
                },
            };

            var manifest = new KitMapManifest
            {
                formatVersion = KitMapManifest.CurrentFormat,
                kitVersion = RequiredKitVersion(scene),
                unityVersion = Application.unityVersion,
                name = result.MapName,
                description = descriptor.Description ?? "",
                author = descriptor.Author ?? "",
                modes = (int)descriptor.Modes,
                sceneName = scene.name,
                bundles = listed.ToArray(),
            };
            if (previewFile != null)
            {
                manifest.preview = new KitMapManifest.Preview
                {
                    file = KitPreviewImage.FileName,
                    sha256 = KitMapManifest.Sha256File(previewFile),
                    size = new FileInfo(previewFile).Length,
                };
            }
            string manifestText = JsonUtility.ToJson(manifest, true);

            result.Output = Path.GetFullPath(Path.Combine(OutputRoot, id));
            WriteOutput(result, files, manifestText);

            if (install)
            {
                if (result.Refused)
                    result.Notes.Add(T("Oyuna kurulmadı: oyun bu paketi reddeder.", "Not installed in the game: the game would refuse this package."));
                else
                    Install(result, files, manifestText);
            }

            Debug.Log(Summary(result));
            return result;
        }

        /// <summary>The card picture (KitPreview), written beside the bundle; null, with a note, when the map
        /// has nothing to take it from or the render failed. A picture is not worth a failed pack: the map
        /// still plays, its card is only plain.</summary>
        static string TakePreview(Scene scene, string work, Result result)
        {
            try
            {
                byte[] png = KitPreview.Capture(scene, out string why);
                if (png == null)
                {
                    result.Notes.Add(T("Önizleme resmi yok, kart düz renk görünür: ", "No preview picture, the card shows a flat colour: ") + why);
                    return null;
                }

                string path = Path.Combine(work, KitPreviewImage.FileName);
                File.WriteAllBytes(path, png);
                return path;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                result.Notes.Add(T("Önizleme resmi çekilemedi; harita resimsiz paketlendi.", "The preview picture could not be taken; the map was packed without one."));
                return null;
            }
        }

        /// <summary>A file the packer writes into a map folder: the manifest, a bundle, the picture.</summary>
        static bool IsPackageFile(string name) =>
            name == KitMapId.ManifestFile || name == KitPreviewImage.FileName
            || (name.StartsWith("map.") && name.EndsWith(".bundle"));

        /// <summary>The oldest kit version a game must read to play this map: 2 once it has map logic, 1
        /// otherwise. Written instead of the kit's own version, so a map that uses nothing new opens in a game
        /// that has not heard of map logic.</summary>
        public static int RequiredKitVersion(Scene scene) =>
            scene.GetRootGameObjects().Any(root => root.GetComponentInChildren<MapEntity>(true) != null) ? 2 : 1;

        static KitMapDescriptor FindDescriptor(Scene scene) =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<KitMapDescriptor>(true)).First();

        /// <summary>The scene's sky, ambient, fog and reflections, stored in the descriptor. The game never
        /// makes a kit scene the active one, so these settings would not reach it any other way.</summary>
        static void CaptureEnvironment(Scene scene, KitMapDescriptor descriptor, Result result)
        {
            KitEnvironment environment = KitEnvironment.Capture();
            if (environment.reflectionTexture != null && !AssetDatabase.Contains(environment.reflectionTexture))
            {
                result.Notes.Add(T("Varsayılan yansıma bir asset değil ve pakete giremedi. Sahnede bir Reflection Probe yoksa yüzeyler düz görünebilir.",
                    "The default reflection is not an asset and cannot go in the package. Without a Reflection Probe in the scene, surfaces may look flat."));
                environment.reflectionTexture = null;
            }

            descriptor.EditorSetEnvironment(environment);
            EditorUtility.SetDirty(descriptor);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Removes what an editor tool left hidden in memory, before the build trips over it.
        ///
        /// Unity runs every package's [PostProcessScene] over the scene it builds, and ProBuilder's walks every
        /// ProBuilder mesh LOADED - Resources.FindObjectsOfTypeAll - not only the scene's. Its New Shape tool keeps
        /// a hidden, half-made preview shape (HideAndDontSave) and leaks one each time it is opened again; that
        /// preview has no faces, the post-processor throws on it, and a strict build fails over an object that
        /// is in no scene and would never have been packed. Seen 2026-10-04: Dust2 had no ProBuilder at all.
        ///
        /// Only objects marked not to be saved are touched, so nothing of any scene changes; the tool makes a
        /// new preview when it is next used. Found by type name: the kit does not depend on ProBuilder.
        /// </summary>
        static void ClearToolLeftovers(Result result)
        {
            // An open shape tool would only make its preview again; leave it first.
            Type tool = UnityEditor.EditorTools.ToolManager.activeToolType;
            if (tool != null && (tool.Namespace ?? "").StartsWith("UnityEditor.ProBuilder", StringComparison.Ordinal))
                UnityEditor.EditorTools.ToolManager.RestorePreviousPersistentTool();

            var leftovers = new HashSet<GameObject>();
            foreach (MonoBehaviour behaviour in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
            {
                if (behaviour == null || EditorUtility.IsPersistent(behaviour)
                    || behaviour.GetType().FullName != "UnityEngine.ProBuilder.ProBuilderMesh")
                    continue;

                GameObject owner = behaviour.gameObject;
                if ((owner.hideFlags & HideFlags.DontSave) != 0)
                    leftovers.Add(owner);
            }

            foreach (GameObject leftover in leftovers)
            {
                if (leftover != null)
                    UnityEngine.Object.DestroyImmediate(leftover);
            }

            if (leftovers.Count > 0)
                Debug.Log($"[KitPack] Removed {leftovers.Count} hidden ProBuilder preview object(s) left in memory by its "
                    + "shape tools; ProBuilder's build step fails on them.");
        }

        /// <summary>The bundle, built and read by the pre-scan. The file, or null when Unity could not build it,
        /// with the first error Unity logged on the way in <paramref name="why"/> (the rest is in the Console):
        /// "Building AssetBundle failed" on its own tells an author nothing.</summary>
        static string Build(Scene scene, string work, out Bundle bundle, out uint crc, out string why)
        {
            bundle = new Bundle { Platform = KitMapId.AnyPlatform };
            crc = 0;
            why = null;

            Directory.CreateDirectory(work);
            var build = new AssetBundleBuild
            {
                assetBundleName = KitMapId.BundleFileName(KitMapId.AnyPlatform),
                assetNames = new[] { scene.path },
            };

            var errors = new List<string>();
            void Collect(string message, string stack, LogType type)
            {
                if ((type == LogType.Error || type == LogType.Exception) && !string.IsNullOrWhiteSpace(message))
                    errors.Add(Where(message, stack));
            }

            var clock = Stopwatch.StartNew();
            AssetBundleManifest built;
            Application.logMessageReceived += Collect;
            try
            {
                Building = true;
                built = BuildPipeline.BuildAssetBundles(work, new[] { build },
                    BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode, HostTarget);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                built = null;
            }
            finally
            {
                Building = false;
                Application.logMessageReceived -= Collect;
            }

            string file = Path.Combine(work, build.assetBundleName);
            if (built == null || !File.Exists(file))
            {
                why = errors.FirstOrDefault(IsCause) ?? errors.FirstOrDefault();
                return null;
            }

            BuildPipeline.GetCRCForAssetBundle(file, out crc);
            bundle.Size = new FileInfo(file).Length;
            bundle.Seconds = clock.Elapsed.TotalSeconds;

            if (bundle.Size > KitMapRules.MaxBundleBytes)
                bundle.Refused.Add($"the file is {bundle.Size / 1048576.0:0} MB, over the {KitMapRules.MaxBundleBytes >> 20} MB limit");

            try
            {
                KitBundleInventory inventory = KitBundleInspector.Inspect(file, KitMapRules.Instance);
                bundle.Refused.AddRange(KitMapRules.Check(inventory));
                bundle.Report = KitBundleInspector.Report(inventory);
            }
            catch (KitBundleRejected e)
            {
                bundle.Refused.Add("the pre-scan refused the file: " + e.Message);
            }

            return file;
        }

        /// <summary>Unity's own closing lines, which say that the build failed but not why.</summary>
        static bool IsCause(string error) =>
            !error.StartsWith("Building AssetBundle failed", StringComparison.Ordinal)
            && !error.StartsWith("Error building Player", StringComparison.Ordinal)
            && !error.StartsWith("Failed to process scene before export", StringComparison.Ordinal);

        /// <summary>The message, and for an exception the first frame of its stack that names a file - which
        /// package threw is most of what an author needs to know.</summary>
        static string Where(string message, string stack)
        {
            string first = message.Trim();
            foreach (string line in (stack ?? "").Split('\n'))
            {
                int at = line.IndexOf(" in ", StringComparison.Ordinal);
                if (at < 0)
                    at = line.IndexOf("(at ", StringComparison.Ordinal);
                if (at >= 0)
                    return first + "\n  " + line.Trim();
            }

            return first;
        }

        static void WriteOutput(Result result, List<string> files, string manifestText)
        {
            Directory.CreateDirectory(result.Output);
            foreach (string old in Directory.GetFiles(result.Output))
            {
                if (IsPackageFile(Path.GetFileName(old)))
                    File.Delete(old);
            }

            foreach (string file in files)
                File.Copy(file, Path.Combine(result.Output, Path.GetFileName(file)), true);
            File.WriteAllText(Path.Combine(result.Output, KitMapId.ManifestFile), manifestText);
            // Beside the folder, not in it: the folder is the package, sent to friends as it is.
            File.WriteAllText(ReportPath(result), Report(result));
        }

        /// <summary>Into the game's Maps/Local/&lt;id&gt;. The manifest goes last, so a game that looks while
        /// the copy runs finds the old manifest and new files, which it refuses on their hashes, rather
        /// than a new manifest over files that are not all there yet.</summary>
        static void Install(Result result, List<string> files, string manifestText)
        {
            string installed = Path.Combine(KitGame.LocalMapsFolder, result.MapId);
            var keep = new HashSet<string>(files.Select(Path.GetFileName)) { KitMapId.ManifestFile };

            if (Directory.Exists(installed))
            {
                foreach (string existing in Directory.GetFiles(installed))
                {
                    string name = Path.GetFileName(existing);
                    if (!IsPackageFile(name))
                    {
                        result.Error = F("{0} içinde tanınmayan bir dosya var ({1}). Harita klasöründe yalnızca paketin kendi dosyaları olabilir: onu taşı ya da sil, sonra yeniden paketle.",
                            "{0} holds a file the kit did not put there ({1}). A map folder may only hold the package's own files: move or delete it, then pack again.",
                            installed, name);
                        return;
                    }
                }
            }

            try
            {
                Directory.CreateDirectory(installed);
                foreach (string file in files)
                    File.Copy(file, Path.Combine(installed, Path.GetFileName(file)), true);
                foreach (string existing in Directory.GetFiles(installed))
                {
                    if (!keep.Contains(Path.GetFileName(existing)))
                        File.Delete(existing);
                }
                File.WriteAllText(Path.Combine(installed, KitMapId.ManifestFile), manifestText);
                result.Installed = installed;
            }
            catch (IOException e)
            {
                result.Error = T("Oyuna kurulamadı: dosyalardan biri kullanımda. Oyun açıksa kapat ve yeniden paketle.",
                    "Could not install into the game: one of the files is in use. If the game is running, close it and pack again.")
                    + "\n" + e.Message;
            }
            catch (UnauthorizedAccessException e)
            {
                result.Error = T("Oyuna kurulamadı: klasöre yazma izni yok.", "Could not install into the game: no permission to write the folder.")
                    + "\n" + e.Message;
            }
        }

        public static string ReportPath(Result result) => result.Output + ".report.txt";

        /// <summary>Each bundle's pre-scan report and verdict, as the report file beside the package holds
        /// them.</summary>
        public static string Report(Result result)
        {
            var text = new StringBuilder();
            text.AppendLine($"{result.MapName} ({result.MapId}), kit {KitMapManifest.CurrentKitVersion}, Unity {Application.unityVersion}");
            foreach (Bundle bundle in result.Bundles)
            {
                text.AppendLine();
                text.AppendLine($"[{bundle.Platform}] {bundle.Size / 1048576.0:0.0} MB, built in {bundle.Seconds:0.0} s");
                text.Append(bundle.Report);
                text.AppendLine(bundle.Refused.Count == 0 ? "rules: nothing refused" : "rules: REFUSED\n  - " + string.Join("\n  - ", bundle.Refused));
            }
            return text.ToString();
        }

        static string Summary(Result result)
        {
            var text = new StringBuilder($"[MapKit] {result.MapName} ({result.MapId}): ");
            text.Append(string.Join(", ", result.Bundles.Select(b => $"{b.Platform} {b.Size / 1048576.0:0.0} MB")));
            text.Append(result.Refused ? ", REFUSED by the game's rules" : ", accepted by the game's rules");
            if (!string.IsNullOrEmpty(result.Installed))
                text.Append($", installed in {result.Installed}");
            if (result.Error != null)
                text.Append($"\n{result.Error}");
            foreach (string note in result.Notes)
                text.Append($"\n  - {note}");
            text.Append($"\n{Report(result)}");
            return text.ToString();
        }
    }
}

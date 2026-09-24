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
    /// Packing a map: the open scene built as an AssetBundle for Windows, Linux and macOS, each bundle read
    /// with the game's own pre-scan (KitBundleInspector and KitMapRules, the same code the game runs before
    /// it lets Unity open a file), the manifest written, the whole folder put in Builds/Maps/&lt;id&gt; and
    /// installed in the game's local maps folder.
    ///
    /// Validation comes first and its errors stop the pack: they are exactly what the game would refuse.
    /// The pre-scan comes after, on the real files, because the validator only estimates from the scene
    /// and a bundle can differ from its scene (a texture's import settings, a shader Unity pulled in).
    ///
    /// A bundle the pre-scan refuses is written to Builds but not installed. The game would refuse it on
    /// load anyway, and an author testing a map should read the reason here rather than in a toast.
    /// </summary>
    public static class KitPack
    {
        public struct Platform
        {
            public string Key;
            public BuildTarget Target;
            public string Label;
        }

        public static readonly Platform[] Platforms =
        {
            new Platform { Key = "windows", Target = BuildTarget.StandaloneWindows64, Label = "Windows" },
            new Platform { Key = "linux", Target = BuildTarget.StandaloneLinux64, Label = "Linux" },
            new Platform { Key = "macos", Target = BuildTarget.StandaloneOSX, Label = "macOS" },
        };

        /// <summary>Whether Unity can build for a platform here: its Build Support module is installed.</summary>
        public static bool IsInstalled(Platform platform) =>
            BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, platform.Target);

        /// <summary>The platform this editor runs on, which is the bundle the game on this computer reads.</summary>
        public static string HostKey => KitMapId.PlatformKey;

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

        static string F(string tr, string en, params object[] args) =>
            string.Format(KitEditorLanguage.Culture, KitEditorLanguage.T(tr, en), args);

        /// <summary>Packs the scene for the platforms named (KitMapId platform keys), and installs it when
        /// asked and when the game would accept it. The scene must be the active one: the environment the
        /// map carries is read from the active scene's settings.</summary>
        public static Result Pack(Scene scene, ICollection<string> platforms, bool install)
        {
            var result = new Result();

            if (string.IsNullOrEmpty(scene.path))
            {
                result.Error = T("Sahne kaydedilmemiş. Önce kaydet.", "The scene is not saved. Save it first.");
                return result;
            }

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

            var listed = new List<KitMapManifest.Bundle>();
            var files = new List<string>();
            Platform[] chosen = Platforms.Where(p => platforms.Contains(p.Key)).ToArray();
            try
            {
                for (int i = 0; i < chosen.Length; i++)
                {
                    Platform platform = chosen[i];
                    if (!IsInstalled(platform))
                    {
                        result.Notes.Add(F("{0} atlandı: Unity Hub'dan {0} Build Support modülünü kur.",
                            "{0} skipped: install the {0} Build Support module from Unity Hub.", platform.Label));
                        continue;
                    }

                    if (EditorUtility.DisplayCancelableProgressBar("Map Kit", F("{0} için paketleniyor...", "Packing for {0}...", platform.Label),
                            (float)i / chosen.Length))
                    {
                        result.Error = T("İptal edildi.", "Cancelled.");
                        return result;
                    }

                    string file = Build(scene, platform, work, out Bundle bundle, out uint crc);
                    if (file == null)
                    {
                        // This computer's platform is the one a test needs; without it there is nothing to
                        // install. Another platform failing leaves a map that still works here.
                        string failed = F("{0} paketi oluşturulamadı. Ayrıntı Console'da.",
                            "The {0} package could not be built. The Console has the details.", platform.Label);
                        if (platform.Key == HostKey)
                        {
                            result.Error = failed;
                            return result;
                        }
                        result.Notes.Add(failed + T(" Harita onsuz paketlendi.", " The map was packed without it."));
                        continue;
                    }

                    files.Add(file);
                    result.Bundles.Add(bundle);
                    listed.Add(new KitMapManifest.Bundle
                    {
                        platform = platform.Key,
                        file = KitMapId.BundleFileName(platform.Key),
                        sha256 = KitMapManifest.Sha256File(file),
                        crc = crc,
                        size = bundle.Size,
                    });
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (listed.Count == 0)
            {
                result.Error = T("Hiçbir platform için paketlenemedi.", "Nothing could be packed for any platform.");
                return result;
            }

            var manifest = new KitMapManifest
            {
                formatVersion = KitMapManifest.CurrentFormat,
                kitVersion = KitMapManifest.CurrentKitVersion,
                unityVersion = Application.unityVersion,
                name = result.MapName,
                description = descriptor.Description ?? "",
                author = descriptor.Author ?? "",
                modes = (int)descriptor.Modes,
                sceneName = scene.name,
                bundles = listed.ToArray(),
            };
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

        /// <summary>One platform's bundle, built and read by the pre-scan. The file, or null when Unity could
        /// not build it (the reason is in the Console).</summary>
        static string Build(Scene scene, Platform platform, string work, out Bundle bundle, out uint crc)
        {
            bundle = new Bundle { Platform = platform.Key };
            crc = 0;

            string folder = Path.Combine(work, platform.Key);
            Directory.CreateDirectory(folder);
            var build = new AssetBundleBuild
            {
                assetBundleName = KitMapId.BundleFileName(platform.Key),
                assetNames = new[] { scene.path },
            };

            var clock = Stopwatch.StartNew();
            AssetBundleManifest built;
            try
            {
                Building = true;
                built = BuildPipeline.BuildAssetBundles(folder, new[] { build },
                    BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode, platform.Target);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                built = null;
            }
            finally
            {
                Building = false;
            }

            string file = Path.Combine(folder, build.assetBundleName);
            if (built == null || !File.Exists(file))
                return null;

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

        static void WriteOutput(Result result, List<string> files, string manifestText)
        {
            Directory.CreateDirectory(result.Output);
            foreach (string old in Directory.GetFiles(result.Output))
            {
                string name = Path.GetFileName(old);
                if (name == KitMapId.ManifestFile || (name.StartsWith("map.") && name.EndsWith(".bundle")))
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
                    bool ours = name == KitMapId.ManifestFile || (name.StartsWith("map.") && name.EndsWith(".bundle"));
                    if (!ours)
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

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Mimicraft.MapKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mimicraft.EditorTools
{
    /// <summary>
    /// Map Kit > Pack and Test: KitPack for the open scene, and the game started on the result.
    ///
    /// Both buttons install the map, because a packed map the game cannot see is only half useful: after
    /// Pack it is in the game's map picker, for a session with friends who have the same files. Pack and
    /// Test also starts the game straight into a private Practice session on it (KitMapLaunch, game side).
    ///
    /// The game keeps a loaded map's files open, so a running game is closed first, with the author's
    /// say-so. Replacing files under a game that has them open fails on Windows and misleads everywhere
    /// else.
    /// </summary>
    public sealed class KitPackWindow : EditorWindow
    {
        const string PlatformPrefix = "MimicBusters.MapKit.Pack.";

        KitPack.Result last;
        List<KitGame.Install> installs;
        Vector2 scroll;
        GUIStyle wrap;

        [MenuItem("Map Kit/Pack and Test", priority = 20)]
        public static void Open()
        {
            var window = GetWindow<KitPackWindow>();
            window.titleContent = new GUIContent("Map Kit");
            window.minSize = new Vector2(380, 320);
        }

        [MenuItem("Map Kit/Open the Game's Maps Folder", priority = 40)]
        static void RevealMaps()
        {
            Directory.CreateDirectory(KitGame.LocalMapsFolder);
            EditorUtility.RevealInFinder(KitGame.LocalMapsFolder + Path.DirectorySeparatorChar);
        }

        static string T(string tr, string en) => KitEditorLanguage.T(tr, en);

        static string F(string tr, string en, params object[] args) =>
            string.Format(KitEditorLanguage.Culture, KitEditorLanguage.T(tr, en), args);

        static bool PlatformOn(string key) => EditorPrefs.GetBool(PlatformPrefix + key, true);

        void OnEnable()
        {
            if (!KitGame.IsPlayer(KitGame.GamePath))
                FindGame(quiet: true);
        }

        void FindGame(bool quiet)
        {
            installs = KitGame.FindSteamInstalls();
            if (installs.Count > 0 && !installs.Any(i => i.Path == KitGame.GamePath))
                KitGame.GamePath = installs[0].Path;
            if (!quiet && installs.Count == 0)
                ShowNotification(new GUIContent(T("Steam'de oyun bulunamadı. Elle seç.", "The game was not found in Steam. Choose it by hand.")));
        }

        void OnGUI()
        {
            wrap ??= new GUIStyle(EditorStyles.label) { wordWrap = true, richText = false };

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(T("Paketle ve Test Et", "Pack and Test"), EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                KitEditorLanguage.Toolbar();
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);

            Scene scene = SceneManager.GetActiveScene();
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(T("Harita", "Map"), EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(string.IsNullOrEmpty(scene.path)
                    ? T("Sahne kaydedilmemiş.", "The scene is not saved.")
                    : F("{0}  ·  kimlik {1}", "{0}  ·  id {1}", scene.name, KitMapId.SanitizeName(scene.name)));
                if (GUILayout.Button(T("Doğrula", "Validate"), GUILayout.Width(80)))
                    KitMapValidatorWindow.Open();
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(T("Platformlar", "Platforms"), EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (KitPack.Platform platform in KitPack.Platforms)
                {
                    bool installed = KitPack.IsInstalled(platform);
                    using (new EditorGUI.DisabledScope(!installed))
                    {
                        bool on = installed && PlatformOn(platform.Key);
                        string label = installed ? platform.Label : platform.Label + T(" (modül yok)", " (no module)");
                        bool now = GUILayout.Toggle(on, label, GUILayout.ExpandWidth(false));
                        if (installed && now != on)
                            EditorPrefs.SetBool(PlatformPrefix + platform.Key, now);
                    }
                    GUILayout.Space(8);
                }
            }
            EditorGUILayout.LabelField(T("Test için bu bilgisayarın platformu her zaman paketlenir. Atölye'ye yüklemek için üçü de gerekir.",
                "For a test, this computer's platform is always packed. Uploading to the Workshop needs all three."), EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(6);
            DrawGame();

            EditorGUILayout.Space(10);
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(scene.path)))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(T("Paketle", "Pack"), GUILayout.Height(28)))
                    Run(test: false);
                if (GUILayout.Button(T("Paketle ve Test Et", "Pack and Test"), GUILayout.Height(28)))
                    Run(test: true);
            }

            if (last != null)
                DrawResult();

            EditorGUILayout.EndScrollView();
        }

        void DrawGame()
        {
            EditorGUILayout.LabelField(T("Oyun", "Game"), EditorStyles.boldLabel);

            string game = KitGame.GamePath;
            bool found = KitGame.IsPlayer(game);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(found ? game : T("Seçilmedi", "Not chosen"), wrap);
                if (GUILayout.Button(T("Seç...", "Choose..."), GUILayout.Width(72)))
                    ChooseGame();
                if (GUILayout.Button(T("Steam'de bul", "Find in Steam"), GUILayout.Width(96)))
                    FindGame(quiet: false);
            }

            if (installs != null && installs.Count > 1)
            {
                int current = installs.FindIndex(i => i.Path == game);
                int picked = EditorGUILayout.Popup(T("Kurulu sürümler", "Installed copies"), Mathf.Max(0, current),
                    installs.Select(i => i.Label).ToArray());
                if (picked != current)
                    KitGame.GamePath = installs[picked].Path;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(F("Haritalar: {0}", "Maps: {0}", KitGame.LocalMapsFolder), EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button(T("Aç", "Open"), EditorStyles.miniButton, GUILayout.Width(48)))
                    RevealMaps();
                if (GUILayout.Button(T("Değiştir...", "Change..."), EditorStyles.miniButton, GUILayout.Width(72)))
                    ChooseDataFolder();
                if (!string.IsNullOrEmpty(KitGame.DataFolderOverride)
                    && GUILayout.Button(T("Varsayılan", "Default"), EditorStyles.miniButton, GUILayout.Width(72)))
                    KitGame.DataFolderOverride = "";
            }
        }

        void ChooseGame()
        {
            string picked = Application.platform == RuntimePlatform.OSXEditor
                ? EditorUtility.OpenFilePanel(T("Oyunu seç", "Choose the game"), "/Applications", "app")
                : EditorUtility.OpenFilePanel(T("Oyunu seç", "Choose the game"), "",
                    Application.platform == RuntimePlatform.WindowsEditor ? "exe" : "x86_64");
            if (string.IsNullOrEmpty(picked))
                return;
            if (!KitGame.IsPlayer(picked))
            {
                EditorUtility.DisplayDialog("Map Kit", T("Bu bir Unity oyunu gibi görünmüyor: yanında _Data klasörü yok.",
                    "That does not look like a Unity game: there is no _Data folder beside it."), T("Tamam", "OK"));
                return;
            }
            KitGame.GamePath = picked;
        }

        void ChooseDataFolder()
        {
            string picked = EditorUtility.OpenFolderPanel(T("Oyunun veri klasörü", "The game's data folder"), KitGame.DataFolder, "");
            if (!string.IsNullOrEmpty(picked))
                KitGame.DataFolderOverride = picked;
        }

        void Run(bool test)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            // Declined above: packing saves the scene (the environment goes into the descriptor), and that
            // would save the very changes the author just chose not to.
            Scene scene = SceneManager.GetActiveScene();
            if (scene.isDirty)
            {
                ShowNotification(new GUIContent(T("Sahnede kaydedilmemiş değişiklik var. Paketlemek için kaydet.",
                    "The scene has unsaved changes. Save it to pack.")));
                return;
            }

            var platforms = KitPack.Platforms.Where(p => KitPack.IsInstalled(p) && PlatformOn(p.Key)).Select(p => p.Key).ToList();
            if (test && !platforms.Contains(KitPack.HostKey))
                platforms.Add(KitPack.HostKey);
            if (platforms.Count == 0)
            {
                ShowNotification(new GUIContent(T("En az bir platform seç.", "Pick at least one platform.")));
                return;
            }

            if (test && !KitGame.IsPlayer(KitGame.GamePath))
            {
                EditorUtility.DisplayDialog("Map Kit", T("Oyun bulunamadı. Önce Oyun bölümünden seç.", "The game was not found. Choose it under Game first."), T("Tamam", "OK"));
                return;
            }

            // The game keeps a loaded map's files open. Closed first, if the author agrees.
            Process[] running = KitGame.Running();
            if (running.Length > 0)
            {
                int choice = EditorUtility.DisplayDialogComplex("Map Kit",
                    T("Oyun açık. Harita dosyaları değişirken oyunun kapalı olması gerekir.", "The game is running. It has to be closed while the map files change."),
                    T("Oyunu kapat", "Close the game"), T("İptal", "Cancel"), T("Açık kalsın", "Leave it running"));
                if (choice == 1)
                    return;
                if (choice == 0 && !KitGame.Close(running))
                {
                    EditorUtility.DisplayDialog("Map Kit", T("Oyun kapatılamadı. Elle kapat ve yeniden dene.", "The game could not be closed. Close it by hand and try again."), T("Tamam", "OK"));
                    return;
                }
            }

            last = KitPack.Pack(scene, platforms, install: true);
            Repaint();

            if (!last.Ok || last.Refused || string.IsNullOrEmpty(last.Installed) || !test)
                return;

            if (!KitGame.Launch(last.MapId, out string error))
                EditorUtility.DisplayDialog("Map Kit", T("Oyun başlatılamadı: ", "The game could not be started: ") + error, T("Tamam", "OK"));
        }

        void DrawResult()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(T("Son paket", "Last pack"), EditorStyles.boldLabel);

            if (!last.Ok)
                EditorGUILayout.HelpBox(last.Error, MessageType.Error);
            else if (last.Refused)
                EditorGUILayout.HelpBox(T("Paket oluştu ama oyun onu reddeder. Nedenleri aşağıda.", "The package was made, but the game would refuse it. The reasons are below."), MessageType.Error);
            else if (!string.IsNullOrEmpty(last.Installed))
                EditorGUILayout.HelpBox(F("'{0}' paketlendi ve oyuna kuruldu. Oyunda harita seçicinin Topluluk bölümünde.",
                    "'{0}' is packed and installed in the game. In the game it is under Community in the map picker.", last.MapName), MessageType.Info);

            foreach (KitPack.Bundle bundle in last.Bundles)
            {
                string verdict = bundle.Refused.Count == 0 ? T("oyun kabul eder", "the game accepts it") : T("oyun reddeder", "the game refuses it");
                EditorGUILayout.LabelField(F("{0}: {1:0.0} MB, {2:0} sn, {3}", "{0}: {1:0.0} MB, {2:0} s, {3}",
                    PlatformLabel(bundle.Platform), bundle.Size / 1048576.0, bundle.Seconds, verdict));
                foreach (string reason in bundle.Refused)
                    EditorGUILayout.LabelField("  " + reason, wrap);
            }

            foreach (string note in last.Notes)
                EditorGUILayout.HelpBox(note, MessageType.Warning);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (!string.IsNullOrEmpty(last.Output) && Directory.Exists(last.Output)
                    && GUILayout.Button(T("Paket klasörü", "Package folder"), EditorStyles.miniButton))
                    EditorUtility.RevealInFinder(Path.Combine(last.Output, KitMapId.ManifestFile));
                if (!string.IsNullOrEmpty(last.Installed) && Directory.Exists(last.Installed)
                    && GUILayout.Button(T("Oyundaki klasör", "Folder in the game"), EditorStyles.miniButton))
                    EditorUtility.RevealInFinder(Path.Combine(last.Installed, KitMapId.ManifestFile));
                if (last.Bundles.Count > 0 && GUILayout.Button(T("Raporu Console'a yaz", "Report to the Console"), EditorStyles.miniButton))
                    UnityEngine.Debug.Log("[MapKit] " + KitPack.Report(last));
            }
        }

        static string PlatformLabel(string key) =>
            KitPack.Platforms.Where(p => p.Key == key).Select(p => p.Label).DefaultIfEmpty(key).First();
    }
}

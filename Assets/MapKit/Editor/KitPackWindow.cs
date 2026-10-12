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
        KitPack.Result last;
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
        internal static void RevealMaps()
        {
            Directory.CreateDirectory(KitGame.LocalMapsFolder);
            EditorUtility.RevealInFinder(KitGame.LocalMapsFolder + Path.DirectorySeparatorChar);
        }

        static string T(string tr, string en) => KitEditorLanguage.T(tr, en);

        static string F(string tr, string en, params object[] args) => KitEditorLanguage.F(tr, en, args);

        void OnEnable() => KitGameGui.FindIfMissing();

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

            EditorGUILayout.LabelField(T("Tek paket: Windows, Linux ve macOS aynı dosyayı açar.",
                "One package: Windows, Linux and macOS open the same file."), EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(6);
            DrawGame();

            EditorGUILayout.Space(10);
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(scene.path)))
            using (new EditorGUILayout.HorizontalScope())
            {
                bool pack = GUILayout.Button(T("Paketle", "Pack"), GUILayout.Height(28));
                bool packAndTest = GUILayout.Button(T("Paketle ve Test Et", "Pack and Test"), GUILayout.Height(28));
                if (pack || packAndTest)
                {
                    Run(test: packAndTest);

                    // A pack builds, reloads the scene and shows dialogs in the middle of this OnGUI, which leaves
                    // the layout groups opened above unbalanced ("EndLayoutGroup: BeginLayoutGroup must be called
                    // first"). Unity's way out: end this OnGUI here and draw afresh.
                    GUIUtility.ExitGUI();
                }
            }

            if (last != null)
                DrawResult();

            EditorGUILayout.EndScrollView();
        }

        void DrawGame()
        {
            EditorGUILayout.LabelField(T("Oyun", "Game"), EditorStyles.boldLabel);
            KitGameGui.Draw(this);
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

            last = KitPack.Pack(scene, install: true);
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
            key == KitMapId.AnyPlatform ? T("Tüm platformlar", "All platforms") : key;
    }
}

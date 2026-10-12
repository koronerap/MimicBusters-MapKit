using System.IO;
using Mimicraft.MapKit;
using UnityEditor;
using UnityEngine;

namespace Mimicraft.EditorTools
{
    /// <summary>
    /// Map Kit > Welcome: what a map author sees the first time the kit project opens on their computer. The language
    /// first - everything under it is in that language at once - then where the game is, then the way in: a new map,
    /// the example map, the logic pieces, the validator, Pack and Test, and the guide.
    ///
    /// Opened by itself once per computer (EditorPrefs), or every time when the author asks for it in Settings. Never in
    /// the game's own project, which carries the kit's code too, and never in batch mode.
    /// </summary>
    [InitializeOnLoad]
    public sealed class KitWelcomeWindow : EditorWindow
    {
        const string SeenKey = "MimicBusters.MapKit.WelcomeSeen";
        const string StartupKey = "MimicBusters.MapKit.WelcomeAtStartup";
        const string SessionKey = "MimicBusters.MapKit.WelcomeAskedThisSession";

        Vector2 scroll;
        GUIStyle heading;
        GUIStyle body;

        static string T(string tr, string en) => KitEditorLanguage.T(tr, en);
        static string F(string tr, string en, params object[] args) => KitEditorLanguage.F(tr, en, args);

        /// <summary>Whether the window opens every time the project does, not only the first time.</summary>
        public static bool ShowAtStartup
        {
            get => EditorPrefs.GetBool(StartupKey, false);
            set => EditorPrefs.SetBool(StartupKey, value);
        }

        static KitWelcomeWindow() => EditorApplication.delayCall += OpenAtStartup;

        // Once per editor session: a script reload runs the static constructor again, and the window must not come
        // back every time a script is saved.
        static void OpenAtStartup()
        {
            if (Application.isBatchMode || SessionState.GetBool(SessionKey, false))
                return;
            SessionState.SetBool(SessionKey, true);

            if (PlayerSettings.productName == KitGame.Product)
                return;

            if (!EditorPrefs.GetBool(SeenKey, false) || ShowAtStartup)
                Open();
        }

        [MenuItem("Map Kit/Welcome", priority = 0)]
        public static void Open()
        {
            EditorPrefs.SetBool(SeenKey, true);
            var window = GetWindow<KitWelcomeWindow>(true);
            window.Retitle();
            window.minSize = new Vector2(460, 560);
        }

        void OnEnable()
        {
            KitEditorLanguage.Changed += Retitle;
            KitGameGui.FindIfMissing();
            Retitle();
        }

        void OnDisable() => KitEditorLanguage.Changed -= Retitle;

        void Retitle() => titleContent = new GUIContent(T("Hoş geldin", "Welcome"));

        void OnGUI()
        {
            heading ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, wordWrap = true };
            body ??= new GUIStyle(EditorStyles.label) { wordWrap = true };

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.Space(10);

            GUILayout.Label("Mimic Busters Map Kit", heading);
            GUILayout.Label(F("Kit sürümü {0}", "Kit version {0}", KitMapManifest.CurrentKitVersion), EditorStyles.miniLabel);
            EditorGUILayout.Space(4);
            GUILayout.Label(T("Kendi haritanı yap, oyunda dene ve arkadaşlarınla oyna. Önce dilini seç ve oyunun yerini " +
                              "göster; sonra aşağıdan başla.",
                "Make your own map, try it in the game and play it with your friends. Pick your language and show the kit " +
                "where the game is first; then start from below."), body);

            Section(T("1. Dil", "1. Language"));
            KitSettingsWindow.DrawLanguage();

            Section(T("2. Oyun", "2. The game"));
            GUILayout.Label(T("Paketle ve Test Et haritayı bu oyunda açar. Steam'de kuruluysa kendiliğinden bulunur.",
                "Pack and Test opens the map in this copy of the game. If Steam has it installed, it is found by itself."),
                EditorStyles.wordWrappedMiniLabel);
            KitGameGui.Draw(this);

            Section(T("3. Başla", "3. Start"));
            Step(T("Yeni harita", "New map"),
                T("Boş bir harita sahnesi kurar: zemin, ışık ve doğma noktalarıyla.",
                    "Sets up an empty map scene, with a floor, light and spawn points."),
                KitNewMapWindow.Open);
            Step(T("Örnek harita", "Example map"),
                T("Kapıları, butonları ve tetikleyicileri kullanan hazır bir harita. İncelemek için iyi bir başlangıç.",
                    "A ready map that uses doors, buttons and triggers. A good place to look around first."),
                () => EditorApplication.ExecuteMenuItem("Map Kit/Examples/Logic Playground"));
            Step(T("Mantık", "Logic"),
                T("Kapı, buton, tetikleyici gibi parçalar ve birbirlerine nasıl bağlandıkları.",
                    "Pieces such as doors, buttons and triggers, and how they are wired together."),
                KitLogicWindow.Open);
            Step(T("Doğrula", "Validate"),
                T("Haritadaki sorunları listeler. Paketlemeden önce bak.",
                    "Lists what is wrong with the map. Look before you pack."),
                KitMapValidatorWindow.Open);
            Step(T("Paketle ve Test Et", "Pack and Test"),
                T("Haritayı paketler ve oyunda açar.", "Packs the map and opens it in the game."),
                KitPackWindow.Open);
            Step(T("Kılavuz", "Guide"),
                T("Kitin kılavuzu: her parça ve her adım.", "The kit's guide: every piece and every step."),
                OpenGuide);

            EditorGUILayout.Space(12);
            using (new EditorGUILayout.HorizontalScope())
            {
                ShowAtStartup = EditorGUILayout.ToggleLeft(T("Proje her açıldığında göster", "Show every time the project opens"),
                    ShowAtStartup);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(T("Ayarlar", "Settings"), GUILayout.Width(90)))
                    KitSettingsWindow.Open();
                if (GUILayout.Button(T("Kapat", "Close"), GUILayout.Width(90)))
                    Close();
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.EndScrollView();
        }

        static void Section(string heading)
        {
            EditorGUILayout.Space(14);
            EditorGUILayout.LabelField(heading, EditorStyles.boldLabel);
        }

        void Step(string label, string description, System.Action open)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(label, GUILayout.Width(150), GUILayout.Height(24)))
                {
                    open();
                    GUIUtility.ExitGUI();
                }
                GUILayout.Label(description, body);
            }
        }

        /// <summary>The README beside the project in the kit's language (README.de.md, README.ja.md...), the English
        /// README.md when that language has none.</summary>
        static void OpenGuide()
        {
            string root = Directory.GetParent(Application.dataPath)?.FullName ?? "";
            string english = Path.Combine(root, "README.md");
            string own = Path.Combine(root, $"README.{KitEditorLanguage.Language}.md");
            string guide = KitEditorLanguage.Language != "en" && File.Exists(own) ? own : english;

            if (File.Exists(guide))
                EditorUtility.OpenWithDefaultApp(guide);
            else
                EditorUtility.DisplayDialog("Map Kit", T("Kılavuz bulunamadı: README.md projenin klasöründe olmalı.",
                    "The guide was not found: README.md should be in the project's folder."), T("Tamam", "OK"));
        }
    }
}

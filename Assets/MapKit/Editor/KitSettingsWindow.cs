using UnityEditor;
using UnityEngine;

namespace Mimicraft.EditorTools
{
    /// <summary>
    /// Map Kit > Settings: the kit's own preferences, all per machine (EditorPrefs) - the language every kit window
    /// speaks, where the game is installed and where it keeps its maps, and whether the welcome window opens with the
    /// project. Reached from the menu, the gear in each kit window's toolbar, and the welcome window.
    /// </summary>
    public sealed class KitSettingsWindow : EditorWindow
    {
        Vector2 scroll;

        static string T(string tr, string en) => KitEditorLanguage.T(tr, en);

        [MenuItem("Map Kit/Settings", priority = 200)]
        public static void Open()
        {
            var window = GetWindow<KitSettingsWindow>();
            window.Retitle();
            window.minSize = new Vector2(380, 300);
        }

        void OnEnable()
        {
            KitEditorLanguage.Changed += Retitle;
            KitGameGui.FindIfMissing();
            Retitle();
        }

        void OnDisable() => KitEditorLanguage.Changed -= Retitle;

        void Retitle() => titleContent = new GUIContent(T("Kit Ayarları", "Kit Settings"));

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField(T("Dil", "Language"), EditorStyles.boldLabel);
            DrawLanguage();
            EditorGUILayout.LabelField(T("Kitin bütün pencereleri bu dili kullanır.", "All of the kit's windows use this language."),
                EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField(T("Oyun", "The game"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(T("Paketle ve Test Et haritayı bu oyunda açar.",
                "Pack and Test opens the map in this copy of the game."), EditorStyles.wordWrappedMiniLabel);
            KitGameGui.Draw(this);

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField(T("Hoş geldin penceresi", "Welcome window"), EditorStyles.boldLabel);
            KitWelcomeWindow.ShowAtStartup = EditorGUILayout.ToggleLeft(
                T("Proje her açıldığında göster", "Show it every time the project opens"), KitWelcomeWindow.ShowAtStartup);
            if (GUILayout.Button(T("Hoş geldin penceresini aç", "Open the welcome window"), GUILayout.Width(220)))
                KitWelcomeWindow.Open();

            EditorGUILayout.EndScrollView();
        }

        /// <summary>The language dropdown, in each language's own name. Shared with the welcome window.</summary>
        public static void DrawLanguage()
        {
            var names = new string[KitEditorLanguage.Languages.Length];
            for (int i = 0; i < names.Length; i++)
                names[i] = KitEditorLanguage.Languages[i].Name;

            int current = Mathf.Max(0, KitEditorLanguage.IndexOf(KitEditorLanguage.Language));
            int picked = EditorGUILayout.Popup(current, names);
            if (picked != current)
                KitEditorLanguage.Language = KitEditorLanguage.Languages[picked].Code;
        }
    }
}

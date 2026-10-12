using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Mimicraft.EditorTools
{
    /// <summary>Where the game is, as the kit's windows show and change it: the Pack and Test window, Settings and the
    /// welcome window draw the same block, so a path chosen in one is the path all three show.</summary>
    static class KitGameGui
    {
        static List<KitGame.Install> installs;
        static GUIStyle wrap;

        static string T(string tr, string en) => KitEditorLanguage.T(tr, en);
        static string F(string tr, string en, params object[] args) => KitEditorLanguage.F(tr, en, args);

        /// <summary>Looks in Steam when no game is chosen yet, quietly.</summary>
        public static void FindIfMissing()
        {
            if (!KitGame.IsPlayer(KitGame.GamePath))
                Find(null);
        }

        /// <summary>The copies Steam has installed; the first one is taken when the chosen path is none of them.
        /// Says so on <paramref name="owner"/> when there are none.</summary>
        public static void Find(EditorWindow owner)
        {
            installs = KitGame.FindSteamInstalls();
            if (installs.Count > 0 && !installs.Any(i => i.Path == KitGame.GamePath))
                KitGame.GamePath = installs[0].Path;
            if (owner != null && installs.Count == 0)
                owner.ShowNotification(new GUIContent(T("Steam'de oyun bulunamadı. Elle seç.",
                    "The game was not found in Steam. Choose it by hand.")));
        }

        /// <summary>The game's path with Choose and Find in Steam, the copies to pick from when there are several, and
        /// the maps folder the game reads.</summary>
        public static void Draw(EditorWindow owner)
        {
            wrap ??= new GUIStyle(EditorStyles.label) { wordWrap = true, richText = false };

            string game = KitGame.GamePath;
            bool found = KitGame.IsPlayer(game);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(found ? game : T("Seçilmedi", "Not chosen"), wrap);
                if (GUILayout.Button(T("Seç...", "Choose..."), GUILayout.Width(72)))
                    ChooseGame();
                if (GUILayout.Button(T("Steam'de bul", "Find in Steam"), GUILayout.Width(96)))
                    Find(owner);
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
                    KitPackWindow.RevealMaps();
                if (GUILayout.Button(T("Değiştir...", "Change..."), EditorStyles.miniButton, GUILayout.Width(72)))
                    ChooseDataFolder();
                if (!string.IsNullOrEmpty(KitGame.DataFolderOverride)
                    && GUILayout.Button(T("Varsayılan", "Default"), EditorStyles.miniButton, GUILayout.Width(72)))
                    KitGame.DataFolderOverride = "";
            }
        }

        static void ChooseGame()
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

        static void ChooseDataFolder()
        {
            string picked = EditorUtility.OpenFolderPanel(T("Oyunun veri klasörü", "The game's data folder"), KitGame.DataFolder, "");
            if (!string.IsNullOrEmpty(picked))
                KitGame.DataFolderOverride = picked;
        }
    }
}

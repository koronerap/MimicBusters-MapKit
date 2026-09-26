using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mimicraft.EditorTools
{
    /// <summary>
    /// The kit's Validate window: KitMapValidator run over the open scene, its findings grouped by area,
    /// each with a button that selects the objects it is about.
    ///
    /// Turkish or English, the author's choice, remembered per machine; the first time it follows the
    /// system language. The kit goes to players everywhere, and the game it serves is made in Turkish.
    /// </summary>
    public sealed class KitMapValidatorWindow : EditorWindow
    {
        KitValidation result;
        bool outdated;
        Vector2 scroll;
        GUIStyle wrap;

        [MenuItem("Map Kit/Validate", priority = 10)]
        public static void Open()
        {
            var window = GetWindow<KitMapValidatorWindow>();
            window.titleContent = new GUIContent("Map Kit");
            window.Validate();
        }

        void OnEnable()
        {
            EditorSceneManager.activeSceneChangedInEditMode += OnSceneSwitched;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        void OnDisable()
        {
            EditorSceneManager.activeSceneChangedInEditMode -= OnSceneSwitched;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        }

        void OnSceneSwitched(Scene from, Scene to)
        {
            result = null;
            Repaint();
        }

        // Not re-run on every change: on a big scene a validation is a noticeable pause, and the author is
        // usually in the middle of something. The list says it may be out of date instead.
        void OnHierarchyChanged()
        {
            if (result != null && !outdated)
            {
                outdated = true;
                Repaint();
            }
        }

        public void Validate()
        {
            result = KitMapValidator.Run(SceneManager.GetActiveScene());
            outdated = false;
            Repaint();
        }

        /// <summary>The findings as text, to the clipboard and the Console - for a bug report, or for
        /// whoever is helping with the map.</summary>
        void Report()
        {
            var text = new System.Text.StringBuilder();
            text.AppendLine($"[MapKit] {T("Doğrulama", "Validation")}: {result.SceneName} ({result.MapId}), "
                + string.Format(Culture, T("{0} hata, {1} uyarı", "{0} error(s), {1} warning(s)"), result.Errors, result.Warnings));
            text.AppendLine(string.Format(Culture, "renderers {0}, meshes {1}, triangles {2:N0}, vertices {3:N0}, textures {4} ({5:0.0} MB), lights {6} ({7} shadowed), lightmaps {8}",
                result.Renderers, result.Meshes, result.Triangles, result.Vertices, result.Textures, result.TextureBytes / 1048576.0,
                result.Lights, result.ShadowedRealtimeLights, result.Lightmaps));
            foreach (KitIssue issue in result.Issues.OrderBy(i => (int)i.Area).ThenBy(i => (int)i.Level))
            {
                string names = issue.Targets.Length == 0 ? "" : "  [" + string.Join(", ", issue.Targets.Take(6).Select(t => t.name))
                    + (issue.Targets.Length > 6 ? ", ..." : "") + "]";
                text.AppendLine($"  {issue.Level,-7} {AreaName(issue.Area)}: {issue.Text(turkish)}{names}");
            }

            EditorGUIUtility.systemCopyBuffer = text.ToString();
            Debug.Log(text.ToString());
            ShowNotification(new GUIContent(T("Rapor panoya ve Console'a yazıldı.", "Report copied and written to the Console.")));
        }

        bool turkish => KitEditorLanguage.Turkish;
        static string T(string tr, string en) => KitEditorLanguage.T(tr, en);
        static CultureInfo Culture => KitEditorLanguage.Culture;

        void OnGUI()
        {
            wrap ??= new GUIStyle(EditorStyles.label) { wordWrap = true, richText = false };

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(T("Doğrula", "Validate"), EditorStyles.toolbarButton, GUILayout.Width(80)))
                    Validate();
                GUILayout.Label(SceneManager.GetActiveScene().name, EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(result == null))
                {
                    if (GUILayout.Button(T("Rapor", "Report"), EditorStyles.toolbarButton, GUILayout.Width(60)))
                        Report();
                }
                KitEditorLanguage.Toolbar();
            }

            if (result == null)
            {
                EditorGUILayout.HelpBox(T("Açık sahneyi denetlemek için Doğrula'ya bas.", "Press Validate to check the open scene."), MessageType.Info);
                return;
            }

            DrawSummary();

            if (outdated)
                EditorGUILayout.HelpBox(T("Sahne değişti. Liste eski olabilir, yeniden doğrula.", "The scene has changed. This list may be out of date; validate again."), MessageType.None);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var group in result.Issues.GroupBy(i => i.Area).OrderBy(g => (int)g.Key))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField(AreaName(group.Key), EditorStyles.boldLabel);
                foreach (KitIssue issue in group.OrderBy(i => (int)i.Level))
                    DrawIssue(issue);
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawSummary()
        {
            string summary;
            MessageType type;
            if (!result.CanPack)
            {
                summary = string.Format(Culture, T("{0} hata, {1} uyarı. Hatalar düzelmeden oyun bu haritayı açmaz.",
                    "{0} error(s), {1} warning(s). The game will not open this map until the errors are fixed."), result.Errors, result.Warnings);
                type = MessageType.Error;
            }
            else if (result.Warnings > 0)
            {
                summary = string.Format(Culture, T("Oyun bu haritayı açar. {0} uyarıya yine de bak.",
                    "The game will open this map. The {0} warning(s) are still worth a look."), result.Warnings);
                type = MessageType.Warning;
            }
            else
            {
                summary = T("Oyun bu haritayı açar.", "The game will open this map.");
                type = MessageType.Info;
            }
            EditorGUILayout.HelpBox(summary, type);

            EditorGUILayout.LabelField(string.Format(Culture,
                T("Renderer {0} · mesh {1} · üçgen {2:N0} · vertex {3:N0} · doku {4} ({5:0.0} MB) · ışık {6}, {7} gölgeli · lightmap {8}",
                  "Renderers {0} · meshes {1} · triangles {2:N0} · vertices {3:N0} · textures {4} ({5:0.0} MB) · lights {6}, {7} shadowed · lightmaps {8}"),
                result.Renderers, result.Meshes, result.Triangles, result.Vertices, result.Textures, result.TextureBytes / 1048576.0,
                result.Lights, result.ShadowedRealtimeLights, result.Lightmaps), EditorStyles.wordWrappedMiniLabel);
        }

        void DrawIssue(KitIssue issue)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(Icon(issue.Level), GUILayout.Width(20), GUILayout.Height(18));
                GUILayout.Label(issue.Text(turkish), wrap);
                if (issue.Targets.Length > 0 && GUILayout.Button(T("Seç", "Select"), GUILayout.Width(56)))
                {
                    Selection.objects = issue.Targets;
                    EditorGUIUtility.PingObject(issue.Targets[0]);
                }
            }
        }

        static GUIContent Icon(KitIssueLevel level) => EditorGUIUtility.IconContent(
            level == KitIssueLevel.Error ? "console.erroricon.sml"
            : level == KitIssueLevel.Warning ? "console.warnicon.sml"
            : "console.infoicon.sml");

        string AreaName(KitArea area)
        {
            switch (area)
            {
                case KitArea.Scene: return T("Sahne", "Scene");
                case KitArea.MapInfo: return T("Harita bilgisi", "Map info");
                case KitArea.Components: return T("Bileşenler", "Components");
                case KitArea.Materials: return T("Materyaller", "Materials");
                case KitArea.Layers: return T("Katmanlar", "Layers");
                case KitArea.Lighting: return T("Işık", "Lighting");
                case KitArea.Markers: return T("İşaretçiler", "Markers");
                case KitArea.Bounds: return T("Harita sınırı", "Map bounds");
                case KitArea.Budget: return T("Bütçe", "Budget");
                case KitArea.Logic: return T("Harita mantığı", "Map logic");
                default: return T("Paketleme", "Packaging");
            }
        }
    }
}

using System.Linq;
using Mimicraft.MapKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mimicraft.EditorTools
{
    /// <summary>
    /// Map Kit > Logic: every map entity in the open scene and every connection between them on one page
    /// (plan §16.7), so a map's wiring can be read without clicking through objects one by one. A
    /// connection that cannot work is red, and each line selects its entity.
    /// </summary>
    public sealed class KitLogicWindow : EditorWindow
    {
        Vector2 scroll;
        GUIStyle broken;

        static string T(string tr, string en) => KitEditorLanguage.T(tr, en);

        [MenuItem("Map Kit/Logic", priority = 6)]
        public static void Open()
        {
            var window = GetWindow<KitLogicWindow>(false, "Map Logic", true);
            window.minSize = new Vector2(420, 240);
        }

        void OnHierarchyChange() => Repaint();

        void OnSelectionChange() => Repaint();

        void OnGUI()
        {
            broken ??= new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.95f, 0.35f, 0.3f) } };
            Scene scene = SceneManager.GetActiveScene();

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(T("Harita mantığı", "Map logic") + " · " + scene.name, EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(T("Numaraları ver", "Number them"), EditorStyles.toolbarButton))
                    MapLogicIds.Ensure(scene);
                KitEditorLanguage.Toolbar();
            }

            MapEntity[] entities = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MapEntity>(true))
                .OrderBy(e => e.GetType().Name).ThenBy(e => e.EntityId)
                .ToArray();

            if (entities.Length == 0)
            {
                EditorGUILayout.HelpBox(T("Bu sahnede harita mantığı yok. Bir objeye Add Component > Map Kit > Logic altından ekle: "
                        + "düğme, röle, zamanlayıcı, sayaç, göster/gizle, round olayları.",
                        "There is no map logic in this scene. Add some from Add Component > Map Kit > Logic: a button, a relay, "
                        + "a timer, a counter, a toggle, round events."), MessageType.Info);
                return;
            }

            int connections = entities.Sum(e => e.Outputs.Count(o => o != null));
            EditorGUILayout.LabelField(string.Format(T("{0} varlık, {1} bağlantı", "{0} entities, {1} connections"), entities.Length, connections),
                EditorStyles.miniLabel);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (MapEntity entity in entities)
            {
                bool selected = Selection.activeGameObject == entity.gameObject;
                using (new EditorGUILayout.HorizontalScope())
                {
                    string label = $"{(entity.EntityId > 0 ? "#" + entity.EntityId : "#?")}  {entity.name}  ({entity.GetType().Name})"
                        + (entity.StartEnabled ? "" : T("  kapalı başlar", "  starts disabled"));
                    GUILayout.Label(label, selected ? EditorStyles.boldLabel : EditorStyles.label);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(T("Seç", "Select"), EditorStyles.miniButton, GUILayout.Width(44)))
                    {
                        Selection.activeObject = entity;
                        EditorGUIUtility.PingObject(entity);
                    }
                }

                foreach (MapOutput output in entity.Outputs)
                {
                    if (output == null)
                        continue;

                    string target = output.target != null ? output.target.name : T("(hedef yok)", "(no target)");
                    string line = $"      {output.onEvent} → {target}: {output.input}"
                        + (MapLogicGuide.ParameterLabel(output.input) != null ? $" {output.parameter:0.##}" : "")
                        + (output.delay > 0f ? $"  +{output.delay:0.##} s" : "")
                        + (output.once ? T("  bir kez", "  once") : "");
                    bool works = output.target != null && output.target.gameObject.scene == entity.gameObject.scene
                        && output.target.Takes(output.input) && entity.Raises(output.onEvent);
                    GUILayout.Label(line, works ? EditorStyles.miniLabel : broken);
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }
}

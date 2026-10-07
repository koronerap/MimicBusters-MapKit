using Mimicraft.MapKit;
using UnityEditor;
using UnityEngine;

namespace Mimicraft.EditorTools
{
    /// <summary>The variant group's Inspector: what it does in one paragraph, and its versions listed with their
    /// weights and chances, each with a button that shows that one alone in the scene - to build and check one version
    /// at a time. A preview is an ordinary edit (undoable), and what is left switched on is what modes that do not
    /// choose will show.</summary>
    [CustomEditor(typeof(MapVariantGroup))]
    public sealed class MapVariantGroupEditor : Editor
    {
        static string T(string tr, string en) => KitEditorLanguage.T(tr, en);

        public override void OnInspectorGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                KitEditorLanguage.Toolbar();
            }

            EditorGUILayout.HelpBox(T(
                "Her round bu objenin çocuklarından BİRİ açılır, diğerleri kapanır; Avcılar haritayı ezberleyemez. " +
                "Her versiyonu bu objenin doğrudan çocuğu yap. Seçimi sunucu yapar, herkes aynısını görür. " +
                "Seçili olmayan modlarda çocuklar sahnede bırakıldığı gibi kalır. Ağırlık için çocuğa Map Variant " +
                "Option ekle. Versiyonun içindeki spawn işaretçileri yalnızca o versiyon açıkken kullanılır.",
                "Each round ONE of this object's children is switched on and the rest off, so Hunters cannot learn " +
                "the map by heart. Make every version a direct child of this object. The server chooses and " +
                "everybody sees the same. In modes not ticked, the children stay as authored. Add Map Variant Option " +
                "to a child for a weight. Spawn markers inside a version are used only while it is on."),
                MessageType.None);

            DrawDefaultInspector();

            var group = (MapVariantGroup)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(T($"Versiyonlar ({group.Count})", $"Versions ({group.Count})"),
                EditorStyles.boldLabel);

            if (group.Count == 0)
            {
                EditorGUILayout.HelpBox(T("Çocuk yok. Her versiyonu bu objenin altına bir çocuk olarak ekle.",
                    "No children. Add each version under this object as a child."), MessageType.Warning);
                return;
            }

            float total = 0f;
            for (int i = 0; i < group.Count; i++)
                total += group.WeightOf(i);

            for (int i = 0; i < group.Count; i++)
            {
                Transform child = group.transform.GetChild(i);
                float weight = group.WeightOf(i);
                string chance = total > 0f ? $"{weight / total * 100f:0}%" : "-";

                using (new EditorGUILayout.HorizontalScope())
                {
                    bool on = child.gameObject.activeSelf;
                    GUILayout.Label(on ? "●" : "○", GUILayout.Width(14));
                    if (GUILayout.Button(child.name, EditorStyles.label))
                        EditorGUIUtility.PingObject(child.gameObject);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"{T("ağırlık", "weight")} {weight:0.##}  {chance}", GUILayout.Width(120));
                    if (GUILayout.Button(T("Göster", "Show"), GUILayout.Width(60)))
                        ShowOnly(group, i);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(T("Rastgele birini göster", "Show a random one")))
                    ShowOnly(group, group.Pick(Random.value, -1));
                if (GUILayout.Button(T("Hepsini aç", "Switch all on")))
                    ShowOnly(group, -1);
            }
        }

        /// <summary>One child on and the rest off, or all on for -1. Undoable, and marks the scene changed.</summary>
        static void ShowOnly(MapVariantGroup group, int index)
        {
            for (int i = 0; i < group.Count; i++)
            {
                GameObject child = group.transform.GetChild(i).gameObject;
                bool on = index < 0 || i == index;
                if (child.activeSelf == on)
                    continue;

                Undo.RecordObject(child, "Show variant");
                child.SetActive(on);
            }
        }
    }
}

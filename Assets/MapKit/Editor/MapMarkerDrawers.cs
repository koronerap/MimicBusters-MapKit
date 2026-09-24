using Mimicraft.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Mimicraft.EditorTools
{
    /// <summary>
    /// A marker mask (MapMarkerModes, MapMarkerKind) as a foldout of checkboxes, each with what it means,
    /// instead of Unity's flags dropdown: the LobbySettingFieldsDrawer treatment, for the same reasons.
    /// The dropdown closes on every click and says nothing about what a bit is for; these boxes stay put
    /// and explain themselves, in Turkish or English (KitEditorLanguage).
    ///
    /// On the types, so every field of them gets it: MapMarker's two, the Kit Map Descriptor's modes,
    /// MapModeObject's modes, the bounds volume's seed kinds.
    /// </summary>
    public abstract class MarkerMaskDrawer : PropertyDrawer
    {
        const float Indent = 12f;
        const float NameWidth = 132f;

        protected abstract MapMarkerGuide.Entry[] Entries { get; }

        /// <summary>The quick-set buttons: a label and the whole value it writes.</summary>
        protected abstract (string label, int value)[] QuickSets { get; }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            float gap = EditorGUIUtility.standardVerticalSpacing;
            if (property.propertyType != SerializedPropertyType.Enum || !property.isExpanded)
                return line;
            return (line + gap) * (2 + Entries.Length);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Enum)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);
            float line = EditorGUIUtility.singleLineHeight;
            float gap = EditorGUIUtility.standardVerticalSpacing;
            var row = new Rect(position.x, position.y, position.width, line);

            int value = property.enumValueFlag;
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded,
                new GUIContent($"{label.text}   ({MapMarkerGuide.Summary(value, Entries)})", label.tooltip), true);

            if (property.isExpanded)
            {
                row.y += line + gap;
                var body = new Rect(position.x + Indent, row.y, position.width - Indent, line);
                value = DrawQuickSets(body, value);
                body.y += line + gap;

                foreach (MapMarkerGuide.Entry entry in Entries)
                {
                    var box = new Rect(body.x, body.y, NameWidth, line);
                    var note = new Rect(body.x + NameWidth, body.y, body.width - NameWidth, line);

                    EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
                    bool on = EditorGUI.ToggleLeft(box, new GUIContent(entry.Name, entry.Description), (value & entry.Bit) != 0);
                    EditorGUI.showMixedValue = false;
                    EditorGUI.LabelField(note, entry.Description, EditorStyles.miniLabel);

                    value = on ? value | entry.Bit : value & ~entry.Bit;
                    body.y += line + gap;
                }
            }

            if (value != property.enumValueFlag)
                property.enumValueFlag = value;
            EditorGUI.EndProperty();
        }

        int DrawQuickSets(Rect row, int value)
        {
            (string label, int value)[] sets = QuickSets;
            float width = (row.width - 3f * (sets.Length - 1)) / sets.Length;
            var slot = new Rect(row.x, row.y, width, row.height);
            for (int i = 0; i < sets.Length; i++)
            {
                GUIStyle style = sets.Length == 1 ? EditorStyles.miniButton
                    : i == 0 ? EditorStyles.miniButtonLeft
                    : i == sets.Length - 1 ? EditorStyles.miniButtonRight
                    : EditorStyles.miniButtonMid;
                if (GUI.Button(slot, sets[i].label, style))
                    value = sets[i].value;
                slot.x += width + 3f;
            }
            return value;
        }
    }

    /// <summary>Which map modes use a marker, a mode object or a map.</summary>
    [CustomPropertyDrawer(typeof(MapMarkerModes))]
    public sealed class MapMarkerModesDrawer : MarkerMaskDrawer
    {
        protected override MapMarkerGuide.Entry[] Entries => MapMarkerGuide.Modes;

        // "All" writes Unity's Everything (~0), which the built-in maps' markers already carry.
        protected override (string label, int value)[] QuickSets => new[]
        {
            (KitEditorLanguage.T("Hepsi", "All"), ~0),
            ("Standard + Practice", (int)(MapMarkerModes.Standard | MapMarkerModes.Practice)),
            (KitEditorLanguage.T("Hiçbiri", "None"), 0),
        };
    }

    /// <summary>What a marker is for.</summary>
    [CustomPropertyDrawer(typeof(MapMarkerKind))]
    public sealed class MapMarkerKindDrawer : MarkerMaskDrawer
    {
        protected override MapMarkerGuide.Entry[] Entries => MapMarkerGuide.Kinds;

        protected override (string label, int value)[] QuickSets => new[]
        {
            (KitEditorLanguage.T("Doğma noktası", "Spawn point"), (int)(MapMarkerKind.HiderSpawn | MapMarkerKind.LobbySpawn)),
            (KitEditorLanguage.T("Hiçbiri", "None"), 0),
        };
    }
}

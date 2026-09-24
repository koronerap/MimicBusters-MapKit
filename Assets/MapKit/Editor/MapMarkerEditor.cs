using System.Collections.Generic;
using System.Linq;
using Mimicraft.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Mimicraft.EditorTools
{
    /// <summary>
    /// The MapMarker inspector: modes first, because which kinds mean anything depends on them (the
    /// field's own tooltip always said so), then kinds, then what the two add up to. For every mode
    /// ticked, which of the ticked kinds it reads; and any kind ticked that no ticked mode reads, which is
    /// a point that is silently ignored in the game.
    ///
    /// The boxes themselves are MarkerMaskDrawer's, so the same fields look the same everywhere else.
    /// </summary>
    [CustomEditor(typeof(MapMarker)), CanEditMultipleObjects]
    public sealed class MapMarkerEditor : Editor
    {
        SerializedProperty modes;
        SerializedProperty kinds;

        static string T(string tr, string en) => KitEditorLanguage.T(tr, en);

        void OnEnable()
        {
            modes = serializedObject.FindProperty("modes");
            kinds = serializedObject.FindProperty("kinds");

            // Open by default: they are all this inspector is for.
            modes.isExpanded = true;
            kinds.isExpanded = true;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                KitEditorLanguage.Toolbar();
            }

            EditorGUILayout.PropertyField(modes, new GUIContent(T("1. Modlar", "1. Modes"),
                T("Bu işaretçiyi hangi modlar kullanır. Hiçbiri seçili değilse hiçbir mod kullanmaz.",
                    "Which modes use this marker. With none ticked, no mode does.")));
            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(kinds, new GUIContent(T("2. Türler", "2. Kinds"),
                T("Bu nokta ne için. Bir nokta birkaç şey birden olabilir.", "What this point is for. One point can be several things.")));

            serializedObject.ApplyModifiedProperties();

            if (modes.hasMultipleDifferentValues || kinds.hasMultipleDifferentValues)
                return;

            EditorGUILayout.Space(4);
            DrawVerdict((MapMarkerModes)modes.enumValueFlag, (MapMarkerKind)kinds.enumValueFlag);
        }

        static void DrawVerdict(MapMarkerModes modeMask, MapMarkerKind kindMask)
        {
            if (modeMask == MapMarkerModes.None)
            {
                EditorGUILayout.HelpBox(T("Hiçbir mod seçili değil: bu işaretçiyi oyunda hiçbir mod kullanmaz.",
                    "No mode is ticked: no mode in the game uses this marker."), MessageType.Warning);
                return;
            }

            if (kindMask == MapMarkerKind.None)
            {
                EditorGUILayout.HelpBox(T("Hiçbir tür seçili değil: bu nokta hiçbir şey için kullanılmaz.",
                    "No kind is ticked: this point is used for nothing."), MessageType.Warning);
                return;
            }

            var lines = new List<string>();
            foreach (MapMarkerGuide.Entry mode in MapMarkerGuide.Modes)
            {
                if (((int)modeMask & mode.Bit) == 0)
                    continue;
                var read = (int)(kindMask & MapMarkerGuide.Reads((MapMarkerModes)mode.Bit));
                lines.Add(read == 0
                    ? string.Format(T("{0}: bu türlerin hiçbirini okumaz", "{0}: reads none of these kinds"), mode.Name)
                    : $"{mode.Name}: {string.Join(", ", MapMarkerGuide.NamesIn(read, MapMarkerGuide.Kinds))}");
            }
            EditorGUILayout.HelpBox(T("Oyunda kim, ne olarak kullanır:\n", "Who uses it in the game, and as what:\n")
                + string.Join("\n", lines), MessageType.Info);

            var unused = (int)(kindMask & ~MapMarkerGuide.Reads(modeMask));
            string[] names = MapMarkerGuide.NamesIn(unused, MapMarkerGuide.Kinds).ToArray();
            if (names.Length > 0)
                EditorGUILayout.HelpBox(string.Format(T("{0}: seçili modların hiçbiri bu türü okumaz. Oyunda yok sayılır.",
                    "{0}: none of the ticked modes reads this kind. The game ignores it."), string.Join(", ", names)),
                    MessageType.Warning);
        }
    }
}

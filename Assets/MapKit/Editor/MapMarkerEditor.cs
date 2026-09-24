using System.Collections.Generic;
using Mimicraft.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Mimicraft.EditorTools
{
    /// <summary>
    /// The MapMarker inspector: modes first, because which kinds mean anything depends on them (the
    /// field's own tooltip always said so), then the kinds those modes read, then what the two add up to.
    ///
    /// The kinds list follows the modes. With only Deathmatch ticked it offers HiderSpawn, HunterSpawn and
    /// LobbySpawn, the kinds Deathmatch reads, and folds the rest away under one line that can be opened.
    /// A ticked kind is never folded away, read or not: a box nobody can see that still holds its bit
    /// would make the marker do something invisible (under Deathmatch alone a ticked HunterDoor gets its
    /// object switched off as a door). It stays in the list, marked as unread by the ticked modes.
    ///
    /// The modes' boxes are MarkerMaskDrawer's, so that field looks the same everywhere else. The kinds
    /// are drawn here, because only here are the marker's modes known; everywhere else (the bounds
    /// volume's seed kinds) they keep the plain drawer.
    /// </summary>
    [CustomEditor(typeof(MapMarker)), CanEditMultipleObjects]
    public sealed class MapMarkerEditor : Editor
    {
        SerializedProperty modes;
        SerializedProperty kinds;
        bool showUnread;
        GUIStyle unreadNote;

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
            unreadNote ??= new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.95f, 0.6f, 0.15f) }, wordWrap = true };

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                KitEditorLanguage.Toolbar();
            }

            EditorGUILayout.PropertyField(modes, new GUIContent(T("1. Modlar", "1. Modes"),
                T("Bu işaretçiyi hangi modlar kullanır. Hiçbiri seçili değilse hiçbir mod kullanmaz.",
                    "Which modes use this marker. With none ticked, no mode does.")));
            EditorGUILayout.Space(2);

            bool mixed = modes.hasMultipleDifferentValues || kinds.hasMultipleDifferentValues;
            if (mixed)
                EditorGUILayout.PropertyField(kinds, new GUIContent(T("2. Türler", "2. Kinds")));
            else
                DrawKinds((MapMarkerModes)modes.enumValueFlag);

            serializedObject.ApplyModifiedProperties();

            if (mixed)
                return;

            EditorGUILayout.Space(4);
            DrawVerdict((MapMarkerModes)modes.enumValueFlag, (MapMarkerKind)kinds.enumValueFlag);
        }

        void DrawKinds(MapMarkerModes modeMask)
        {
            int value = kinds.enumValueFlag;
            kinds.isExpanded = EditorGUILayout.Foldout(kinds.isExpanded,
                $"{T("2. Türler", "2. Kinds")}   ({MapMarkerGuide.Summary(value, MapMarkerGuide.Kinds)})", true);
            if (!kinds.isExpanded)
                return;

            bool anyMode = modeMask != MapMarkerModes.None;
            int read = anyMode ? (int)MapMarkerGuide.Reads(modeMask) : ~0;

            using (new EditorGUI.IndentLevelScope())
            {
                if (!anyMode)
                    EditorGUILayout.LabelField(T("Önce modları seç: türler seçtiğin modların okuduklarına göre listelenir.",
                        "Pick the modes first: the kinds are listed by what the modes you pick read."), EditorStyles.wordWrappedMiniLabel);

                var unread = new List<MapMarkerGuide.Entry>();
                foreach (MapMarkerGuide.Entry entry in MapMarkerGuide.Kinds)
                {
                    bool on = (value & entry.Bit) != 0;
                    bool isRead = (read & entry.Bit) != 0;
                    if (!isRead && !on)
                    {
                        unread.Add(entry);
                        continue;
                    }
                    value = KindRow(entry, value, isRead);
                }

                if (unread.Count > 0)
                {
                    showUnread = EditorGUILayout.Foldout(showUnread,
                        string.Format(T("Bu modların okumadığı türler ({0})", "Kinds these modes do not read ({0})"), unread.Count), true);
                    if (showUnread)
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            foreach (MapMarkerGuide.Entry entry in unread)
                                value = KindRow(entry, value, false);
                        }
                    }
                }

                if (GUILayout.Button(T("Türleri temizle", "Clear the kinds"), EditorStyles.miniButton, GUILayout.Width(120)))
                    value = 0;
            }

            if (value != kinds.enumValueFlag)
                kinds.enumValueFlag = value;
        }

        /// <summary>One kind's box and what it is for; for a kind the ticked modes do not read, the note
        /// says that first, in a warning colour.</summary>
        int KindRow(MapMarkerGuide.Entry entry, int value, bool read)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                bool on = EditorGUILayout.ToggleLeft(new GUIContent(entry.Name, entry.Description), (value & entry.Bit) != 0,
                    GUILayout.Width(170));
                bool ticked = (value & entry.Bit) != 0;
                if (read || !ticked)
                    EditorGUILayout.LabelField(entry.Description, EditorStyles.miniLabel);
                else
                    EditorGUILayout.LabelField(T("Seçili modlar bunu okumaz. ", "The ticked modes do not read this. ") + entry.Description, unreadNote);
                return on ? value | entry.Bit : value & ~entry.Bit;
            }
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
                var used = (int)(kindMask & MapMarkerGuide.Reads((MapMarkerModes)mode.Bit));
                lines.Add(used == 0
                    ? string.Format(T("{0}: bu türlerin hiçbirini okumaz", "{0}: reads none of these kinds"), mode.Name)
                    : $"{mode.Name}: {string.Join(", ", MapMarkerGuide.NamesIn(used, MapMarkerGuide.Kinds))}");
            }
            EditorGUILayout.HelpBox(T("Oyunda kim, ne olarak kullanır:\n", "Who uses it in the game, and as what:\n")
                + string.Join("\n", lines), MessageType.Info);
        }
    }
}
